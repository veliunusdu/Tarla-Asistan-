import uuid
from collections.abc import Callable

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.ai_chat import AIChatProvider, LocalAIChatProvider
from app.config import Settings, get_settings
from app.database import get_db
from app.firebase_auth import (
    FirebaseAuthUnavailableError,
    FirebaseTokenError,
    verify_firebase_id_token,
)
from app.models import User, UserRole
from app.media_storage import MediaStorage, create_media_storage
from app.otp import OtpStore
from app.push import PushProvider
from app.security import decode_access_token
from app.weather import WeatherProvider

bearer = HTTPBearer(auto_error=False)


def _unauthorized() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="Geçersiz veya süresi dolmuş oturum.",
        headers={"WWW-Authenticate": "Bearer"},
    )


def _firebase_unavailable() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
        detail="Kimlik doğrulama hizmeti kullanılamıyor.",
    )


def _firebase_mapping_conflict() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_409_CONFLICT,
        detail="Firebase hesabı yerel hesaba bağlanamadı.",
    )


def _resolve_firebase_mapping_after_race(
    db: Session, *, uid: str, phone_number: str
) -> User:
    user = db.scalar(select(User).where(User.firebase_uid == uid))
    if user is not None:
        return user
    user = db.scalar(select(User).where(User.phone_number == phone_number))
    if user is not None and user.firebase_uid == uid:
        return user
    raise _firebase_mapping_conflict()


def get_otp_store(request: Request) -> OtpStore:
    return request.app.state.otp_store


def get_weather_provider(request: Request) -> WeatherProvider:
    return request.app.state.weather_provider


def get_push_provider(request: Request) -> PushProvider:
    return request.app.state.push_provider


def get_ai_chat_provider(request: Request) -> AIChatProvider:
    return getattr(request.app.state, "ai_chat_provider", LocalAIChatProvider())


def get_media_storage(
    request: Request, settings: Settings = Depends(get_settings)
) -> MediaStorage:
    storage = getattr(request.app.state, "media_storage", None)
    return storage if storage is not None else create_media_storage(settings)


def get_current_user(
    credentials: HTTPAuthorizationCredentials | None = Depends(bearer),
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> User:
    if credentials is None or credentials.scheme.lower() != "bearer":
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Oturum açmanız gerekiyor.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    if settings.firebase_auth_enabled:
        try:
            identity = verify_firebase_id_token(credentials.credentials)
        except FirebaseTokenError as exc:
            raise _unauthorized() from exc
        except FirebaseAuthUnavailableError as exc:
            raise _firebase_unavailable() from exc
        user = db.scalar(select(User).where(User.firebase_uid == identity.uid))
        if user is not None:
            return user
        if not identity.phone_number:
            raise _unauthorized()
        user = db.scalar(
            select(User).where(User.phone_number == identity.phone_number)
        )
        if user is not None:
            if user.firebase_uid is not None:
                raise _firebase_mapping_conflict()
            result = db.execute(
                update(User)
                .where(
                    User.phone_number == identity.phone_number,
                    User.firebase_uid.is_(None),
                )
                .values(firebase_uid=identity.uid)
                .execution_options(synchronize_session=False)
            )
            if result.rowcount == 1:
                try:
                    db.commit()
                except IntegrityError:
                    db.rollback()
                    return _resolve_firebase_mapping_after_race(
                        db,
                        uid=identity.uid,
                        phone_number=identity.phone_number,
                    )
                db.expire_all()
                return _resolve_firebase_mapping_after_race(
                    db,
                    uid=identity.uid,
                    phone_number=identity.phone_number,
                )
            db.rollback()
            return _resolve_firebase_mapping_after_race(
                db,
                uid=identity.uid,
                phone_number=identity.phone_number,
            )
        user = User(
            phone_number=identity.phone_number,
            firebase_uid=identity.uid,
            role=UserRole.FARMER,
        )
        db.add(user)
        try:
            db.commit()
        except IntegrityError as exc:
            db.rollback()
            user = db.scalar(select(User).where(User.firebase_uid == identity.uid))
            if user is not None:
                return user
            try:
                return _resolve_firebase_mapping_after_race(
                    db,
                    uid=identity.uid,
                    phone_number=identity.phone_number,
                )
            except HTTPException as conflict:
                raise conflict from exc
        db.refresh(user)
        return user
    payload = decode_access_token(credentials.credentials, settings)
    try:
        user_id = uuid.UUID(payload["sub"])
    except (ValueError, TypeError) as exc:
        raise HTTPException(status_code=401, detail="Geçersiz oturum.") from exc
    user = db.get(User, user_id)
    if user is None:
        raise HTTPException(status_code=401, detail="Kullanıcı bulunamadı.")
    return user


def require_roles(*roles: UserRole) -> Callable[[User], User]:
    def dependency(user: User = Depends(get_current_user)) -> User:
        if user.role not in roles:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Bu işlem için yetkiniz yok.",
            )
        return user

    return dependency

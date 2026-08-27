import uuid
from collections.abc import Callable

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.ai_chat import AIChatProvider, LocalAIChatProvider
from app.config import Settings, get_settings
from app.database import get_db
from app.firebase_auth import (
    FirebaseAuthUnavailableError,
    FirebaseTokenError,
    verify_firebase_id_token,
)
from app.firebase_mapping import require_active, resolve_firebase_user
from app.media_storage import MediaStorage, create_media_storage
from app.models import AccountStatus, User, UserRole
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
    # 1. Try resolving backend-issued JWT access token
    try:
        payload = decode_access_token(credentials.credentials, settings)
        user_id = uuid.UUID(payload["sub"])
        user = db.get(User, user_id)
        if user is not None:
            return require_active(user)
    except HTTPException as exc:
        if exc.status_code == status.HTTP_403_FORBIDDEN:
            raise
    except Exception:
        pass

    # 2. If token is not an internal JWT and Firebase Auth is enabled, verify Firebase ID token
    if settings.firebase_auth_enabled:
        try:
            identity = verify_firebase_id_token(credentials.credentials)
            return resolve_firebase_user(db, identity)
        except FirebaseTokenError as exc:
            raise _unauthorized() from exc
        except FirebaseAuthUnavailableError as exc:
            raise _firebase_unavailable() from exc

    raise _unauthorized()


def get_account_deletion_request_user(
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
    user = None
    try:
        payload = decode_access_token(credentials.credentials, settings)
        user_id = uuid.UUID(payload["sub"])
        user = db.get(User, user_id)
    except HTTPException as exc:
        if exc.status_code == status.HTTP_403_FORBIDDEN:
            raise
        user = None
    except Exception:
        user = None

    if user is None:
        if settings.firebase_auth_enabled:
            try:
                identity = verify_firebase_id_token(credentials.credentials)
            except FirebaseTokenError as exc:
                raise _unauthorized() from exc
            except FirebaseAuthUnavailableError as exc:
                raise _firebase_unavailable() from exc
            existing = db.scalar(select(User).where(User.firebase_uid == identity.uid))
            user = existing if existing is not None else resolve_firebase_user(db, identity)
        else:
            raise _unauthorized()
    if user.account_status not in {
        AccountStatus.ACTIVE,
        AccountStatus.DELETION_PENDING,
    }:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Hesap aktif değil.",
        )
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

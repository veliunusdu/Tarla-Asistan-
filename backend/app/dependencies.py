import uuid
from collections.abc import Callable

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
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
from app.models import User, UserRole
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
    if settings.firebase_auth_enabled:
        try:
            identity = verify_firebase_id_token(credentials.credentials)
        except FirebaseTokenError as exc:
            raise _unauthorized() from exc
        except FirebaseAuthUnavailableError as exc:
            raise _firebase_unavailable() from exc
        return resolve_firebase_user(db, identity)
    payload = decode_access_token(credentials.credentials, settings)
    try:
        user_id = uuid.UUID(payload["sub"])
    except (ValueError, TypeError) as exc:
        raise HTTPException(status_code=401, detail="Geçersiz oturum.") from exc
    user = db.get(User, user_id)
    if user is None:
        raise HTTPException(status_code=401, detail="Kullanıcı bulunamadı.")
    return require_active(user)


def require_roles(*roles: UserRole) -> Callable[[User], User]:
    def dependency(user: User = Depends(get_current_user)) -> User:
        if user.role not in roles:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Bu işlem için yetkiniz yok.",
            )
        return user

    return dependency

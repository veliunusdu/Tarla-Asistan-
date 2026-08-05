import uuid
from collections.abc import Callable

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import get_db
from app.models import User, UserRole
from app.otp import OtpStore
from app.push import PushProvider
from app.security import decode_access_token
from app.weather import WeatherProvider

bearer = HTTPBearer(auto_error=False)


def get_otp_store(request: Request) -> OtpStore:
    return request.app.state.otp_store


def get_weather_provider(request: Request) -> WeatherProvider:
    return request.app.state.weather_provider


def get_push_provider(request: Request) -> PushProvider:
    return request.app.state.push_provider


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

import logging
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, Response, status
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import get_db
from app.dependencies import get_current_user, get_otp_store
from app.firebase_auth import (
    FirebaseAuthUnavailableError,
    FirebaseIdentity,
    FirebaseTokenError,
    verify_firebase_id_token,
)
from app.firebase_mapping import (
    lock_active_user_for_update,
    resolve_firebase_user,
)
from app.models import RefreshToken, User, UserRole
from app.otp import (
    OtpExpired,
    OtpInvalid,
    OtpRateLimited,
    OtpService,
    OtpStore,
)
from app.schemas import (
    FirebaseLoginRequest,
    RefreshTokenRequest,
    RequestOtpRequest,
    RequestOtpResponse,
    TokenResponse,
    UserResponse,
    VerifyOtpRequest,
)
from app.security import create_access_token, create_refresh_token, hash_refresh_token

router = APIRouter(prefix="/auth", tags=["Kimlik doğrulama"])
logger = logging.getLogger(__name__)


def issue_session(user: User, db: Session, settings: Settings) -> TokenResponse:
    locked_user = lock_active_user_for_update(db, user.id)
    access_token = create_access_token(
        str(locked_user.id), locked_user.role.value, settings
    )
    raw_refresh_token, refresh_token = create_refresh_token(locked_user.id, settings)
    db.add(refresh_token)
    db.commit()
    return TokenResponse(
        access_token=access_token,
        refresh_token=raw_refresh_token,
        expires_in=settings.access_token_expire_minutes * 60,
        user=UserResponse.model_validate(locked_user),
    )


@router.post(
    "/request-otp",
    response_model=RequestOtpResponse,
    summary="Telefon numarasına tek kullanımlık kod gönder",
)
def request_otp(
    payload: RequestOtpRequest,
    store: OtpStore = Depends(get_otp_store),
    settings: Settings = Depends(get_settings),
) -> RequestOtpResponse:
    service = OtpService(store, settings)
    try:
        code = service.issue(payload.phone_number)
    except OtpRateLimited as exc:
        raise HTTPException(
            status_code=status.HTTP_429_TOO_MANY_REQUESTS,
            detail="Yeni kod istemeden önce kısa bir süre bekleyin.",
            headers={"Retry-After": str(settings.otp_request_cooldown_seconds)},
        ) from exc

    expose_debug_otp = (
        settings.environment == "local" and settings.otp_expose_in_response
    )
    # SMS sağlayıcısı Sprint 1 kapsamı dışında; kod yalnızca açık yerel modda loglanır.
    if expose_debug_otp:
        logger.info("Development OTP issued for %s: %s", payload.phone_number, code)
    return RequestOtpResponse(
        expires_in=settings.otp_ttl_seconds,
        debug_otp=code if expose_debug_otp else None,
    )


@router.post(
    "/verify-otp",
    response_model=TokenResponse,
    summary="Tek kullanımlık kodu doğrula ve JWT üret",
)
def verify_otp(
    payload: VerifyOtpRequest,
    store: OtpStore = Depends(get_otp_store),
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> TokenResponse:
    service = OtpService(store, settings)
    try:
        service.verify(payload.phone_number, payload.otp_code)
    except OtpExpired as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Kod bulunamadı veya süresi doldu.",
        ) from exc
    except OtpInvalid as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Kod geçersiz veya deneme sınırı aşıldı.",
        ) from exc

    user = db.scalar(select(User).where(User.phone_number == payload.phone_number))
    if user is None:
        role = (
            UserRole.AGRONOMIST
            if payload.phone_number in settings.agronomist_phone_numbers
            else UserRole.FARMER
        )
        user = User(phone_number=payload.phone_number, role=role)
        db.add(user)
        db.commit()
        db.refresh(user)

    return issue_session(user, db, settings)


@router.post(
    "/firebase",
    response_model=TokenResponse,
    summary="Firebase ID tokenını doğrula ve JWT üret",
)
def firebase_login(
    payload: FirebaseLoginRequest,
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> TokenResponse:
    if not settings.firebase_auth_enabled and settings.environment == "local" and (
        payload.id_token.startswith("dev_") or payload.id_token.startswith("mock_")
    ):
        phone = "+905550000000"
        identity = FirebaseIdentity(uid=payload.id_token, phone_number=phone)
        user = resolve_firebase_user(db, identity)
        return issue_session(user, db, settings)

    try:
        identity = verify_firebase_id_token(payload.id_token)
    except FirebaseTokenError as exc:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Geçersiz veya süresi dolmuş Firebase kimlik doğrulama belirteci.",
            headers={"WWW-Authenticate": "Bearer"},
        ) from exc
    except FirebaseAuthUnavailableError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Kimlik doğrulama hizmeti kullanılamıyor.",
        ) from exc

    user = resolve_firebase_user(db, identity)
    return issue_session(user, db, settings)


@router.post(
    "/refresh",
    response_model=TokenResponse,
    summary="Refresh tokenı döndürerek oturumu yenile",
)
def refresh_session(
    payload: RefreshTokenRequest,
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> TokenResponse:
    stored_token = db.scalar(
        select(RefreshToken).where(
            RefreshToken.token_hash == hash_refresh_token(payload.refresh_token)
        )
    )
    if stored_token is None or stored_token.revoked_at is not None:
        raise HTTPException(status_code=401, detail="Refresh oturumu geçersiz.")

    expires_at = stored_token.expires_at
    if expires_at.tzinfo is None:
        expires_at = expires_at.replace(tzinfo=timezone.utc)
    if expires_at <= datetime.now(timezone.utc):
        stored_token.revoked_at = datetime.now(timezone.utc)
        db.commit()
        raise HTTPException(status_code=401, detail="Refresh oturumunun süresi doldu.")

    user = lock_active_user_for_update(db, stored_token.user_id)
    stored_token = db.scalar(
        select(RefreshToken)
        .where(RefreshToken.id == stored_token.id)
        .execution_options(populate_existing=True)
    )
    if stored_token is None or stored_token.revoked_at is not None:
        raise HTTPException(status_code=401, detail="Refresh oturumu geçersiz.")

    expires_at = stored_token.expires_at
    if expires_at.tzinfo is None:
        expires_at = expires_at.replace(tzinfo=timezone.utc)
    if expires_at <= datetime.now(timezone.utc):
        stored_token.revoked_at = datetime.now(timezone.utc)
        db.commit()
        raise HTTPException(status_code=401, detail="Refresh oturumunun süresi doldu.")

    stored_token.revoked_at = datetime.now(timezone.utc)
    raw_refresh_token, replacement = create_refresh_token(
        user.id, settings, family_id=stored_token.family_id
    )
    db.add(replacement)
    db.commit()
    return TokenResponse(
        access_token=create_access_token(str(user.id), user.role.value, settings),
        refresh_token=raw_refresh_token,
        expires_in=settings.access_token_expire_minutes * 60,
        user=UserResponse.model_validate(user),
    )


@router.post(
    "/logout",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="Refresh oturumunu iptal et",
)
def logout(
    payload: RefreshTokenRequest,
    db: Session = Depends(get_db),
) -> Response:
    stored_token = db.scalar(
        select(RefreshToken).where(
            RefreshToken.token_hash == hash_refresh_token(payload.refresh_token)
        )
    )
    if stored_token is not None and stored_token.revoked_at is None:
        stored_token.revoked_at = datetime.now(timezone.utc)
        db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@router.get("/me", response_model=UserResponse, summary="Aktif oturumu getir")
def me(user: User = Depends(get_current_user)) -> User:
    return user

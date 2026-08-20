from dataclasses import dataclass
from pathlib import Path
from threading import RLock
from typing import Any

from app.config import get_settings

FIREBASE_AUTH_APP_NAME = "tarla-auth"
_firebase_auth_app_lock = RLock()


class FirebaseTokenError(ValueError):
    """A Firebase token is malformed, expired, revoked, or for another project."""


class FirebaseAuthUnavailableError(RuntimeError):
    """Firebase Admin cannot verify tokens because its service is unavailable."""


@dataclass(frozen=True)
class FirebaseIdentity:
    uid: str
    phone_number: str | None


def get_firebase_auth_app() -> object:
    """Return the Admin app dedicated to ID-token verification.

    This deliberately does not reuse the FCM app: either service can be
    configured and initialized independently.
    """

    try:
        import firebase_admin
        from firebase_admin import credentials
    except ImportError as exc:
        raise RuntimeError("firebase-admin paketi kurulu değil.") from exc

    with _firebase_auth_app_lock:
        try:
            return firebase_admin.get_app(FIREBASE_AUTH_APP_NAME)
        except ValueError:
            settings = get_settings()
            options = (
                {"projectId": settings.firebase_project_id}
                if settings.firebase_project_id
                else None
            )
            credential: Any
            try:
                if settings.firebase_service_account_path:
                    credential_path = Path(settings.firebase_service_account_path)
                    if not credential_path.is_file():
                        raise FirebaseAuthUnavailableError(
                            "Firebase servis hesabı dosyası bulunamadı."
                        )
                    credential = credentials.Certificate(str(credential_path))
                else:
                    credential = credentials.ApplicationDefault()
                return firebase_admin.initialize_app(
                    credential, options=options, name=FIREBASE_AUTH_APP_NAME
                )
            except ValueError:
                try:
                    return firebase_admin.get_app(FIREBASE_AUTH_APP_NAME)
                except ValueError as exc:
                    raise FirebaseAuthUnavailableError(
                        "Firebase kimlik doğrulama uygulaması başlatılamadı."
                    ) from exc


def verify_firebase_id_token(token: str) -> FirebaseIdentity:
    """Verify a Firebase ID token and return only identity claims we trust."""

    try:
        from firebase_admin import auth, exceptions
    except ImportError as exc:
        raise FirebaseAuthUnavailableError(
            "firebase-admin paketi kurulu değil."
        ) from exc

    app = get_firebase_auth_app()
    try:
        claims = auth.verify_id_token(token, app=app, check_revoked=True)
    except (
        auth.InvalidIdTokenError,
        auth.ExpiredIdTokenError,
        auth.RevokedIdTokenError,
        ValueError,
    ) as exc:
        raise FirebaseTokenError("Firebase tokenı geçersiz.") from exc
    except exceptions.FirebaseError as exc:
        raise FirebaseAuthUnavailableError(
            "Firebase token doğrulama hizmeti kullanılamıyor."
        ) from exc

    uid = claims.get("uid")
    if not isinstance(uid, str) or not uid:
        raise FirebaseTokenError("Firebase tokenında UID yok.")
    phone_number = claims.get("phone_number")
    if phone_number is not None and not isinstance(phone_number, str):
        raise FirebaseTokenError("Firebase tokenında geçersiz telefon numarası var.")
    return FirebaseIdentity(uid=uid, phone_number=phone_number)

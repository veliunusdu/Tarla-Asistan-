import pytest
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials
from fastapi.testclient import TestClient
from firebase_admin import auth
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import sessionmaker

from app.config import Settings, get_settings
from app.dependencies import get_current_user
from app.firebase_auth import (
    FIREBASE_AUTH_APP_NAME,
    FirebaseAuthUnavailableError,
    FirebaseIdentity,
)
from app.main import app
from app.models import User, UserRole
from app.security import create_access_token


def test_verified_firebase_identity_creates_farmer_and_maps_to_local_user(
    client: TestClient, monkeypatch
):
    """A verified UID must be persisted and must not trust token role claims."""

    def verify_id_token(token: str, *, app: object, check_revoked: bool):
        assert token == "firebase-token"
        assert app is not None
        assert check_revoked is True
        return {
            "uid": "firebase-uid-1",
            "phone_number": "+905551234567",
            "role": "AGRONOMIST",
        }

    monkeypatch.setattr("firebase_admin.auth.verify_id_token", verify_id_token)
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer firebase-token"}
    )

    assert response.status_code == 200
    assert response.json()["firebase_uid"] == "firebase-uid-1"
    assert response.json()["phone_number"] == "+905551234567"
    assert response.json()["role"] == "FARMER"


def test_invalid_firebase_token_is_unauthorized(
    client: TestClient, monkeypatch
):
    """Malformed Firebase tokens must never reach local user mapping."""

    def reject_token(token: str, *, app: object, check_revoked: bool):
        raise ValueError("invalid token")

    monkeypatch.setattr("firebase_admin.auth.verify_id_token", reject_token)
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer invalid"}
    )

    assert response.status_code == 401
    assert response.json()["detail"] == "Geçersiz veya süresi dolmuş oturum."


@pytest.mark.parametrize(
    "failure",
    [
        auth.InvalidIdTokenError("malformed"),
        auth.ExpiredIdTokenError("expired", ValueError("expired")),
        auth.RevokedIdTokenError("revoked"),
    ],
)
def test_firebase_token_rejection_families_are_generic_unauthorized(
    client: TestClient, monkeypatch, failure: Exception
):
    """Firebase's token failures must not disclose their cause to clients."""

    def reject_token(token: str, *, app: object, check_revoked: bool):
        raise failure

    monkeypatch.setattr("firebase_admin.auth.verify_id_token", reject_token)
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer rejected-token"}
    )

    assert response.status_code == 401
    assert response.json() == {"detail": "Geçersiz veya süresi dolmuş oturum."}


def test_firebase_verification_service_failure_is_not_reclassified_as_unauthorized(
    client: TestClient, monkeypatch
):
    """Certificate/backend outages are server failures, not invalid credentials."""

    def verification_backend_failure(token: str, *, app: object, check_revoked: bool):
        raise auth.CertificateFetchError("certificate endpoint unavailable", OSError())

    monkeypatch.setattr(
        "firebase_admin.auth.verify_id_token", verification_backend_failure
    )
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer valid-looking-token"}
    )

    assert response.status_code == 503
    assert response.json() == {"detail": "Kimlik doğrulama hizmeti kullanılamıyor."}


def test_firebase_configuration_failure_has_a_safe_server_response(
    client: TestClient, monkeypatch
):
    """Missing Admin credentials must be operationally visible, not a token 401."""

    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: (_ for _ in ()).throw(
            FirebaseAuthUnavailableError("missing credential path")
        ),
    )
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer valid-looking-token"}
    )

    assert response.status_code == 503
    assert response.json() == {"detail": "Kimlik doğrulama hizmeti kullanılamıyor."}


def test_first_firebase_access_without_verified_phone_is_unauthorized(
    client: TestClient, monkeypatch
):
    """A UID alone cannot create a local user with the current schema."""

    monkeypatch.setattr(
        "firebase_admin.auth.verify_id_token",
        lambda token, *, app, check_revoked: {"uid": "phone-less-uid"},
    )
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": "Bearer phone-less-token"}
    )

    assert response.status_code == 401
    assert response.json() == {"detail": "Geçersiz veya süresi dolmuş oturum."}


def test_disabled_firebase_auth_keeps_legacy_jwt_mapping(
    client: TestClient, db_session, monkeypatch
):
    """Disabling Firebase must keep the existing signed local JWT flow intact."""

    user = User(phone_number="+905551234571", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.commit()
    settings = Settings(firebase_auth_enabled=False)
    token = create_access_token(str(user.id), user.role.value, settings)
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: (_ for _ in ()).throw(AssertionError("Firebase was called")),
    )
    app.dependency_overrides[get_settings] = lambda: settings

    response = client.get(
        "/api/v1/auth/me", headers={"Authorization": f"Bearer {token}"}
    )

    assert response.status_code == 200
    assert response.json()["id"] == str(user.id)
    assert response.json()["role"] == "AGRONOMIST"


def test_firebase_uid_race_recovers_existing_user_from_an_independent_session(
    db_session, monkeypatch
):
    """The losing first-sign-in transaction must re-fetch the winning UID row."""

    identity = FirebaseIdentity("firebase-race-uid", "+905551234567")
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token", lambda token: identity
    )
    winning_session = sessionmaker(bind=db_session.bind, expire_on_commit=False)

    def lose_unique_uid_race():
        with winning_session() as winner:
            winner.add(
                User(
                    phone_number="+905551234567",
                    firebase_uid="firebase-race-uid",
                    role=UserRole.FARMER,
                )
            )
            winner.commit()
        raise IntegrityError("insert users", {}, ValueError("unique firebase uid"))

    monkeypatch.setattr(db_session, "commit", lose_unique_uid_race)

    user = get_current_user(
        HTTPAuthorizationCredentials(scheme="Bearer", credentials="firebase-token"),
        db_session,
        Settings(firebase_auth_enabled=True),
    )

    assert user.firebase_uid == "firebase-race-uid"
    assert user.role == UserRole.FARMER


def test_unlinked_legacy_phone_is_atomically_linked_to_verified_firebase_uid(
    db_session, monkeypatch
):
    """A verified phone must reclaim its existing local identity."""

    existing = User(phone_number="+905551234568", role=UserRole.FARMER)
    db_session.add(existing)
    db_session.commit()
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: FirebaseIdentity("new-firebase-uid", "+905551234568"),
    )

    user = get_current_user(
        HTTPAuthorizationCredentials(scheme="Bearer", credentials="firebase-token"),
        db_session,
        Settings(firebase_auth_enabled=True),
    )

    db_session.refresh(existing)
    assert user.id == existing.id
    assert existing.firebase_uid == "new-firebase-uid"
    assert existing.role == UserRole.FARMER


def test_phone_already_linked_to_same_uid_is_idempotent(db_session, monkeypatch):
    existing = User(
        phone_number="+905551234572",
        firebase_uid="same-firebase-uid",
        role=UserRole.FARMER,
    )
    db_session.add(existing)
    db_session.commit()
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: FirebaseIdentity("same-firebase-uid", "+905551234572"),
    )

    user = get_current_user(
        HTTPAuthorizationCredentials(scheme="Bearer", credentials="firebase-token"),
        db_session,
        Settings(firebase_auth_enabled=True),
    )

    assert user.id == existing.id
    assert user.firebase_uid == "same-firebase-uid"


def test_phone_linked_to_different_uid_remains_conflict(db_session, monkeypatch):
    existing = User(
        phone_number="+905551234573",
        firebase_uid="other-firebase-uid",
        role=UserRole.FARMER,
    )
    db_session.add(existing)
    db_session.commit()
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: FirebaseIdentity("new-firebase-uid", "+905551234573"),
    )

    with pytest.raises(HTTPException) as exc_info:
        get_current_user(
            HTTPAuthorizationCredentials(scheme="Bearer", credentials="firebase-token"),
            db_session,
            Settings(firebase_auth_enabled=True),
        )

    assert exc_info.value.status_code == 409
    db_session.refresh(existing)
    assert existing.firebase_uid == "other-firebase-uid"


def test_legacy_phone_link_race_returns_the_winning_same_uid(
    db_session, monkeypatch
):
    existing = User(phone_number="+905551234574", role=UserRole.FARMER)
    db_session.add(existing)
    db_session.commit()
    monkeypatch.setattr(
        "app.dependencies.verify_firebase_id_token",
        lambda token: FirebaseIdentity("race-firebase-uid", "+905551234574"),
    )
    competing_session = sessionmaker(bind=db_session.bind, expire_on_commit=False)
    original_execute = db_session.execute
    raced = False

    def execute_after_winner(statement, *args, **kwargs):
        nonlocal raced
        if not raced:
            raced = True
            with competing_session() as winner:
                winner_user = winner.scalar(
                    select(User).where(User.phone_number == "+905551234574")
                )
                winner_user.firebase_uid = "race-firebase-uid"
                winner.commit()
        return original_execute(statement, *args, **kwargs)

    monkeypatch.setattr(db_session, "execute", execute_after_winner)

    user = get_current_user(
        HTTPAuthorizationCredentials(scheme="Bearer", credentials="firebase-token"),
        db_session,
        Settings(firebase_auth_enabled=True),
    )

    assert user.id == existing.id
    assert user.firebase_uid == "race-firebase-uid"


def test_repeated_firebase_uid_reuses_original_local_identity(client: TestClient, monkeypatch):
    """Later claims must not replace the first mapped phone number or FARMER role."""

    claims = iter(
        [
            {"uid": "stable-uid", "phone_number": "+905551234569", "role": "FARMER"},
            {
                "uid": "stable-uid",
                "phone_number": "+905551234570",
                "role": "AGRONOMIST",
            },
        ]
    )

    def verify_id_token(token: str, *, app: object, check_revoked: bool):
        return next(claims)

    monkeypatch.setattr("firebase_admin.auth.verify_id_token", verify_id_token)
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    first = client.get("/api/v1/auth/me", headers={"Authorization": "Bearer one"})
    second = client.get("/api/v1/auth/me", headers={"Authorization": "Bearer two"})

    assert first.status_code == 200
    assert second.status_code == 200
    assert second.json()["firebase_uid"] == "stable-uid"
    assert second.json()["phone_number"] == "+905551234569"
    assert second.json()["role"] == "FARMER"


def test_named_auth_app_recovers_after_another_initializer_wins(monkeypatch):
    """A duplicate-name race must reuse the newly registered auth app."""

    from app.firebase_auth import get_firebase_auth_app

    known_app = object()
    calls = 0

    def get_app(name: str):
        nonlocal calls
        assert name == FIREBASE_AUTH_APP_NAME
        calls += 1
        if calls == 1:
            raise ValueError("not initialized")
        return known_app

    monkeypatch.setattr("firebase_admin.get_app", get_app)
    monkeypatch.setattr(
        "app.firebase_auth.get_settings",
        lambda: Settings(firebase_service_account_path=None),
    )
    monkeypatch.setattr(
        "firebase_admin.initialize_app",
        lambda credential, *, options, name: (_ for _ in ()).throw(
            ValueError("already exists")
        ),
    )

    try:
        resolved = get_firebase_auth_app()
    except ValueError:
        resolved = None

    assert resolved is known_app


def test_firebase_login_endpoint_creates_user_and_issues_session(
    client: TestClient, monkeypatch
):
    def verify_id_token(token: str, *, app: object, check_revoked: bool):
        assert token == "valid-firebase-token"
        return {
            "uid": "fb-uid-login-1",
            "phone_number": "+905559998877",
        }

    monkeypatch.setattr("firebase_admin.auth.verify_id_token", verify_id_token)
    monkeypatch.setattr("app.firebase_auth.get_firebase_auth_app", lambda: object())
    app.dependency_overrides[get_settings] = lambda: Settings(
        firebase_auth_enabled=True
    )

    response = client.post(
        "/api/v1/auth/firebase",
        json={"id_token": "valid-firebase-token"},
    )
    assert response.status_code == 200
    data = response.json()
    assert data["access_token"]
    assert data["refresh_token"]
    assert data["user"]["firebase_uid"] == "fb-uid-login-1"
    assert data["user"]["phone_number"] == "+905559998877"
    assert data["user"]["role"] == "FARMER"

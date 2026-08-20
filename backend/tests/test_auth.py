from fastapi.testclient import TestClient

from app.config import Settings
from app.models import AccountStatus, User
from app.security import create_refresh_token


def login(client: TestClient, phone: str = "+905551234567") -> dict:
    requested = client.post("/api/v1/auth/request-otp", json={"phone_number": phone})
    assert requested.status_code == 200
    code = requested.json()["debug_otp"]
    verified = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": code},
    )
    assert verified.status_code == 200
    return verified.json()


def test_valid_otp_is_single_use_and_returns_jwt(client: TestClient):
    phone = "+905551234567"
    requested = client.post("/api/v1/auth/request-otp", json={"phone_number": phone})
    code = requested.json()["debug_otp"]

    verified = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": code},
    )

    assert verified.status_code == 200
    assert verified.json()["token_type"] == "bearer"
    assert verified.json()["refresh_token"]
    assert verified.json()["user"]["role"] == "FARMER"
    replay = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": code},
    )
    assert replay.status_code == 400


def test_invalid_otp_and_request_rate_limit_are_rejected(client: TestClient):
    phone = "+905551234567"
    assert client.post("/api/v1/auth/request-otp", json={"phone_number": phone}).status_code == 200
    assert client.post("/api/v1/auth/request-otp", json={"phone_number": phone}).status_code == 429
    invalid = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": "000000"},
    )
    assert invalid.status_code == 400


def test_invalid_phone_and_missing_or_expired_otp_are_rejected(client: TestClient):
    invalid_phone = client.post(
        "/api/v1/auth/request-otp",
        json={"phone_number": "555123"},
    )
    assert invalid_phone.status_code == 422

    missing_or_expired = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": "+905551234567", "otp_code": "123456"},
    )
    assert missing_or_expired.status_code == 400


def test_attempt_limit_consumes_otp(client: TestClient):
    phone = "+905551234567"
    requested = client.post("/api/v1/auth/request-otp", json={"phone_number": phone})
    valid_code = requested.json()["debug_otp"]

    for _ in range(5):
        invalid = client.post(
            "/api/v1/auth/verify-otp",
            json={"phone_number": phone, "otp_code": "000000"},
        )
        assert invalid.status_code == 400

    rejected_valid_code = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": valid_code},
    )
    assert rejected_valid_code.status_code == 400


def test_refresh_rotates_token_and_logout_revokes_session(client: TestClient):
    auth = login(client)
    refreshed = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": auth["refresh_token"]},
    )
    assert refreshed.status_code == 200
    replacement = refreshed.json()["refresh_token"]
    assert replacement != auth["refresh_token"]

    replay = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": auth["refresh_token"]},
    )
    assert replay.status_code == 401

    logged_out = client.post(
        "/api/v1/auth/logout",
        json={"refresh_token": replacement},
    )
    assert logged_out.status_code == 204
    after_logout = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": replacement},
    )
    assert after_logout.status_code == 401


def test_profile_requires_terms_and_all_mandatory_fields(client: TestClient):
    auth = login(client)
    headers = {"Authorization": f"Bearer {auth['access_token']}"}
    rejected = client.put(
        "/api/v1/users/me",
        headers=headers,
        json={
            "full_name": "Veli Ünüşdü",
            "province": "Konya",
            "district": "Selçuklu",
            "terms_accepted": False,
        },
    )
    assert rejected.status_code == 422

    updated = client.put(
        "/api/v1/users/me",
        headers=headers,
        json={
            "full_name": "Veli Ünüşdü",
            "province": "Konya",
            "district": "Selçuklu",
            "terms_accepted": True,
            "notifications_enabled": True,
        },
    )
    assert updated.status_code == 200
    assert updated.json()["profile_complete"] is True


def test_pending_account_cannot_start_a_new_otp_session(client, db_session):
    phone = "+905551234568"
    user = User(phone_number=phone, account_status=AccountStatus.DELETION_PENDING)
    db_session.add(user)
    db_session.commit()
    requested = client.post("/api/v1/auth/request-otp", json={"phone_number": phone})

    verified = client.post(
        "/api/v1/auth/verify-otp",
        json={"phone_number": phone, "otp_code": requested.json()["debug_otp"]},
    )

    assert verified.status_code == 403


def test_pending_account_cannot_rotate_an_unrevoked_refresh_token(
    client, db_session
):
    user = User(
        phone_number="+905551234569",
        account_status=AccountStatus.DELETION_PENDING,
    )
    db_session.add(user)
    db_session.commit()
    raw_token, stored_token = create_refresh_token(user.id, Settings())
    db_session.add(stored_token)
    db_session.commit()

    response = client.post(
        "/api/v1/auth/refresh", json={"refresh_token": raw_token}
    )

    db_session.refresh(stored_token)
    assert response.status_code == 403
    assert stored_token.revoked_at is None

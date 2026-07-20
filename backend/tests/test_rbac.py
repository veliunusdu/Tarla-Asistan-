import uuid

from fastapi.testclient import TestClient
from sqlalchemy.orm import Session

from app.models import Farm
from tests.test_auth import login


def test_missing_token_is_unauthorized(client: TestClient):
    response = client.get("/api/v1/auth/me")
    assert response.status_code == 401


def test_farmer_cannot_access_agronomist_area(client: TestClient):
    auth = login(client, "+905551234567")
    response = client.get(
        "/api/v1/users/agronomist-area",
        headers={"Authorization": f"Bearer {auth['access_token']}"},
    )
    assert response.status_code == 403


def test_configured_agronomist_receives_agronomist_role(client: TestClient):
    auth = login(client, "+905551112233")
    assert auth["user"]["role"] == "AGRONOMIST"


def test_user_cannot_access_another_users_farm(
    client: TestClient,
    db_session: Session,
):
    owner = login(client, "+905551234567")
    other = login(client, "+905552345678")
    farm = Farm(
        owner_id=uuid.UUID(owner["user"]["id"]),
        name="Kuzey Tarlası",
    )
    db_session.add(farm)
    db_session.commit()

    owner_response = client.get(
        f"/api/v1/farms/{farm.id}",
        headers={"Authorization": f"Bearer {owner['access_token']}"},
    )
    assert owner_response.status_code == 200

    other_response = client.get(
        f"/api/v1/farms/{farm.id}",
        headers={"Authorization": f"Bearer {other['access_token']}"},
    )
    assert other_response.status_code == 404

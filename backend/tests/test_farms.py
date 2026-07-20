from datetime import date, timedelta

from fastapi.testclient import TestClient

from tests.test_auth import login


def auth_headers(client: TestClient, phone: str = "+905551234567") -> dict[str, str]:
    auth = login(client, phone)
    return {"Authorization": f"Bearer {auth['access_token']}"}


def farm_payload(name: str = "Kuzey Tarlası") -> dict:
    return {
        "name": name,
        "latitude": 38.7312,
        "longitude": 35.4787,
        "size_in_hectares": 12.5,
        "irrigation_method": "DRIP",
        "soil_type": "Killi tın",
        "note": "Kuzey girişini kullan.",
        "crop_type": "WHEAT",
        "variety": "Bezostaja",
        "planted_at": "2026-03-10",
    }


def create_farm(
    client: TestClient,
    headers: dict[str, str],
    *,
    name: str = "Kuzey Tarlası",
) -> dict:
    response = client.post(
        "/api/v1/farms",
        headers=headers,
        json=farm_payload(name),
    )
    assert response.status_code == 201
    return response.json()


def test_create_list_and_duplicate_name_warning(client: TestClient):
    headers = auth_headers(client)
    first = create_farm(client, headers)
    assert first["warnings"] == []
    assert first["farm"]["current_crop"]["crop_type"] == "WHEAT"

    duplicate = create_farm(client, headers, name="kuzey tarlası")
    assert duplicate["warnings"]

    listed = client.get("/api/v1/farms", headers=headers)
    assert listed.status_code == 200
    assert listed.json()["total"] == 2
    assert len(listed.json()["items"]) == 2


def test_farm_validation_update_ownership_and_archive(client: TestClient):
    owner_headers = auth_headers(client)
    created = create_farm(client, owner_headers)
    farm_id = created["farm"]["id"]

    invalid = farm_payload("Gelecek Tarlası")
    invalid["planted_at"] = (date.today() + timedelta(days=1)).isoformat()
    rejected = client.post("/api/v1/farms", headers=owner_headers, json=invalid)
    assert rejected.status_code == 422

    other_headers = auth_headers(client, "+905552345678")
    hidden = client.patch(
        f"/api/v1/farms/{farm_id}",
        headers=other_headers,
        json={"name": "Başkasının Tarlası"},
    )
    assert hidden.status_code == 404

    updated = client.patch(
        f"/api/v1/farms/{farm_id}",
        headers=owner_headers,
        json={"name": "Güney Tarlası", "size_in_hectares": 13.2},
    )
    assert updated.status_code == 200
    assert updated.json()["farm"]["name"] == "Güney Tarlası"

    archived = client.delete(f"/api/v1/farms/{farm_id}", headers=owner_headers)
    assert archived.status_code == 204
    assert client.get(f"/api/v1/farms/{farm_id}", headers=owner_headers).status_code == 404
    assert client.get("/api/v1/farms", headers=owner_headers).json()["total"] == 0
    archived_list = client.get(
        "/api/v1/farms?include_archived=true",
        headers=owner_headers,
    )
    assert archived_list.json()["total"] == 1
    assert archived_list.json()["items"][0]["archived_at"] is not None


def test_crop_period_requires_confirmation_and_preserves_history(client: TestClient):
    headers = auth_headers(client)
    created = create_farm(client, headers)
    farm_id = created["farm"]["id"]
    active_id = created["farm"]["current_crop"]["id"]

    conflict = client.post(
        f"/api/v1/farms/{farm_id}/production-periods",
        headers=headers,
        json={
            "crop_type": "BARLEY",
            "planted_at": "2026-04-01",
        },
    )
    assert conflict.status_code == 409

    replacement = client.post(
        f"/api/v1/farms/{farm_id}/production-periods",
        headers=headers,
        json={
            "crop_type": "BARLEY",
            "planted_at": "2026-04-01",
            "close_existing": True,
        },
    )
    assert replacement.status_code == 201
    assert replacement.json()["status"] == "ACTIVE"

    history = client.get(
        f"/api/v1/farms/{farm_id}/production-periods",
        headers=headers,
    )
    assert history.status_code == 200
    periods = history.json()["items"]
    assert len(periods) == 2
    old = next(period for period in periods if period["id"] == active_id)
    assert old["status"] == "ARCHIVED"
    assert old["harvested_at"] == "2026-04-01"

    close = client.post(
        (
            f"/api/v1/farms/{farm_id}/production-periods/"
            f"{replacement.json()['id']}/close"
        ),
        headers=headers,
        json={"harvested_at": "2026-06-01"},
    )
    assert close.status_code == 200
    assert close.json()["status"] == "ARCHIVED"

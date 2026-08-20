import uuid

from fastapi.testclient import TestClient

from tests.test_auth import login
from tests.test_farms import create_farm


def headers_for(client: TestClient, phone: str) -> dict[str, str]:
    auth = login(client, phone)
    return {"Authorization": f"Bearer {auth['access_token']}"}


def upload_image(client: TestClient, headers: dict[str, str]) -> dict:
    response = client.post(
        "/api/v1/media",
        headers=headers,
        files={"file": ("yaprak.jpg", b"fake-jpeg-content", "image/jpeg")},
    )
    assert response.status_code == 201
    return response.json()


def case_payload(farm_id: str, media_id: str) -> dict:
    return {
        "client_operation_id": str(uuid.uuid4()),
        "farm_id": farm_id,
        "category": "DISEASE",
        "title": "Yapraklarda sararma",
        "description": "Alt yapraklarda hızla yayılan sarı lekeler var.",
        "media_ids": [media_id],
    }


def test_secure_media_validation_and_access(client: TestClient):
    owner_headers = headers_for(client, "+905551234567")
    media = upload_image(client, owner_headers)
    assert media["kind"] == "IMAGE"
    assert len(media["checksum_sha256"]) == 64

    owner_content = client.get(media["url"], headers=owner_headers)
    assert owner_content.status_code == 200
    assert owner_content.content == b"fake-jpeg-content"

    other_headers = headers_for(client, "+905552345678")
    assert client.get(media["url"], headers=other_headers).status_code == 404

    rejected = client.post(
        "/api/v1/media",
        headers=owner_headers,
        files={"file": ("zararli.exe", b"content", "application/octet-stream")},
    )
    assert rejected.status_code == 415


def test_case_idempotency_visibility_messages_and_close_flow(client: TestClient):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    media = upload_image(client, farmer_headers)
    payload = case_payload(farm["id"], media["id"])

    created = client.post("/api/v1/cases", headers=farmer_headers, json=payload)
    assert created.status_code == 201
    case = created.json()
    assert case["status"] == "OPEN"
    assert case["farm_name"] == farm["name"]
    assert case["media"][0]["id"] == media["id"]

    replayed = client.post("/api/v1/cases", headers=farmer_headers, json=payload)
    assert replayed.status_code == 201
    assert replayed.json()["id"] == case["id"]
    assert client.get("/api/v1/cases", headers=farmer_headers).json()["total"] == 1

    conflicting = dict(payload, title="Başka bir vaka")
    assert client.post("/api/v1/cases", headers=farmer_headers, json=conflicting).status_code == 409

    other_headers = headers_for(client, "+905552345678")
    assert client.get(f"/api/v1/cases/{case['id']}", headers=other_headers).status_code == 404

    expert_headers = headers_for(client, "+905551112233")
    expert_list = client.get("/api/v1/cases", headers=expert_headers)
    assert expert_list.status_code == 200
    assert expert_list.json()["total"] == 1
    assert client.get(media["url"], headers=expert_headers).status_code == 200

    reviewed = client.patch(
        f"/api/v1/cases/{case['id']}/status",
        headers=expert_headers,
        json={"status": "IN_REVIEW", "priority": "CRITICAL", "assign_to_me": True},
    )
    assert reviewed.status_code == 200
    assert reviewed.json()["priority"] == "CRITICAL"
    assert reviewed.json()["assigned_expert_id"] is not None

    request_info = client.post(
        f"/api/v1/cases/{case['id']}/messages",
        headers=expert_headers,
        json={
            "client_operation_id": str(uuid.uuid4()),
            "message_type": "ADDITIONAL_INFO_REQUEST",
            "body": "Yaprağın alt yüzünün de fotoğrafını ekler misiniz?",
        },
    )
    assert request_info.status_code == 201
    assert client.get(f"/api/v1/cases/{case['id']}", headers=farmer_headers).json()["status"] == "WAITING_FARMER"

    farmer_reply = client.post(
        f"/api/v1/cases/{case['id']}/messages",
        headers=farmer_headers,
        json={
            "client_operation_id": str(uuid.uuid4()),
            "message_type": "COMMENT",
            "body": "Yeni fotoğrafı kontrol ettim, lekeler alt yüzde de var.",
        },
    )
    assert farmer_reply.status_code == 201
    assert client.get(f"/api/v1/cases/{case['id']}", headers=expert_headers).json()["status"] == "IN_REVIEW"

    operation_id = str(uuid.uuid4())
    expert_payload = {
        "client_operation_id": operation_id,
        "body": "Saha kontrolü öneriyorum; bu süre içinde yaprakları kuru tutun.",
        "close_case": True,
    }
    answered = client.post(
        f"/api/v1/cases/{case['id']}/expert-response",
        headers=expert_headers,
        json=expert_payload,
    )
    assert answered.status_code == 201
    assert answered.json()["status"] == "CLOSED"
    assert answered.json()["closed_at"] is not None
    assert len(answered.json()["messages"]) == 3

    replayed_answer = client.post(
        f"/api/v1/cases/{case['id']}/expert-response",
        headers=expert_headers,
        json=expert_payload,
    )
    assert replayed_answer.status_code == 201
    assert len(replayed_answer.json()["messages"]) == 3


def test_activity_client_operation_is_idempotent(client: TestClient):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    payload = {
        "client_operation_id": str(uuid.uuid4()),
        "activity_type": "FIELD_CHECK",
        "description": "Çevrimdışı saha kontrolü",
        "occurred_at": "2026-07-20T10:00:00Z",
    }
    first = client.post(
        f"/api/v1/farms/{farm['id']}/activities",
        headers=farmer_headers,
        json=payload,
    )
    second = client.post(
        f"/api/v1/farms/{farm['id']}/activities",
        headers=farmer_headers,
        json=payload,
    )
    assert first.status_code == 201
    assert second.status_code == 201
    assert first.json()["id"] == second.json()["id"]
    assert client.get(
        f"/api/v1/farms/{farm['id']}/activities",
        headers=farmer_headers,
    ).json()["total"] == 1

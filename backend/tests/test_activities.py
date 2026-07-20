from datetime import datetime, timedelta, timezone

from fastapi.testclient import TestClient

from tests.test_farms import auth_headers, create_farm


def activity_payload() -> dict:
    return {
        "activity_type": "IRRIGATION",
        "description": "Damla sulama hattı iki saat çalıştırıldı.",
        "occurred_at": datetime.now(timezone.utc).isoformat(),
        "duration_minutes": 120,
        "amount": 15,
        "unit": "m3",
        "performed_by": "Veli",
        "cost": 450,
    }


def test_manual_activity_validation_listing_and_journal(client: TestClient):
    headers = auth_headers(client)
    farm_id = create_farm(client, headers)["farm"]["id"]

    future_payload = activity_payload()
    future_payload["occurred_at"] = (
        datetime.now(timezone.utc) + timedelta(days=1)
    ).isoformat()
    rejected = client.post(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
        json=future_payload,
    )
    assert rejected.status_code == 422

    created = client.post(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
        json=activity_payload(),
    )
    assert created.status_code == 201
    assert created.json()["status"] == "CONFIRMED"
    assert created.json()["source"] == "MANUAL"

    listed = client.get(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
    )
    assert listed.status_code == 200
    assert listed.json()["total"] == 1

    journal = client.get(
        f"/api/v1/farms/{farm_id}/journal",
        headers=headers,
    )
    assert journal.status_code == 200
    assert journal.json()["items"][0]["entry_type"] == "ACTIVITY"


def test_voice_activity_stays_draft_until_farmer_confirms(client: TestClient):
    headers = auth_headers(client)
    farm_id = create_farm(client, headers)["farm"]["id"]
    payload = activity_payload()
    payload.update(
        {
            "input_method": "VOICE",
            "voice_url": "https://media.example.test/voice/recording.m4a",
            "voice_transcript": "Tarlayı iki saat suladım.",
        }
    )

    created = client.post(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
        json=payload,
    )
    assert created.status_code == 201
    assert created.json()["status"] == "DRAFT"

    journal_before = client.get(
        f"/api/v1/farms/{farm_id}/journal",
        headers=headers,
    )
    assert journal_before.json()["total"] == 0

    confirmed = client.post(
        f"/api/v1/activities/{created.json()['id']}/confirm",
        headers=headers,
    )
    assert confirmed.status_code == 200
    assert confirmed.json()["status"] == "CONFIRMED"
    assert confirmed.json()["confirmed_at"] is not None

    journal_after = client.get(
        f"/api/v1/farms/{farm_id}/journal",
        headers=headers,
    )
    assert journal_after.json()["total"] == 1


def test_activity_edit_history_archive_and_restore(client: TestClient):
    headers = auth_headers(client)
    farm_id = create_farm(client, headers)["farm"]["id"]
    created = client.post(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
        json=activity_payload(),
    ).json()

    updated = client.patch(
        f"/api/v1/activities/{created['id']}",
        headers=headers,
        json={"description": "Sulama hattı 90 dakika çalıştırıldı."},
    )
    assert updated.status_code == 200
    rejected_null = client.patch(
        f"/api/v1/activities/{created['id']}",
        headers=headers,
        json={"description": None},
    )
    assert rejected_null.status_code == 422

    revisions = client.get(
        f"/api/v1/activities/{created['id']}/revisions",
        headers=headers,
    )
    assert revisions.status_code == 200
    assert revisions.json()[0]["previous_values"]["description"] == (
        "Damla sulama hattı iki saat çalıştırıldı."
    )

    archived = client.delete(
        f"/api/v1/activities/{created['id']}",
        headers=headers,
    )
    assert archived.status_code == 204
    active_list = client.get(
        f"/api/v1/farms/{farm_id}/activities",
        headers=headers,
    )
    assert active_list.json()["total"] == 0
    archived_list = client.get(
        f"/api/v1/farms/{farm_id}/activities?include_archived=true",
        headers=headers,
    )
    assert archived_list.json()["total"] == 1

    restored = client.post(
        f"/api/v1/activities/{created['id']}/restore",
        headers=headers,
    )
    assert restored.status_code == 200
    assert restored.json()["archived_at"] is None

import uuid
from datetime import date, datetime, timedelta, timezone

from fastapi.testclient import TestClient
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.models import Activity, Task, WeatherSnapshot
from app.weather import WeatherPoint, serialize_weather_points
from tests.test_farms import auth_headers, create_farm


def expert_task_payload(title: str = "Uzman saha kontrolü") -> dict:
    return {
        "title": title,
        "description": "Yaprakları ve toprak nemini yerinde kontrol edin.",
        "reason": "Uzman tarafından saha takibi için oluşturuldu.",
        "priority": "HIGH",
        "confidence": "HIGH",
        "due_date": date.today().isoformat(),
    }


def test_only_expert_creates_task_and_duplicate_is_rejected(client: TestClient):
    owner_headers = auth_headers(client)
    farm_id = create_farm(client, owner_headers)["farm"]["id"]

    forbidden = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=owner_headers,
        json=expert_task_payload(),
    )
    assert forbidden.status_code == 403

    expert_headers = auth_headers(client, "+905551112233")
    created = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    )
    assert created.status_code == 201
    assert created.json()["source"] == "EXPERT"
    assert created.json()["reason"]

    duplicate = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    )
    assert duplicate.status_code == 409


def test_daily_tasks_are_deduplicated_limited_and_keep_critical_alerts_separate(
    client: TestClient,
    db_session: Session,
):
    owner_headers = auth_headers(client)
    farm_id = create_farm(client, owner_headers)["farm"]["id"]
    expert_headers = auth_headers(client, "+905551112233")
    for number in range(4):
        response = client.post(
            f"/api/v1/farms/{farm_id}/tasks",
            headers=expert_headers,
            json=expert_task_payload(f"Uzman görevi {number}"),
        )
        assert response.status_code == 201

    now = datetime.now(timezone.utc)
    db_session.add(
        WeatherSnapshot(
            farm_id=uuid.UUID(farm_id),
            provider="test_weather",
            payload=serialize_weather_points(
                [
                    WeatherPoint(
                        observed_at=now + timedelta(hours=1),
                        temperature_c=-2,
                        precipitation_probability=20,
                        precipitation_mm=0,
                        wind_speed_kmh=10,
                    )
                ]
            ),
            fetched_at=now,
        )
    )
    db_session.commit()

    first = client.get(f"/api/v1/farms/{farm_id}/tasks", headers=owner_headers)
    assert first.status_code == 200
    assert len(first.json()["items"]) == 3
    assert all(item["source"] == "EXPERT" for item in first.json()["items"])
    assert len(first.json()["critical_weather_alerts"]) == 1
    assert first.json()["critical_weather_alerts"][0]["priority"] == "CRITICAL"
    task_count = db_session.scalar(select(func.count(Task.id)))

    second = client.get(f"/api/v1/farms/{farm_id}/tasks", headers=owner_headers)
    assert second.status_code == 200
    assert db_session.scalar(select(func.count(Task.id))) == task_count


def test_task_completion_is_idempotent_and_appears_in_journal(
    client: TestClient,
    db_session: Session,
):
    owner_headers = auth_headers(client)
    farm_id = create_farm(client, owner_headers)["farm"]["id"]
    expert_headers = auth_headers(client, "+905551112233")
    task = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    ).json()

    completed = client.post(
        f"/api/v1/tasks/{task['id']}/complete",
        headers=owner_headers,
        json={"note": "Kontrol edildi, sorun görülmedi."},
    )
    assert completed.status_code == 200
    assert completed.json()["status"] == "COMPLETED"

    repeated = client.post(
        f"/api/v1/tasks/{task['id']}/complete",
        headers=owner_headers,
        json={"note": "İkinci istek"},
    )
    assert repeated.status_code == 200
    assert (
        db_session.scalar(
            select(func.count(Activity.id)).where(
                Activity.task_id == uuid.UUID(task["id"])
            )
        )
        == 1
    )

    journal = client.get(
        f"/api/v1/farms/{farm_id}/journal",
        headers=owner_headers,
    )
    assert journal.status_code == 200
    assert journal.json()["total"] == 1
    assert journal.json()["items"][0]["entry_type"] == "TASK"
    assert journal.json()["items"][0]["metadata"]["status"] == "COMPLETED"


def test_not_applied_requires_reason_and_other_owner_cannot_access(
    client: TestClient,
):
    owner_headers = auth_headers(client)
    farm_id = create_farm(client, owner_headers)["farm"]["id"]
    expert_headers = auth_headers(client, "+905551112233")
    task_id = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    ).json()["id"]

    missing_reason = client.patch(
        f"/api/v1/tasks/{task_id}/status",
        headers=owner_headers,
        json={"status": "NOT_APPLIED"},
    )
    assert missing_reason.status_code == 422

    other_headers = auth_headers(client, "+905552345678")
    hidden = client.get(f"/api/v1/tasks/{task_id}", headers=other_headers)
    assert hidden.status_code == 404

    updated = client.patch(
        f"/api/v1/tasks/{task_id}/status",
        headers=owner_headers,
        json={
            "status": "NOT_APPLIED",
            "not_applied_reason": "Tarlaya erişim sağlanamadı.",
        },
    )
    assert updated.status_code == 200
    assert updated.json()["not_applied_reason"] == "Tarlaya erişim sağlanamadı."


def test_archived_farm_tasks_are_not_accessible(client: TestClient):
    owner_headers = auth_headers(client)
    farm_id = create_farm(client, owner_headers)["farm"]["id"]
    expert_headers = auth_headers(client, "+905551112233")
    task_id = client.post(
        f"/api/v1/farms/{farm_id}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    ).json()["id"]

    archived = client.delete(f"/api/v1/farms/{farm_id}", headers=owner_headers)
    assert archived.status_code == 204
    hidden = client.get(f"/api/v1/tasks/{task_id}", headers=owner_headers)
    assert hidden.status_code == 404

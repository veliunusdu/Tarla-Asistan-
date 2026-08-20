import uuid
from datetime import date, datetime, timedelta, timezone

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import Settings
from app.models import Notification, NotificationStatus, WeatherSnapshot
from app.push import FirebasePushProvider, PushProviderError, create_push_provider
from app.weather import WeatherPoint, serialize_weather_points
from tests.conftest import MemoryPushProvider
from tests.test_cases import case_payload, headers_for, upload_image
from tests.test_farms import create_farm
from tests.test_tasks import expert_task_payload


def register_device(client: TestClient, headers: dict[str, str]) -> dict:
    response = client.post(
        "/api/v1/notifications/devices",
        headers=headers,
        json={"token": f"device-token-{uuid.uuid4()}", "platform": "ANDROID"},
    )
    assert response.status_code == 201
    return response.json()


def test_task_notification_is_delivered_listed_and_marked_read(
    client: TestClient,
    push_provider: MemoryPushProvider,
):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    device = register_device(client, farmer_headers)
    expert_headers = headers_for(client, "+905551112233")

    created = client.post(
        f"/api/v1/farms/{farm['id']}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    )
    assert created.status_code == 201
    assert len(push_provider.sent) == 1
    assert push_provider.sent[0]["data"]["deep_link"].startswith(
        "tarla-asistani://farms/"
    )

    inbox = client.get("/api/v1/notifications", headers=farmer_headers)
    assert inbox.status_code == 200
    assert inbox.json()["unread"] == 1
    notification = inbox.json()["items"][0]
    assert notification["notification_type"] == "TASK_ASSIGNED"
    assert notification["status"] == "SENT"

    read = client.post(
        f"/api/v1/notifications/{notification['id']}/read",
        headers=farmer_headers,
    )
    assert read.status_code == 200
    assert read.json()["read_at"] is not None

    other_headers = headers_for(client, "+905552345678")
    assert (
        client.post(
            f"/api/v1/notifications/{notification['id']}/read",
            headers=other_headers,
        ).status_code
        == 404
    )
    assert (
        client.delete(
            f"/api/v1/notifications/devices/{device['id']}",
            headers=farmer_headers,
        ).status_code
        == 204
    )


def test_pending_notification_is_sent_when_device_registers(
    client: TestClient,
    db_session: Session,
    push_provider: MemoryPushProvider,
):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    expert_headers = headers_for(client, "+905551112233")
    created = client.post(
        f"/api/v1/farms/{farm['id']}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    )
    assert created.status_code == 201
    assert not push_provider.sent
    notification = db_session.scalar(select(Notification))
    assert notification.status == NotificationStatus.PENDING

    register_device(client, farmer_headers)
    db_session.refresh(notification)
    assert notification.status == NotificationStatus.SENT
    assert len(push_provider.sent) == 1


def test_critical_weather_and_expert_response_notifications_are_deduplicated(
    client: TestClient,
    db_session: Session,
    push_provider: MemoryPushProvider,
):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    register_device(client, farmer_headers)

    now = datetime.now(timezone.utc)
    db_session.add(
        WeatherSnapshot(
            farm_id=uuid.UUID(farm["id"]),
            provider="test_weather",
            payload=serialize_weather_points(
                [
                    WeatherPoint(
                        observed_at=now + timedelta(hours=1),
                        temperature_c=-3,
                        precipitation_probability=10,
                        precipitation_mm=0,
                        wind_speed_kmh=8,
                    )
                ]
            ),
            fetched_at=now,
        )
    )
    db_session.commit()
    first = client.get(
        f"/api/v1/farms/{farm['id']}/tasks?date={date.today().isoformat()}",
        headers=farmer_headers,
    )
    second = client.get(
        f"/api/v1/farms/{farm['id']}/tasks?date={date.today().isoformat()}",
        headers=farmer_headers,
    )
    assert first.status_code == second.status_code == 200
    assert len(first.json()["critical_weather_alerts"]) == 1

    media = upload_image(client, farmer_headers)
    case = client.post(
        "/api/v1/cases",
        headers=farmer_headers,
        json=case_payload(farm["id"], media["id"]),
    ).json()
    expert_headers = headers_for(client, "+905551112233")
    operation_id = str(uuid.uuid4())
    response_payload = {
        "client_operation_id": operation_id,
        "body": "Don riski sonrası bitkileri gün doğumunda kontrol edin.",
        "close_case": False,
    }
    answered = client.post(
        f"/api/v1/cases/{case['id']}/expert-response",
        headers=expert_headers,
        json=response_payload,
    )
    replayed = client.post(
        f"/api/v1/cases/{case['id']}/expert-response",
        headers=expert_headers,
        json=response_payload,
    )
    assert answered.status_code == replayed.status_code == 201
    inbox = client.get("/api/v1/notifications", headers=farmer_headers).json()
    types = [item["notification_type"] for item in inbox["items"]]
    assert types.count("CRITICAL_WEATHER") == 1
    assert types.count("EXPERT_RESPONSE") == 1
    assert len(push_provider.sent) == 2


def test_pilot_feedback_authorization_validation_and_metrics(client: TestClient):
    farmer_headers = headers_for(client, "+905551234567")
    farm = create_farm(client, farmer_headers)["farm"]
    expert_headers = headers_for(client, "+905551112233")
    task = client.post(
        f"/api/v1/farms/{farm['id']}/tasks",
        headers=expert_headers,
        json=expert_task_payload(),
    ).json()

    missing_rating = client.post(
        "/api/v1/pilot/feedback",
        headers=farmer_headers,
        json={
            "feedback_type": "WEEKLY_CHECKIN",
            "comment": "Bu hafta uygulamayı düzenli kullandım.",
        },
    )
    assert missing_rating.status_code == 422
    false_alert_for_expert_task = client.post(
        "/api/v1/pilot/feedback",
        headers=farmer_headers,
        json={
            "feedback_type": "FALSE_ALERT",
            "comment": "Bu görev hava uyarısı değil.",
            "related_task_id": task["id"],
        },
    )
    assert false_alert_for_expert_task.status_code == 422

    created = client.post(
        "/api/v1/pilot/feedback",
        headers=farmer_headers,
        json={
            "feedback_type": "WEEKLY_CHECKIN",
            "rating": 4,
            "comment": "Görevler anlaşılır ve zamanında geldi.",
        },
    )
    assert created.status_code == 201
    feedback_id = created.json()["id"]
    assert (
        client.get("/api/v1/pilot/feedback", headers=farmer_headers).status_code == 403
    )
    listed = client.get("/api/v1/pilot/feedback", headers=expert_headers)
    assert listed.status_code == 200
    assert listed.json()["total"] == 1

    reviewed = client.patch(
        f"/api/v1/pilot/feedback/{feedback_id}",
        headers=expert_headers,
        json={"status": "REVIEWED"},
    )
    assert reviewed.status_code == 200
    assert reviewed.json()["reviewed_by_id"] is not None

    assert (
        client.get("/api/v1/pilot/metrics", headers=farmer_headers).status_code == 403
    )
    metrics = client.get("/api/v1/pilot/metrics", headers=expert_headers)
    assert metrics.status_code == 200
    assert metrics.json()["feedback_count"] == 1
    assert metrics.json()["average_feedback_rating"] == 4.0
    assert metrics.json()["active_farmers"] == 1


def test_observability_endpoints_and_security_headers(client: TestClient):
    response = client.get("/health/live", headers={"X-Request-ID": "pilot-check"})
    assert response.status_code == 200
    assert response.headers["X-Request-ID"] == "pilot-check"
    assert response.headers["X-Content-Type-Options"] == "nosniff"
    metrics = client.get("/metrics")
    assert metrics.status_code == 200
    assert "http_requests_total" in metrics.text


def test_push_provider_rejects_unknown_or_insecure_gateway():
    with pytest.raises(ValueError, match="noop"):
        create_push_provider(Settings(push_provider="unknown"))
    with pytest.raises(ValueError, match="HTTPS"):
        create_push_provider(
            Settings(
                push_provider="http",
                push_gateway_url="http://push.example.test/send",
                push_gateway_token="secret",
            )
        )


def test_firebase_push_provider_sends_notification_and_data():
    sent: list[dict[str, object]] = []

    class FakeMessaging:
        @staticmethod
        def Message(**kwargs):
            return kwargs

        @staticmethod
        def Notification(**kwargs):
            return kwargs

        @staticmethod
        def send(message, app):
            sent.append({"message": message, "app": app})
            return "projects/tarla/messages/42"

    provider = FirebasePushProvider(app=object(), messaging_module=FakeMessaging)

    message_id = provider.send(
        device_token="fcm-token",
        title="Yeni görev",
        body="Sulama kontrolü",
        data={"task_id": "task-1"},
    )

    assert message_id == "projects/tarla/messages/42"
    assert sent[0]["message"] == {
        "token": "fcm-token",
        "notification": {"title": "Yeni görev", "body": "Sulama kontrolü"},
        "data": {"task_id": "task-1"},
    }


def test_firebase_push_provider_wraps_send_failures():
    class FailingMessaging:
        @staticmethod
        def Message(**kwargs):
            return kwargs

        @staticmethod
        def Notification(**kwargs):
            return kwargs

        @staticmethod
        def send(message, app):
            raise RuntimeError("unregistered token")

    provider = FirebasePushProvider(app=object(), messaging_module=FailingMessaging)

    with pytest.raises(PushProviderError, match="FCM"):
        provider.send(
            device_token="expired-token",
            title="Başlık",
            body="İçerik",
            data={},
        )

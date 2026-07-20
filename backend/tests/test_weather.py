from datetime import datetime, timedelta, timezone

from fastapi.testclient import TestClient

from app.dependencies import get_weather_provider
from app.main import app
from app.weather import WeatherPoint, WeatherProviderError
from tests.test_farms import auth_headers, create_farm


class SuccessfulProvider:
    name = "test_weather"

    async def forecast(self, latitude: float, longitude: float) -> list[WeatherPoint]:
        now = datetime.now(timezone.utc)
        return [
            WeatherPoint(
                observed_at=now + timedelta(hours=1),
                temperature_c=-1,
                precipitation_probability=80,
                precipitation_mm=6,
                wind_speed_kmh=35,
            )
        ]


class FailingProvider:
    name = "failing_weather"

    async def forecast(self, latitude: float, longitude: float) -> list[WeatherPoint]:
        raise WeatherProviderError("test failure")


def test_weather_is_persisted_and_provider_failure_returns_stale_data(
    client: TestClient,
):
    headers = auth_headers(client)
    farm_id = create_farm(client, headers)["farm"]["id"]

    app.dependency_overrides[get_weather_provider] = lambda: SuccessfulProvider()
    current = client.get(f"/api/v1/farms/{farm_id}/weather", headers=headers)
    assert current.status_code == 200
    assert current.json()["is_stale"] is False
    assert {risk["risk_type"] for risk in current.json()["risks"]} == {
        "FROST",
        "STRONG_WIND",
        "HEAVY_RAIN",
    }

    app.dependency_overrides[get_weather_provider] = lambda: FailingProvider()
    fallback = client.get(f"/api/v1/farms/{farm_id}/weather", headers=headers)
    assert fallback.status_code == 200
    assert fallback.json()["is_stale"] is True
    assert "son başarılı" in fallback.json()["stale_reason"].lower()
    assert fallback.json()["provider"] == "test_weather"


def test_weather_failure_without_snapshot_is_service_unavailable(client: TestClient):
    headers = auth_headers(client)
    farm_id = create_farm(client, headers)["farm"]["id"]
    app.dependency_overrides[get_weather_provider] = lambda: FailingProvider()

    response = client.get(f"/api/v1/farms/{farm_id}/weather", headers=headers)
    assert response.status_code == 503
    assert "saha koşullarını" in response.json()["detail"].lower()

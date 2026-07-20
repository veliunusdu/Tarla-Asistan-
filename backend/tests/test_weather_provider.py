import asyncio

import pytest
from app.weather import OpenMeteoWeatherProvider, WeatherProviderError


class FakeResponse:
    def __init__(self, payload: dict, *, fail: bool = False) -> None:
        self.payload = payload
        self.fail = fail

    def raise_for_status(self) -> None:
        if self.fail:
            raise WeatherProviderError("request failed")

    def json(self) -> dict:
        return self.payload


class FakeAsyncClient:
    def __init__(self, response: FakeResponse) -> None:
        self.response = response
        self.request_params: dict | None = None

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, traceback):
        return None

    async def get(self, url: str, params: dict) -> FakeResponse:
        self.request_params = params
        return self.response


def test_open_meteo_adapter_normalizes_hourly_response(monkeypatch):
    response = FakeResponse(
        {
            "hourly": {
                "time": ["2026-07-20T10:00"],
                "temperature_2m": [18.5],
                "precipitation_probability": [75],
                "precipitation": [5.2],
                "wind_speed_10m": [31],
            }
        }
    )
    client = FakeAsyncClient(response)
    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: client,
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    points = asyncio.run(provider.forecast(38.7, 35.4))

    assert len(points) == 1
    assert points[0].temperature_c == 18.5
    assert points[0].observed_at.tzinfo is not None
    assert client.request_params["timezone"] == "UTC"


def test_open_meteo_adapter_rejects_malformed_response(monkeypatch):
    client = FakeAsyncClient(FakeResponse({"hourly": {"time": []}}))
    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: client,
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    with pytest.raises(WeatherProviderError):
        asyncio.run(provider.forecast(38.7, 35.4))

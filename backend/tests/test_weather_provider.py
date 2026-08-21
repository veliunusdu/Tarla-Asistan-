import asyncio
import httpx
import pytest

from app.weather import OpenMeteoWeatherProvider, WeatherProviderError


class FakeResponse:
    def __init__(
        self,
        payload: dict,
        *,
        status_code: int = 200,
        fail: bool = False,
    ) -> None:
        self.payload = payload
        self.status_code = status_code
        self.fail = fail

    def raise_for_status(self) -> None:
        if self.fail or self.status_code >= 400:
            request = httpx.Request("GET", "https://weather.test/forecast")
            response = httpx.Response(self.status_code, request=request)
            raise httpx.HTTPStatusError(
                f"HTTP {self.status_code}",
                request=request,
                response=response,
            )

    def json(self) -> dict:
        return self.payload


class FakeAsyncClient:
    def __init__(
        self,
        response: FakeResponse | None = None,
        *,
        timeout_error: bool = False,
    ) -> None:
        self.response = response
        self.timeout_error = timeout_error
        self.request_params: dict | None = None

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, traceback):
        return None

    async def get(self, url: str, params: dict) -> FakeResponse:
        self.request_params = params
        if self.timeout_error:
            raise httpx.ReadTimeout("Connection timed out")
        assert self.response is not None
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
    captured_timeout = []

    def make_client(timeout):
        captured_timeout.append(timeout)
        return FakeAsyncClient(response)

    monkeypatch.setattr("app.weather.httpx.AsyncClient", make_client)
    provider = OpenMeteoWeatherProvider(
        "https://weather.test/forecast", timeout_seconds=6.5
    )

    points = asyncio.run(provider.forecast(38.7, 35.4))

    assert len(points) == 1
    assert points[0].temperature_c == 18.5
    assert points[0].observed_at.tzinfo is not None
    assert captured_timeout == [6.5]


def test_open_meteo_adapter_rejects_malformed_response(monkeypatch):
    client = FakeAsyncClient(FakeResponse({"hourly": {"time": []}}))
    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: client,
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    with pytest.raises(WeatherProviderError):
        asyncio.run(provider.forecast(38.7, 35.4))


def test_open_meteo_adapter_timeout_raises_weather_provider_error(monkeypatch):
    client = FakeAsyncClient(timeout_error=True)
    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: client,
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    with pytest.raises(WeatherProviderError, match="geçerli veri alınamadı"):
        asyncio.run(provider.forecast(38.7, 35.4))


def test_open_meteo_adapter_500_error_raises_weather_provider_error(monkeypatch):
    client = FakeAsyncClient(FakeResponse({}, status_code=500))
    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: client,
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    with pytest.raises(WeatherProviderError, match="geçerli veri alınamadı"):
        asyncio.run(provider.forecast(38.7, 35.4))


def test_open_meteo_adapter_rejects_invalid_coordinates():
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")

    with pytest.raises(WeatherProviderError, match="Geçersiz koordinat"):
        asyncio.run(provider.forecast(95.0, 35.4))

    with pytest.raises(WeatherProviderError, match="Geçersiz koordinat"):
        asyncio.run(provider.forecast(-95.0, 35.4))

    with pytest.raises(WeatherProviderError, match="Geçersiz koordinat"):
        asyncio.run(provider.forecast(38.7, 190.0))


def test_open_meteo_adapter_does_not_send_api_key(monkeypatch):
    response = FakeResponse(
        {
            "hourly": {
                "time": ["2026-07-20T10:00"],
                "temperature_2m": [22.0],
                "precipitation_probability": [0],
                "precipitation": [0.0],
                "wind_speed_10m": [10.0],
            }
        }
    )
    captured_params = {}

    class ParamCapturingClient:
        async def __aenter__(self):
            return self

        async def __aexit__(self, *args):
            return None

        async def get(self, url: str, params: dict) -> FakeResponse:
            captured_params.update(params)
            return response

    monkeypatch.setattr(
        "app.weather.httpx.AsyncClient",
        lambda timeout: ParamCapturingClient(),
    )
    provider = OpenMeteoWeatherProvider("https://weather.test/forecast")
    asyncio.run(provider.forecast(38.7, 35.4))

    for sensitive_key in ("api_key", "apikey", "key", "token", "secret", "appid"):
        assert sensitive_key not in captured_params
        assert sensitive_key.upper() not in captured_params

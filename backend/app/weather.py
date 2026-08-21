from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from typing import Protocol

import httpx

from app.config import Settings


class WeatherProviderError(RuntimeError):
    """A weather provider failed or returned an unusable response."""


@dataclass(frozen=True)
class WeatherPoint:
    observed_at: datetime
    temperature_c: float | None
    precipitation_probability: float | None
    precipitation_mm: float | None
    wind_speed_kmh: float | None


class WeatherProvider(Protocol):
    name: str

    async def forecast(
        self,
        latitude: float,
        longitude: float,
    ) -> list[WeatherPoint]: ...


class OpenMeteoWeatherProvider:
    name = "open_meteo"

    def __init__(self, base_url: str, timeout_seconds: float = 8) -> None:
        self.base_url = base_url
        self.timeout_seconds = timeout_seconds

    async def forecast(
        self,
        latitude: float,
        longitude: float,
    ) -> list[WeatherPoint]:
        if not (-90.0 <= latitude <= 90.0) or not (-180.0 <= longitude <= 180.0):
            raise WeatherProviderError("Geçersiz koordinat değerleri.")
        params = {
            "latitude": latitude,
            "longitude": longitude,
            "hourly": (
                "temperature_2m,precipitation_probability,"
                "precipitation,wind_speed_10m"
            ),
            "forecast_days": 2,
            "timezone": "UTC",
            "temperature_unit": "celsius",
            "wind_speed_unit": "kmh",
            "precipitation_unit": "mm",
        }
        try:
            async with httpx.AsyncClient(timeout=self.timeout_seconds) as client:
                response = await client.get(self.base_url, params=params)
                response.raise_for_status()
                hourly = response.json()["hourly"]
            times = hourly["time"]
            temperatures = hourly["temperature_2m"]
            probabilities = hourly["precipitation_probability"]
            precipitation = hourly["precipitation"]
            wind_speeds = hourly["wind_speed_10m"]
            if not times:
                raise ValueError("empty weather timeline")
            if len({len(times), len(temperatures), len(probabilities), len(precipitation), len(wind_speeds)}) != 1:
                raise ValueError("weather series lengths do not match")
            return [
                WeatherPoint(
                    observed_at=_parse_utc_timestamp(timestamp),
                    temperature_c=_optional_float(temperature),
                    precipitation_probability=_optional_float(probability),
                    precipitation_mm=_optional_float(rain),
                    wind_speed_kmh=_optional_float(wind),
                )
                for timestamp, temperature, probability, rain, wind in zip(
                    times,
                    temperatures,
                    probabilities,
                    precipitation,
                    wind_speeds,
                    strict=True,
                )
            ]
        except (
            httpx.HTTPError,
            KeyError,
            TypeError,
            ValueError,
        ) as exc:
            raise WeatherProviderError(
                "Hava durumu sağlayıcısından geçerli veri alınamadı."
            ) from exc


def _optional_float(value: object) -> float | None:
    return None if value is None else float(value)


def _parse_utc_timestamp(value: str) -> datetime:
    parsed = datetime.fromisoformat(value)
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def serialize_weather_points(points: list[WeatherPoint]) -> dict:
    serialized = []
    for point in points:
        values = asdict(point)
        values["observed_at"] = point.observed_at.isoformat()
        serialized.append(values)
    return {"points": serialized}


def deserialize_weather_points(payload: dict) -> list[WeatherPoint]:
    try:
        return [
            WeatherPoint(
                observed_at=_parse_utc_timestamp(item["observed_at"]),
                temperature_c=_optional_float(item.get("temperature_c")),
                precipitation_probability=_optional_float(
                    item.get("precipitation_probability")
                ),
                precipitation_mm=_optional_float(item.get("precipitation_mm")),
                wind_speed_kmh=_optional_float(item.get("wind_speed_kmh")),
            )
            for item in payload["points"]
        ]
    except (KeyError, TypeError, ValueError) as exc:
        raise WeatherProviderError("Kayıtlı hava durumu verisi okunamadı.") from exc


def create_weather_provider(settings: Settings) -> WeatherProvider:
    if settings.weather_provider == "open_meteo":
        return OpenMeteoWeatherProvider(
            base_url=settings.open_meteo_base_url,
            timeout_seconds=settings.weather_timeout_seconds,
        )
    raise ValueError(
        f"Desteklenmeyen hava durumu sağlayıcısı: {settings.weather_provider}"
    )

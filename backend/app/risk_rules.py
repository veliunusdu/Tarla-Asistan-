from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from enum import StrEnum

from app.weather import WeatherPoint

FROST_TEMPERATURE_C = 0
STRONG_WIND_KMH = 30
HEAVY_RAIN_PROBABILITY = 70
HEAVY_RAIN_MM_PER_HOUR = 5


class WeatherRiskType(StrEnum):
    FROST = "FROST"
    STRONG_WIND = "STRONG_WIND"
    HEAVY_RAIN = "HEAVY_RAIN"


class RiskSeverity(StrEnum):
    HIGH = "HIGH"
    CRITICAL = "CRITICAL"


@dataclass(frozen=True)
class WeatherRisk:
    risk_type: WeatherRiskType
    severity: RiskSeverity
    starts_at: datetime
    ends_at: datetime
    message: str
    suggested_action: str


def evaluate_weather_risks(
    points: list[WeatherPoint],
    *,
    now: datetime | None = None,
) -> list[WeatherRisk]:
    reference = now or datetime.now(timezone.utc)
    reference = _as_utc(reference)
    future_points = [
        point
        for point in points
        if _as_utc(point.observed_at) >= reference
    ]
    risks: list[WeatherRisk] = []

    frost = _matching(
        future_points,
        reference + timedelta(hours=24),
        lambda point: (
            point.temperature_c is not None
            and point.temperature_c <= FROST_TEMPERATURE_C
        ),
    )
    if frost:
        risks.append(
            _risk(
                frost,
                WeatherRiskType.FROST,
                RiskSeverity.CRITICAL,
                "Önümüzdeki 24 saatte don riski görülebilir.",
                (
                    "Hassas ürünleri kontrol edin ve bölgenize uygun koruma "
                    "önlemlerini bir uzmana danışarak değerlendirin."
                ),
            )
        )

    strong_wind = _matching(
        future_points,
        reference + timedelta(hours=24),
        lambda point: (
            point.wind_speed_kmh is not None
            and point.wind_speed_kmh >= STRONG_WIND_KMH
        ),
    )
    if strong_wind:
        risks.append(
            _risk(
                strong_wind,
                WeatherRiskType.STRONG_WIND,
                RiskSeverity.HIGH,
                "Önümüzdeki 24 saatte kuvvetli rüzgâr görülebilir.",
                (
                    "İlaçlama planını ertelemeyi değerlendirin; saha koşullarını "
                    "yerinde kontrol etmeden uygulama yapmayın."
                ),
            )
        )

    heavy_rain = _matching(
        future_points,
        reference + timedelta(hours=12),
        lambda point: (
            point.precipitation_probability is not None
            and point.precipitation_probability >= HEAVY_RAIN_PROBABILITY
            and point.precipitation_mm is not None
            and point.precipitation_mm >= HEAVY_RAIN_MM_PER_HOUR
        ),
    )
    if heavy_rain:
        risks.append(
            _risk(
                heavy_rain,
                WeatherRiskType.HEAVY_RAIN,
                RiskSeverity.HIGH,
                "Önümüzdeki 12 saatte yoğun yağış riski görülebilir.",
                (
                    "Sulama planını yeniden değerlendirin ve drenajı kontrol edin; "
                    "kararı yerel koşullara göre verin."
                ),
            )
        )
    return risks


def _matching(
    points: list[WeatherPoint],
    deadline: datetime,
    predicate,
) -> list[WeatherPoint]:
    return [
        point
        for point in points
        if _as_utc(point.observed_at) <= deadline and predicate(point)
    ]


def _risk(
    points: list[WeatherPoint],
    risk_type: WeatherRiskType,
    severity: RiskSeverity,
    message: str,
    suggested_action: str,
) -> WeatherRisk:
    timestamps = [_as_utc(point.observed_at) for point in points]
    return WeatherRisk(
        risk_type=risk_type,
        severity=severity,
        starts_at=min(timestamps),
        ends_at=max(timestamps),
        message=message,
        suggested_action=suggested_action,
    )


def _as_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)

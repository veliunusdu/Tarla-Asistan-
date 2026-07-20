from datetime import datetime, timedelta, timezone

from app.risk_rules import evaluate_weather_risks
from app.weather import WeatherPoint


def point(
    observed_at: datetime,
    *,
    temperature: float = 12,
    probability: float = 10,
    precipitation: float = 0,
    wind: float = 5,
) -> WeatherPoint:
    return WeatherPoint(
        observed_at=observed_at,
        temperature_c=temperature,
        precipitation_probability=probability,
        precipitation_mm=precipitation,
        wind_speed_kmh=wind,
    )


def test_risk_thresholds_and_safe_action_language():
    now = datetime(2026, 7, 20, 10, tzinfo=timezone.utc)
    risks = evaluate_weather_risks(
        [
            point(now + timedelta(hours=1), temperature=0),
            point(now + timedelta(hours=2), wind=30),
            point(
                now + timedelta(hours=3),
                probability=70,
                precipitation=5,
            ),
        ],
        now=now,
    )

    assert {risk.risk_type.value for risk in risks} == {
        "FROST",
        "STRONG_WIND",
        "HEAVY_RAIN",
    }
    actions = " ".join(risk.suggested_action.lower() for risk in risks)
    assert "değerlendirin" in actions
    assert "kontrol" in actions
    assert "kesinlikle" not in actions


def test_outside_thresholds_and_windows_do_not_create_risks():
    now = datetime(2026, 7, 20, 10, tzinfo=timezone.utc)
    risks = evaluate_weather_risks(
        [
            point(
                now + timedelta(hours=1),
                temperature=0.1,
                probability=69,
                precipitation=4.9,
                wind=29.9,
            ),
            point(now + timedelta(hours=25), temperature=-5, wind=50),
        ],
        now=now,
    )
    assert risks == []

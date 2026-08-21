import uuid
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import get_db
from app.dependencies import get_current_user, get_weather_provider
from app.models import User, WeatherSnapshot, utcnow
from app.risk_rules import evaluate_weather_risks
from app.routers.farms import get_owned_farm
from app.schemas import (
    FarmWeatherResponse,
    WeatherPointResponse,
    WeatherRiskResponse,
)
from app.weather import (
    WeatherPoint,
    WeatherProvider,
    WeatherProviderError,
    deserialize_weather_points,
    serialize_weather_points,
)

router = APIRouter(prefix="/farms", tags=["Hava Durumu"])


@router.get(
    "/{farm_id}/weather",
    response_model=FarmWeatherResponse,
    summary="Tarla için hava tahmini ve tarımsal riskleri getir",
)
async def get_farm_weather(
    farm_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    provider: WeatherProvider = Depends(get_weather_provider),
    settings: Settings = Depends(get_settings),
) -> FarmWeatherResponse:
    farm = get_owned_farm(db, user, farm_id)
    if farm.latitude is None or farm.longitude is None:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Hava durumu için tarlanın konumu tanımlanmalıdır.",
        )
    if not (-90.0 <= farm.latitude <= 90.0) or not (-180.0 <= farm.longitude <= 180.0):
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Geçersiz tarla koordinatları.",
        )

    now = utcnow()
    is_stale = False
    stale_reason: str | None = None
    try:
        points = await provider.forecast(farm.latitude, farm.longitude)
        if not points:
            raise WeatherProviderError("Hava durumu sağlayıcısı boş veri döndürdü.")
        snapshot = WeatherSnapshot(
            farm_id=farm.id,
            provider=provider.name,
            payload=serialize_weather_points(points),
            fetched_at=now,
        )
        db.add(snapshot)
        db.commit()
        db.refresh(snapshot)
    except WeatherProviderError:
        snapshot = db.scalar(
            select(WeatherSnapshot)
            .where(WeatherSnapshot.farm_id == farm.id)
            .order_by(WeatherSnapshot.fetched_at.desc())
            .limit(1)
        )
        if snapshot is None:
            raise HTTPException(
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                detail=(
                    "Hava durumu şu anda alınamıyor. Daha sonra tekrar deneyin; "
                    "bu sırada saha koşullarını yerinde kontrol edin."
                ),
            ) from None
        try:
            points = deserialize_weather_points(snapshot.payload)
        except WeatherProviderError as exc:
            raise HTTPException(
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                detail="Hava durumu verisi şu anda kullanılamıyor.",
            ) from exc
        is_stale = True
        stale_reason = (
            "Sağlayıcıya ulaşılamadı; son başarılı hava durumu verisi gösteriliyor."
        )

    fetched_at = _as_utc(snapshot.fetched_at)
    stale_deadline = now - timedelta(hours=settings.weather_stale_after_hours)
    if fetched_at < stale_deadline:
        is_stale = True
        stale_reason = stale_reason or (
            "Hava durumu verisi güncellik süresini aştı; saha koşullarını kontrol edin."
        )

    risks = evaluate_weather_risks(points, now=now)
    return FarmWeatherResponse(
        farm_id=farm.id,
        provider=snapshot.provider,
        fetched_at=fetched_at,
        is_stale=is_stale,
        stale_reason=stale_reason,
        points=[WeatherPointResponse(**_point_values(point)) for point in points],
        risks=[
            WeatherRiskResponse(
                risk_type=risk.risk_type.value,
                severity=risk.severity.value,
                starts_at=risk.starts_at,
                ends_at=risk.ends_at,
                message=risk.message,
                suggested_action=risk.suggested_action,
            )
            for risk in risks
        ],
    )


def _point_values(point: WeatherPoint) -> dict:
    return {
        "observed_at": point.observed_at,
        "temperature_c": point.temperature_c,
        "precipitation_probability": point.precipitation_probability,
        "precipitation_mm": point.precipitation_mm,
        "wind_speed_kmh": point.wind_speed_kmh,
    }


def _as_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)

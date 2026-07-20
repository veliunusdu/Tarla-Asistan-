import uuid

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.database import get_db
from app.dependencies import get_current_user
from app.models import CropPeriod, CropPeriodStatus, User
from app.routers.farms import get_owned_farm
from app.schemas import (
    CropPeriodClose,
    CropPeriodCreate,
    CropPeriodListResponse,
    CropPeriodResponse,
)

router = APIRouter(prefix="/farms", tags=["Üretim Dönemleri"])


@router.get(
    "/{farm_id}/production-periods",
    response_model=CropPeriodListResponse,
    summary="Tarlanın üretim dönemi geçmişini listele",
)
def list_production_periods(
    farm_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> CropPeriodListResponse:
    farm = get_owned_farm(db, user, farm_id)
    periods = db.scalars(
        select(CropPeriod)
        .where(CropPeriod.farm_id == farm.id)
        .order_by(CropPeriod.planted_at.desc(), CropPeriod.created_at.desc())
    ).all()
    return CropPeriodListResponse(
        items=[CropPeriodResponse.model_validate(period) for period in periods]
    )


@router.post(
    "/{farm_id}/production-periods",
    response_model=CropPeriodResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Yeni üretim dönemi başlat",
)
def create_production_period(
    farm_id: uuid.UUID,
    payload: CropPeriodCreate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> CropPeriod:
    farm = get_owned_farm(db, user, farm_id)
    active_period = db.scalar(
        select(CropPeriod).where(
            CropPeriod.farm_id == farm.id,
            CropPeriod.status == CropPeriodStatus.ACTIVE,
        )
    )
    if active_period is not None and not payload.close_existing:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail=(
                "Bu tarlada aktif bir ürün bulunuyor. Yeni dönemi başlatmak için "
                "close_existing=true ile mevcut dönemi kapatmayı onaylayın."
            ),
        )
    if active_period is not None:
        if payload.planted_at < active_period.planted_at:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail="Yeni ekim tarihi mevcut aktif dönemin ekim tarihinden önce olamaz.",
            )
        active_period.status = CropPeriodStatus.ARCHIVED
        active_period.harvested_at = payload.planted_at

    period = CropPeriod(
        farm_id=farm.id,
        crop_type=payload.crop_type,
        variety=payload.variety,
        planted_at=payload.planted_at,
        status=CropPeriodStatus.ACTIVE,
    )
    db.add(period)
    db.commit()
    db.refresh(period)
    return period


@router.post(
    "/{farm_id}/production-periods/{period_id}/close",
    response_model=CropPeriodResponse,
    summary="Aktif üretim dönemini kapat",
)
def close_production_period(
    farm_id: uuid.UUID,
    period_id: uuid.UUID,
    payload: CropPeriodClose,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> CropPeriod:
    farm = get_owned_farm(db, user, farm_id)
    period = db.scalar(
        select(CropPeriod).where(
            CropPeriod.id == period_id,
            CropPeriod.farm_id == farm.id,
        )
    )
    if period is None:
        raise HTTPException(status_code=404, detail="Üretim dönemi bulunamadı.")
    if period.status != CropPeriodStatus.ACTIVE:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Bu üretim dönemi zaten kapatılmış.",
        )
    if payload.harvested_at < period.planted_at:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Hasat tarihi ekim tarihinden önce olamaz.",
        )
    period.harvested_at = payload.harvested_at
    period.status = CropPeriodStatus.ARCHIVED
    db.commit()
    db.refresh(period)
    return period

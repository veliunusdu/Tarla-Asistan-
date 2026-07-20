import uuid

from fastapi import APIRouter, Depends, HTTPException, Query, Response, status
from sqlalchemy import func, select
from sqlalchemy.orm import Session, selectinload

from app.database import get_db
from app.dependencies import get_current_user
from app.models import CropPeriod, CropPeriodStatus, Farm, User, utcnow
from app.schemas import (
    FarmCreate,
    FarmListResponse,
    FarmMutationResponse,
    FarmResponse,
    FarmUpdate,
)

router = APIRouter(prefix="/farms", tags=["Tarlalar"])

DUPLICATE_NAME_WARNING = (
    "Aynı ada sahip aktif bir tarlanız zaten var. Konumları kontrol edin."
)


def get_owned_farm(
    db: Session,
    user: User,
    farm_id: uuid.UUID,
    *,
    include_archived: bool = False,
) -> Farm:
    filters = [Farm.id == farm_id, Farm.owner_id == user.id]
    if not include_archived:
        filters.append(Farm.archived_at.is_(None))
    farm = db.scalar(
        select(Farm)
        .options(selectinload(Farm.crop_periods))
        .where(*filters)
    )
    if farm is None:
        # Yetkisiz kullanıcıya kaydın varlığını sızdırmamak için 404 döner.
        raise HTTPException(status_code=404, detail="Tarla bulunamadı.")
    return farm


def duplicate_name_warnings(
    db: Session,
    user: User,
    name: str,
    *,
    exclude_farm_id: uuid.UUID | None = None,
) -> list[str]:
    filters = [
        Farm.owner_id == user.id,
        Farm.archived_at.is_(None),
        func.lower(Farm.name) == name.lower(),
    ]
    if exclude_farm_id is not None:
        filters.append(Farm.id != exclude_farm_id)
    duplicate = db.scalar(select(Farm.id).where(*filters).limit(1))
    return [DUPLICATE_NAME_WARNING] if duplicate is not None else []


@router.post(
    "",
    response_model=FarmMutationResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Tarla ve ilk üretim dönemini oluştur",
)
def create_farm(
    payload: FarmCreate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> FarmMutationResponse:
    warnings = duplicate_name_warnings(db, user, payload.name)
    farm = Farm(
        owner_id=user.id,
        name=payload.name,
        latitude=payload.latitude,
        longitude=payload.longitude,
        size_in_hectares=payload.size_in_hectares,
        irrigation_method=payload.irrigation_method,
        soil_type=payload.soil_type,
        note=payload.note,
    )
    farm.crop_periods.append(
        CropPeriod(
            crop_type=payload.crop_type,
            variety=payload.variety,
            planted_at=payload.planted_at,
            status=CropPeriodStatus.ACTIVE,
        )
    )
    db.add(farm)
    db.commit()
    db.refresh(farm)
    return FarmMutationResponse(
        farm=FarmResponse.model_validate(farm),
        warnings=warnings,
    )


@router.get(
    "",
    response_model=FarmListResponse,
    summary="Kullanıcının tarlalarını listele",
)
def list_farms(
    include_archived: bool = False,
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> FarmListResponse:
    filters = [Farm.owner_id == user.id]
    if not include_archived:
        filters.append(Farm.archived_at.is_(None))
    farms = db.scalars(
        select(Farm)
        .options(selectinload(Farm.crop_periods))
        .where(*filters)
        .order_by(Farm.created_at.desc())
        .limit(limit)
        .offset(offset)
    ).all()
    total = db.scalar(select(func.count(Farm.id)).where(*filters)) or 0
    return FarmListResponse(
        items=[FarmResponse.model_validate(farm) for farm in farms],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.get(
    "/{farm_id}",
    response_model=FarmResponse,
    summary="Sahibi olunan tarlayı getir",
)
def get_farm(
    farm_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Farm:
    return get_owned_farm(db, user, farm_id)


@router.patch(
    "/{farm_id}",
    response_model=FarmMutationResponse,
    summary="Sahibi olunan tarlayı güncelle",
)
def update_farm(
    farm_id: uuid.UUID,
    payload: FarmUpdate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> FarmMutationResponse:
    farm = get_owned_farm(db, user, farm_id)
    values = payload.model_dump(exclude_unset=True)
    warnings: list[str] = []
    if "name" in values:
        warnings = duplicate_name_warnings(
            db,
            user,
            values["name"],
            exclude_farm_id=farm.id,
        )
    for field_name, value in values.items():
        setattr(farm, field_name, value)
    db.commit()
    db.refresh(farm)
    return FarmMutationResponse(
        farm=FarmResponse.model_validate(farm),
        warnings=warnings,
    )


@router.delete(
    "/{farm_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="Sahibi olunan tarlayı arşivle",
)
def archive_farm(
    farm_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Response:
    farm = get_owned_farm(db, user, farm_id)
    farm.archived_at = utcnow()
    db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)

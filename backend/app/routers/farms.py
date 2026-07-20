import uuid

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.database import get_db
from app.dependencies import get_current_user
from app.models import Farm, User
from app.schemas import FarmResponse

router = APIRouter(prefix="/farms", tags=["Tarlalar"])


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
    farm = db.scalar(
        select(Farm).where(
            Farm.id == farm_id,
            Farm.owner_id == user.id,
            Farm.archived_at.is_(None),
        )
    )
    if farm is None:
        # Yetkisiz kullanıcıya kaydın varlığını sızdırmamak için 404 döner.
        raise HTTPException(status_code=404, detail="Tarla bulunamadı.")
    return farm

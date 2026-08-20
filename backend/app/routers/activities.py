import enum
import uuid
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Query, Response, status
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database import get_db
from app.dependencies import get_current_user, require_roles
from app.idempotency import record_operation, replayed_resource_id
from app.models import (
    Activity,
    ActivityRevision,
    ActivitySource,
    ActivityStatus,
    CropPeriod,
    Farm,
    Task,
    TaskStatus,
    User,
    UserRole,
    utcnow,
)
from app.routers.farms import get_owned_farm
from app.schemas import (
    ActivityCreate,
    ActivityListResponse,
    ActivityResponse,
    ActivityRevisionResponse,
    ActivityUpdate,
    FarmJournalResponse,
    JournalEntryResponse,
)

router = APIRouter(tags=["Faaliyetler ve Tarla Günlüğü"])


@router.post(
    "/farms/{farm_id}/activities",
    response_model=ActivityResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Faaliyet veya sesli faaliyet taslağı oluştur",
)
def create_activity(
    farm_id: uuid.UUID,
    payload: ActivityCreate,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> Activity:
    farm = get_owned_farm(db, user, farm_id)
    replayed_id = replayed_resource_id(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"activity.create:{farm.id}",
        payload=payload,
    )
    if replayed_id is not None:
        replayed = db.get(Activity, replayed_id)
        if replayed is not None and replayed.farm_id == farm.id:
            return replayed
    _validate_crop_period(db, farm.id, payload.crop_period_id)
    now = utcnow()
    is_voice_draft = payload.input_method == ActivitySource.VOICE
    activity = Activity(
        farm_id=farm.id,
        crop_period_id=payload.crop_period_id,
        created_by_id=user.id,
        activity_type=payload.activity_type,
        status=(
            ActivityStatus.DRAFT
            if is_voice_draft
            else ActivityStatus.CONFIRMED
        ),
        source=payload.input_method,
        description=payload.description,
        occurred_at=payload.occurred_at,
        duration_minutes=payload.duration_minutes,
        amount=payload.amount,
        unit=payload.unit,
        photo_url=payload.photo_url,
        voice_url=payload.voice_url,
        voice_transcript=payload.voice_transcript,
        performed_by=payload.performed_by,
        cost=payload.cost,
        confirmed_at=None if is_voice_draft else now,
    )
    db.add(activity)
    db.flush()
    record_operation(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"activity.create:{farm.id}",
        payload=payload,
        resource_type="activity",
        resource_id=activity.id,
    )
    db.commit()
    db.refresh(activity)
    return activity


@router.get(
    "/farms/{farm_id}/activities",
    response_model=ActivityListResponse,
    summary="Tarla faaliyetlerini listele",
)
def list_activities(
    farm_id: uuid.UUID,
    include_drafts: bool = True,
    include_archived: bool = False,
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> ActivityListResponse:
    farm = _get_accessible_farm(db, user, farm_id)
    filters = [Activity.farm_id == farm.id]
    if not include_drafts:
        filters.append(Activity.status == ActivityStatus.CONFIRMED)
    if not include_archived:
        filters.append(Activity.archived_at.is_(None))
    items = db.scalars(
        select(Activity)
        .where(*filters)
        .order_by(Activity.occurred_at.desc(), Activity.created_at.desc())
        .limit(limit)
        .offset(offset)
    ).all()
    total = db.scalar(select(func.count(Activity.id)).where(*filters)) or 0
    return ActivityListResponse(
        items=[ActivityResponse.model_validate(item) for item in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.patch(
    "/activities/{activity_id}",
    response_model=ActivityResponse,
    summary="Faaliyeti güncelle ve önceki değerleri koru",
)
def update_activity(
    activity_id: uuid.UUID,
    payload: ActivityUpdate,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> Activity:
    activity = _get_owned_activity(db, user, activity_id)
    values = payload.model_dump(exclude_unset=True)
    if "crop_period_id" in values:
        _validate_crop_period(db, activity.farm_id, values["crop_period_id"])
    db.add(
        ActivityRevision(
            activity_id=activity.id,
            changed_by_id=user.id,
            previous_values={
                field_name: _json_value(getattr(activity, field_name))
                for field_name in values
            },
        )
    )
    for field_name, value in values.items():
        setattr(activity, field_name, value)
    db.commit()
    db.refresh(activity)
    return activity


@router.post(
    "/activities/{activity_id}/confirm",
    response_model=ActivityResponse,
    summary="Sesli faaliyet taslağını doğrula",
)
def confirm_activity(
    activity_id: uuid.UUID,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> Activity:
    activity = _get_owned_activity(db, user, activity_id)
    if activity.status == ActivityStatus.CONFIRMED:
        return activity
    activity.status = ActivityStatus.CONFIRMED
    activity.confirmed_at = utcnow()
    db.commit()
    db.refresh(activity)
    return activity


@router.get(
    "/activities/{activity_id}/revisions",
    response_model=list[ActivityRevisionResponse],
    summary="Faaliyet değişiklik geçmişini getir",
)
def list_activity_revisions(
    activity_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> list[ActivityRevision]:
    activity = _get_accessible_activity(db, user, activity_id)
    return db.scalars(
        select(ActivityRevision)
        .where(ActivityRevision.activity_id == activity.id)
        .order_by(ActivityRevision.changed_at.desc())
    ).all()


@router.delete(
    "/activities/{activity_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="Faaliyeti arşivle",
)
def archive_activity(
    activity_id: uuid.UUID,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> Response:
    activity = _get_owned_activity(db, user, activity_id)
    if activity.archived_at is None:
        db.add(
            ActivityRevision(
                activity_id=activity.id,
                changed_by_id=user.id,
                previous_values={"archived_at": None},
            )
        )
        activity.archived_at = utcnow()
        db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@router.post(
    "/activities/{activity_id}/restore",
    response_model=ActivityResponse,
    summary="Arşivlenmiş faaliyeti geri yükle",
)
def restore_activity(
    activity_id: uuid.UUID,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> Activity:
    activity = _get_owned_activity(
        db,
        user,
        activity_id,
        include_archived=True,
    )
    if activity.archived_at is not None:
        db.add(
            ActivityRevision(
                activity_id=activity.id,
                changed_by_id=user.id,
                previous_values={
                    "archived_at": _json_value(activity.archived_at)
                },
            )
        )
        activity.archived_at = None
        db.commit()
        db.refresh(activity)
    return activity


@router.get(
    "/farms/{farm_id}/journal",
    response_model=FarmJournalResponse,
    summary="Tarla günlüğünü kronolojik olarak getir",
)
def get_farm_journal(
    farm_id: uuid.UUID,
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> FarmJournalResponse:
    farm = _get_accessible_farm(db, user, farm_id)
    activities = db.scalars(
        select(Activity).where(
            Activity.farm_id == farm.id,
            Activity.status == ActivityStatus.CONFIRMED,
            Activity.archived_at.is_(None),
            Activity.source != ActivitySource.TASK,
        )
    ).all()
    tasks = db.scalars(
        select(Task).where(
            Task.farm_id == farm.id,
            Task.status.in_(
                (
                    TaskStatus.COMPLETED,
                    TaskStatus.NOT_APPLIED,
                    TaskStatus.CANCELLED,
                )
            ),
        )
    ).all()

    entries = [
        JournalEntryResponse(
            entry_type="ACTIVITY",
            id=activity.id,
            occurred_at=activity.occurred_at,
            title=activity.activity_type.value,
            description=activity.description,
            metadata={
                "source": activity.source.value,
                "photo_url": activity.photo_url,
                "voice_url": activity.voice_url,
            },
        )
        for activity in activities
    ]
    entries.extend(
        JournalEntryResponse(
            entry_type="TASK",
            id=task.id,
            occurred_at=task.completed_at or task.updated_at,
            title=task.title,
            description=(
                task.not_applied_reason
                if task.status == TaskStatus.NOT_APPLIED
                else task.completion_note or task.description
            ),
            metadata={
                "status": task.status.value,
                "source": task.source.value,
                "priority": task.priority.value,
                "photo_url": task.photo_url,
            },
        )
        for task in tasks
    )
    entries.sort(key=lambda item: item.occurred_at, reverse=True)
    total = len(entries)
    return FarmJournalResponse(
        items=entries[offset : offset + limit],
        total=total,
        limit=limit,
        offset=offset,
    )


def _get_accessible_farm(
    db: Session,
    user: User,
    farm_id: uuid.UUID,
) -> Farm:
    if user.role == UserRole.FARMER:
        return get_owned_farm(db, user, farm_id)
    farm = db.scalar(
        select(Farm).where(Farm.id == farm_id, Farm.archived_at.is_(None))
    )
    if farm is None:
        raise HTTPException(status_code=404, detail="Tarla bulunamadı.")
    return farm


def _get_accessible_activity(
    db: Session,
    user: User,
    activity_id: uuid.UUID,
) -> Activity:
    query = (
        select(Activity)
        .join(Farm, Farm.id == Activity.farm_id)
        .where(Activity.id == activity_id)
    )
    if user.role == UserRole.FARMER:
        query = query.where(Farm.owner_id == user.id)
    activity = db.scalar(query)
    if activity is None:
        raise HTTPException(status_code=404, detail="Faaliyet bulunamadı.")
    return activity


def _get_owned_activity(
    db: Session,
    user: User,
    activity_id: uuid.UUID,
    *,
    include_archived: bool = False,
) -> Activity:
    filters = [
        Activity.id == activity_id,
        Farm.id == Activity.farm_id,
        Farm.owner_id == user.id,
    ]
    if not include_archived:
        filters.append(Activity.archived_at.is_(None))
    activity = db.scalar(select(Activity).join(Farm).where(*filters))
    if activity is None:
        raise HTTPException(status_code=404, detail="Faaliyet bulunamadı.")
    return activity


def _validate_crop_period(
    db: Session,
    farm_id: uuid.UUID,
    crop_period_id: uuid.UUID | None,
) -> None:
    if crop_period_id is None:
        return
    exists = db.scalar(
        select(CropPeriod.id).where(
            CropPeriod.id == crop_period_id,
            CropPeriod.farm_id == farm_id,
        )
    )
    if exists is None:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Üretim dönemi bu tarlaya ait değil.",
        )


def _json_value(value):
    if isinstance(value, enum.Enum):
        return value.value
    if isinstance(value, (datetime, uuid.UUID)):
        return value.isoformat() if isinstance(value, datetime) else str(value)
    return value

import uuid
from datetime import date

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import get_db
from app.dependencies import get_current_user, get_push_provider, require_roles
from app.models import (
    Activity,
    ActivitySource,
    ActivityStatus,
    ActivityType,
    CropPeriod,
    CropPeriodStatus,
    Farm,
    NotificationType,
    Task,
    TaskSource,
    TaskStatus,
    User,
    UserRole,
    utcnow,
)
from app.notifications import safe_notify_user
from app.push import PushProvider
from app.routers.farms import get_owned_farm
from app.schemas import (
    DailyTaskListResponse,
    TaskCompleteRequest,
    TaskCreate,
    TaskResponse,
    TaskStatusUpdate,
)
from app.task_engine import (
    ensure_daily_tasks,
    mark_overdue_tasks,
    task_dedupe_key,
    task_sort_key,
)

router = APIRouter(tags=["Günlük Görevler"])

ACTIVE_TASK_STATUSES = (TaskStatus.NEW, TaskStatus.VIEWED, TaskStatus.PLANNED)
TERMINAL_TASK_STATUSES = (
    TaskStatus.COMPLETED,
    TaskStatus.NOT_APPLIED,
    TaskStatus.CANCELLED,
)


@router.post(
    "/farms/{farm_id}/tasks",
    response_model=TaskResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Uzman görevi oluştur",
)
def create_expert_task(
    farm_id: uuid.UUID,
    payload: TaskCreate,
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
    provider: PushProvider = Depends(get_push_provider),
) -> Task:
    farm = db.scalar(select(Farm).where(Farm.id == farm_id, Farm.archived_at.is_(None)))
    if farm is None:
        raise HTTPException(status_code=404, detail="Tarla bulunamadı.")

    crop_period_id = payload.crop_period_id
    if crop_period_id is None:
        crop_period_id = db.scalar(
            select(CropPeriod.id).where(
                CropPeriod.farm_id == farm.id,
                CropPeriod.status == CropPeriodStatus.ACTIVE,
            )
        )
    elif (
        db.scalar(
            select(CropPeriod.id).where(
                CropPeriod.id == crop_period_id,
                CropPeriod.farm_id == farm.id,
            )
        )
        is None
    ):
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Üretim dönemi bu tarlaya ait değil.",
        )

    dedupe_key = task_dedupe_key(
        source=TaskSource.EXPERT,
        title=payload.title,
        description=payload.description,
        reason=payload.reason,
        crop_period_id=crop_period_id,
    )
    task = Task(
        farm_id=farm.id,
        crop_period_id=crop_period_id,
        created_by_id=user.id,
        title=payload.title,
        description=payload.description,
        reason=payload.reason,
        priority=payload.priority,
        status=TaskStatus.NEW,
        source=TaskSource.EXPERT,
        confidence=payload.confidence,
        due_date=payload.due_date,
        dedupe_key=dedupe_key,
    )
    db.add(task)
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Aynı görev bu tarla ve tarih için zaten mevcut.",
        ) from None
    db.refresh(task)
    safe_notify_user(
        db,
        provider,
        user_id=farm.owner_id,
        notification_type=NotificationType.TASK_ASSIGNED,
        title="Yeni uzman göreviniz var",
        body=task.title,
        deep_link=f"tarla-asistani://farms/{farm.id}/tasks/{task.id}",
        data={"farm_id": str(farm.id), "task_id": str(task.id)},
        dedupe_key=f"task-assigned:{task.id}",
    )
    return task


@router.get(
    "/farms/{farm_id}/tasks",
    response_model=DailyTaskListResponse,
    summary="Günlük öncelikli görevleri listele",
)
def list_daily_tasks(
    farm_id: uuid.UUID,
    target_date: date = Query(default_factory=date.today, alias="date"),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
    provider: PushProvider = Depends(get_push_provider),
) -> DailyTaskListResponse:
    farm = get_owned_farm(db, user, farm_id)
    created_tasks = ensure_daily_tasks(db, farm, target_date, settings)
    for created_task in created_tasks:
        if (
            created_task.source == TaskSource.WEATHER
            and created_task.priority.value == "CRITICAL"
        ):
            safe_notify_user(
                db,
                provider,
                user_id=farm.owner_id,
                notification_type=NotificationType.CRITICAL_WEATHER,
                title="Kritik hava uyarısı",
                body=created_task.title,
                deep_link=(
                    f"tarla-asistani://farms/{farm.id}/weather"
                    f"?task_id={created_task.id}"
                ),
                data={"farm_id": str(farm.id), "task_id": str(created_task.id)},
                dedupe_key=f"critical-weather:{created_task.id}",
            )
    mark_overdue_tasks(db, farm.id, date.today())

    daily_tasks = db.scalars(
        select(Task).where(
            Task.farm_id == farm.id,
            Task.due_date == target_date,
            Task.status.in_(ACTIVE_TASK_STATUSES),
        )
    ).all()
    critical_weather_alerts = sorted(
        (
            task
            for task in daily_tasks
            if task.source == TaskSource.WEATHER and task.priority.value == "CRITICAL"
        ),
        key=task_sort_key,
    )
    visible_tasks = sorted(
        (task for task in daily_tasks if task not in critical_weather_alerts),
        key=task_sort_key,
    )[:3]
    overdue = db.scalars(
        select(Task)
        .where(
            Task.farm_id == farm.id,
            Task.status == TaskStatus.OVERDUE,
        )
        .order_by(Task.due_date.asc(), Task.created_at.asc())
        .limit(20)
    ).all()
    return DailyTaskListResponse(
        date=target_date,
        items=[TaskResponse.model_validate(task) for task in visible_tasks],
        critical_weather_alerts=[
            TaskResponse.model_validate(task) for task in critical_weather_alerts
        ],
        overdue=[TaskResponse.model_validate(task) for task in overdue],
    )


@router.get(
    "/tasks/{task_id}",
    response_model=TaskResponse,
    summary="Görev detayını getir",
)
def get_task(
    task_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Task:
    return _get_accessible_task(db, user, task_id)


@router.patch(
    "/tasks/{task_id}/status",
    response_model=TaskResponse,
    summary="Görev durumunu güncelle",
)
def update_task_status(
    task_id: uuid.UUID,
    payload: TaskStatusUpdate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Task:
    task = _get_accessible_task(db, user, task_id)
    return _apply_status_update(db, task, user, payload)


@router.post(
    "/tasks/{task_id}/complete",
    response_model=TaskResponse,
    summary="Görevi tamamla ve tarla günlüğüne kaydet",
)
def complete_task(
    task_id: uuid.UUID,
    payload: TaskCompleteRequest,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Task:
    task = _get_accessible_task(db, user, task_id)
    return _apply_status_update(
        db,
        task,
        user,
        TaskStatusUpdate(
            status=TaskStatus.COMPLETED,
            note=payload.note,
            photo_url=payload.photo_url,
        ),
    )


def _get_accessible_task(db: Session, user: User, task_id: uuid.UUID) -> Task:
    query = (
        select(Task)
        .join(Farm, Farm.id == Task.farm_id)
        .where(Task.id == task_id, Farm.archived_at.is_(None))
    )
    if user.role == UserRole.FARMER:
        query = query.where(Farm.owner_id == user.id)
    task = db.scalar(query)
    if task is None:
        raise HTTPException(status_code=404, detail="Görev bulunamadı.")
    return task


def _apply_status_update(
    db: Session,
    task: Task,
    user: User,
    payload: TaskStatusUpdate,
) -> Task:
    if user.role == UserRole.AGRONOMIST and payload.status != TaskStatus.CANCELLED:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Uzman yalnızca görevi iptal edebilir.",
        )
    if user.role == UserRole.FARMER and payload.status == TaskStatus.CANCELLED:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Görevi yalnızca uzman iptal edebilir.",
        )
    if task.status in TERMINAL_TASK_STATUSES:
        if task.status == payload.status:
            return task
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Sonlandırılmış görevin durumu değiştirilemez.",
        )

    now = utcnow()
    task.status = payload.status
    task.completion_note = payload.note
    task.photo_url = payload.photo_url
    if payload.status == TaskStatus.VIEWED:
        task.viewed_at = now
    elif payload.status == TaskStatus.COMPLETED:
        task.completed_at = now
        _record_task_completion(db, task, user, now)
    elif payload.status == TaskStatus.NOT_APPLIED:
        task.not_applied_reason = payload.not_applied_reason
        task.completed_at = now
    elif payload.status == TaskStatus.CANCELLED:
        task.completed_at = now

    task_id = task.id
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        current = db.scalar(select(Task).where(Task.id == task_id))
        if (
            payload.status == TaskStatus.COMPLETED
            and current is not None
            and current.status == TaskStatus.COMPLETED
        ):
            return current
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Görev durumu aynı anda güncellendi; lütfen yeniden deneyin.",
        ) from None
    db.refresh(task)
    return task


def _record_task_completion(
    db: Session,
    task: Task,
    user: User,
    completed_at,
) -> None:
    existing = db.scalar(select(Activity.id).where(Activity.task_id == task.id))
    if existing is not None:
        return
    detail = task.completion_note or task.description
    db.add(
        Activity(
            farm_id=task.farm_id,
            crop_period_id=task.crop_period_id,
            task_id=task.id,
            created_by_id=user.id,
            activity_type=ActivityType.OTHER,
            status=ActivityStatus.CONFIRMED,
            source=ActivitySource.TASK,
            description=f"Görev tamamlandı: {task.title}. {detail}",
            occurred_at=completed_at,
            photo_url=task.photo_url,
            confirmed_at=completed_at,
        )
    )

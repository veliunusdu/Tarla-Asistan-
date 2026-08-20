import uuid
from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import func, select
from sqlalchemy.orm import Session, selectinload

from app.database import get_db
from app.dependencies import get_current_user, require_roles
from app.models import (
    Activity,
    CaseMessage,
    CaseStatus,
    Farm,
    FeedbackStatus,
    FeedbackType,
    Notification,
    NotificationStatus,
    PilotFeedback,
    SupportCase,
    Task,
    TaskPriority,
    TaskSource,
    TaskStatus,
    User,
    UserRole,
    utcnow,
)
from app.schemas import (
    PilotFeedbackCreate,
    PilotFeedbackListResponse,
    PilotFeedbackResponse,
    PilotFeedbackUpdate,
    PilotMetricsResponse,
)

router = APIRouter(prefix="/pilot", tags=["Pilot Ölçümü ve Geri Bildirim"])


@router.post(
    "/feedback",
    response_model=PilotFeedbackResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Haftalık geri bildirim veya yanlış uyarı kaydı oluştur",
)
def create_feedback(
    payload: PilotFeedbackCreate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> PilotFeedback:
    if payload.related_task_id is not None:
        task = _accessible_task(db, user, payload.related_task_id)
        if (
            payload.feedback_type == FeedbackType.FALSE_ALERT
            and task.source != TaskSource.WEATHER
        ):
            raise HTTPException(
                status_code=422,
                detail="Yanlış uyarı kaydı yalnızca hava uyarısı görevi için açılabilir.",
            )
    if payload.related_case_id is not None:
        _accessible_case(db, user, payload.related_case_id)
    feedback = PilotFeedback(
        created_by_id=user.id,
        feedback_type=payload.feedback_type,
        rating=payload.rating,
        comment=payload.comment,
        related_task_id=payload.related_task_id,
        related_case_id=payload.related_case_id,
    )
    db.add(feedback)
    db.commit()
    return _get_feedback(db, feedback.id)


@router.get(
    "/feedback",
    response_model=PilotFeedbackListResponse,
    summary="Pilot geri bildirimlerini listele",
)
def list_feedback(
    feedback_type: FeedbackType | None = None,
    feedback_status: FeedbackStatus | None = Query(default=None, alias="status"),
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
) -> PilotFeedbackListResponse:
    filters = []
    if feedback_type is not None:
        filters.append(PilotFeedback.feedback_type == feedback_type)
    if feedback_status is not None:
        filters.append(PilotFeedback.status == feedback_status)
    items = db.scalars(
        select(PilotFeedback)
        .options(selectinload(PilotFeedback.created_by))
        .where(*filters)
        .order_by(PilotFeedback.created_at.desc())
        .limit(limit)
        .offset(offset)
    ).all()
    total = db.scalar(select(func.count(PilotFeedback.id)).where(*filters)) or 0
    return PilotFeedbackListResponse(
        items=[PilotFeedbackResponse.model_validate(item) for item in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.patch(
    "/feedback/{feedback_id}",
    response_model=PilotFeedbackResponse,
    summary="Pilot geri bildirim durumunu güncelle",
)
def update_feedback(
    feedback_id: uuid.UUID,
    payload: PilotFeedbackUpdate,
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
) -> PilotFeedback:
    feedback = db.get(PilotFeedback, feedback_id)
    if feedback is None:
        raise HTTPException(status_code=404, detail="Geri bildirim bulunamadı.")
    feedback.status = payload.status
    feedback.reviewed_by_id = user.id
    feedback.reviewed_at = utcnow()
    db.commit()
    return _get_feedback(db, feedback.id)


@router.get(
    "/metrics",
    response_model=PilotMetricsResponse,
    summary="Pilot başarı ve kalite metriklerini getir",
)
def pilot_metrics(
    window_days: int = Query(default=7, ge=1, le=90),
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
) -> PilotMetricsResponse:
    since = utcnow() - timedelta(days=window_days)
    tasks_created = _count(db, Task.id, Task.created_at >= since)
    tasks_completed = _count(
        db,
        Task.id,
        Task.created_at >= since,
        Task.status == TaskStatus.COMPLETED,
    )
    critical_alerts = _count(
        db,
        Task.id,
        Task.created_at >= since,
        Task.source == TaskSource.WEATHER,
        Task.priority == TaskPriority.CRITICAL,
    )
    false_alerts = _count(
        db,
        PilotFeedback.id,
        PilotFeedback.created_at >= since,
        PilotFeedback.feedback_type == FeedbackType.FALSE_ALERT,
    )
    cases = db.scalars(select(SupportCase).where(SupportCase.created_at >= since)).all()
    response_minutes: list[float] = []
    for case in cases:
        first_response = db.scalar(
            select(func.min(CaseMessage.created_at))
            .join(User, User.id == CaseMessage.sender_id)
            .where(
                CaseMessage.case_id == case.id,
                User.role == UserRole.AGRONOMIST,
            )
        )
        if first_response is not None:
            created_at = case.created_at
            if created_at.tzinfo is None:
                created_at = created_at.replace(tzinfo=first_response.tzinfo)
            response_minutes.append(
                max((first_response - created_at).total_seconds() / 60, 0)
            )
    notifications_created = _count(
        db, Notification.id, Notification.created_at >= since
    )
    notifications_sent = _count(
        db,
        Notification.id,
        Notification.created_at >= since,
        Notification.status == NotificationStatus.SENT,
    )
    feedback_count = _count(db, PilotFeedback.id, PilotFeedback.created_at >= since)
    average_rating = db.scalar(
        select(func.avg(PilotFeedback.rating)).where(
            PilotFeedback.created_at >= since,
            PilotFeedback.rating.is_not(None),
        )
    )
    active_farmer_ids = set(
        db.scalars(
            select(Farm.owner_id)
            .join(Activity, Activity.farm_id == Farm.id)
            .where(Activity.created_at >= since)
        ).all()
    )
    active_farmer_ids.update(
        db.scalars(
            select(Farm.owner_id)
            .join(SupportCase, SupportCase.farm_id == Farm.id)
            .where(SupportCase.created_at >= since)
        ).all()
    )
    active_farmer_ids.update(
        db.scalars(
            select(PilotFeedback.created_by_id)
            .join(User, User.id == PilotFeedback.created_by_id)
            .where(
                PilotFeedback.created_at >= since,
                User.role == UserRole.FARMER,
            )
        ).all()
    )
    cases_answered = sum(
        case.status in (CaseStatus.ANSWERED, CaseStatus.CLOSED) for case in cases
    )
    return PilotMetricsResponse(
        window_days=window_days,
        active_farmers=len(active_farmer_ids),
        tasks_created=tasks_created,
        tasks_completed=tasks_completed,
        task_completion_rate=_rate(tasks_completed, tasks_created),
        critical_weather_alerts=critical_alerts,
        false_alerts=false_alerts,
        false_alert_rate=_rate(false_alerts, critical_alerts),
        cases_created=len(cases),
        cases_answered=cases_answered,
        average_expert_response_minutes=(
            round(sum(response_minutes) / len(response_minutes), 2)
            if response_minutes
            else None
        ),
        notifications_created=notifications_created,
        notifications_sent=notifications_sent,
        notification_delivery_rate=_rate(notifications_sent, notifications_created),
        feedback_count=feedback_count,
        average_feedback_rating=(
            round(float(average_rating), 2) if average_rating is not None else None
        ),
    )


def _count(db: Session, column, *filters) -> int:
    return db.scalar(select(func.count(column)).where(*filters)) or 0


def _rate(numerator: int, denominator: int) -> float:
    return round(numerator / denominator * 100, 2) if denominator else 0.0


def _accessible_task(db: Session, user: User, task_id: uuid.UUID) -> Task:
    query = select(Task).join(Farm, Farm.id == Task.farm_id).where(Task.id == task_id)
    if user.role == UserRole.FARMER:
        query = query.where(Farm.owner_id == user.id)
    task = db.scalar(query)
    if task is None:
        raise HTTPException(status_code=404, detail="Görev bulunamadı.")
    return task


def _accessible_case(db: Session, user: User, case_id: uuid.UUID) -> SupportCase:
    query = (
        select(SupportCase)
        .join(Farm, Farm.id == SupportCase.farm_id)
        .where(SupportCase.id == case_id)
    )
    if user.role == UserRole.FARMER:
        query = query.where(Farm.owner_id == user.id)
    case = db.scalar(query)
    if case is None:
        raise HTTPException(status_code=404, detail="Vaka bulunamadı.")
    return case


def _get_feedback(db: Session, feedback_id: uuid.UUID) -> PilotFeedback:
    feedback = db.scalar(
        select(PilotFeedback)
        .options(selectinload(PilotFeedback.created_by))
        .where(PilotFeedback.id == feedback_id)
        .execution_options(populate_existing=True)
    )
    if feedback is None:
        raise HTTPException(status_code=404, detail="Geri bildirim bulunamadı.")
    return feedback

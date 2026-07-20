import hashlib
from dataclasses import dataclass
from datetime import date, datetime, time, timedelta, timezone

from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.config import Settings
from app.models import (
    Activity,
    ActivityStatus,
    CropPeriod,
    CropPeriodStatus,
    Farm,
    Task,
    TaskConfidence,
    TaskPriority,
    TaskSource,
    TaskStatus,
    WeatherSnapshot,
    utcnow,
)
from app.risk_rules import RiskSeverity, WeatherRisk, evaluate_weather_risks
from app.weather import WeatherProviderError, deserialize_weather_points


@dataclass(frozen=True)
class TaskSpec:
    title: str
    description: str
    reason: str
    priority: TaskPriority
    source: TaskSource
    confidence: TaskConfidence
    due_date: date
    crop_period_id: object | None = None
    dedupe_discriminator: str = ""


def ensure_daily_tasks(
    db: Session,
    farm: Farm,
    target_date: date,
    settings: Settings,
) -> list[Task]:
    if target_date != date.today() or farm.archived_at is not None:
        return []

    crop_period = db.scalar(
        select(CropPeriod).where(
            CropPeriod.farm_id == farm.id,
            CropPeriod.status == CropPeriodStatus.ACTIVE,
        )
    )
    specs: list[TaskSpec] = []
    if crop_period is not None:
        growing_day = max((target_date - crop_period.planted_at).days + 1, 1)
        specs.append(
            TaskSpec(
                title="Ürün gelişimini sahada kontrol edin",
                description=(
                    "Bitki gelişimini, toprak nemini ve olağan dışı belirtileri "
                    "kontrol edip gözlemlerinizi tarla günlüğüne kaydedin."
                ),
                reason=(
                    f"{crop_period.crop_type.value} üretim döneminin "
                    f"{growing_day}. günü için düzenli saha kontrolü."
                ),
                priority=TaskPriority.MEDIUM,
                source=TaskSource.CROP_CALENDAR,
                confidence=TaskConfidence.MEDIUM,
                due_date=target_date,
                crop_period_id=crop_period.id,
                dedupe_discriminator="daily-field-check",
            )
        )

    since = datetime.combine(
        target_date - timedelta(days=7),
        time.min,
        tzinfo=timezone.utc,
    )
    recent_activity = db.scalar(
        select(Activity.id)
        .where(
            Activity.farm_id == farm.id,
            Activity.status == ActivityStatus.CONFIRMED,
            Activity.archived_at.is_(None),
            Activity.occurred_at >= since,
        )
        .limit(1)
    )
    if recent_activity is None:
        specs.append(
            TaskSpec(
                title="Tarla günlüğünü güncelleyin",
                description=(
                    "Son sulama, gübreleme, ilaçlama veya saha kontrolü gibi "
                    "işlemleri tarla günlüğüne ekleyin."
                ),
                reason="Son yedi gün içinde doğrulanmış faaliyet kaydı bulunamadı.",
                priority=TaskPriority.LOW,
                source=TaskSource.SYSTEM,
                confidence=TaskConfidence.HIGH,
                due_date=target_date,
                crop_period_id=crop_period.id if crop_period else None,
                dedupe_discriminator="weekly-activity-reminder",
            )
        )

    specs.extend(
        _fresh_weather_task_specs(
            db,
            farm,
            crop_period,
            target_date,
            settings,
        )
    )

    created: list[Task] = []
    for spec in specs:
        dedupe_key = task_dedupe_key(
            source=spec.source,
            title=spec.title,
            description=spec.description,
            reason=spec.reason,
            crop_period_id=spec.crop_period_id,
            discriminator=spec.dedupe_discriminator,
        )
        exists = db.scalar(
            select(Task.id).where(
                Task.farm_id == farm.id,
                Task.due_date == target_date,
                Task.dedupe_key == dedupe_key,
            )
        )
        if exists is not None:
            continue
        task = Task(
            farm_id=farm.id,
            crop_period_id=spec.crop_period_id,
            title=spec.title,
            description=spec.description,
            reason=spec.reason,
            priority=spec.priority,
            status=TaskStatus.NEW,
            source=spec.source,
            confidence=spec.confidence,
            due_date=spec.due_date,
            dedupe_key=dedupe_key,
        )
        db.add(task)
        created.append(task)

    if created:
        try:
            db.commit()
        except IntegrityError:
            # Another request may have generated the same daily set first.
            # The unique key remains the source of truth; the caller re-queries.
            db.rollback()
            return []
        for task in created:
            db.refresh(task)
    return created


def task_dedupe_key(
    *,
    source: TaskSource,
    title: str,
    description: str,
    reason: str,
    crop_period_id: object | None,
    discriminator: str = "",
) -> str:
    normalized = "|".join(
        (
            source.value,
            " ".join(title.casefold().split()),
            " ".join(description.casefold().split()),
            " ".join(reason.casefold().split()),
            str(crop_period_id or ""),
            discriminator,
        )
    )
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def mark_overdue_tasks(db: Session, farm_id: object, before: date) -> None:
    tasks = db.scalars(
        select(Task).where(
            Task.farm_id == farm_id,
            Task.due_date < before,
            Task.status.in_(
                (TaskStatus.NEW, TaskStatus.VIEWED, TaskStatus.PLANNED)
            ),
        )
    ).all()
    if not tasks:
        return
    for task in tasks:
        task.status = TaskStatus.OVERDUE
    db.commit()


def task_sort_key(task: Task) -> tuple[int, int, datetime]:
    priority_rank = {
        TaskPriority.CRITICAL: 0,
        TaskPriority.HIGH: 1,
        TaskPriority.MEDIUM: 2,
        TaskPriority.LOW: 3,
    }
    source_rank = {
        TaskSource.EXPERT: 0,
        TaskSource.WEATHER: 1,
        TaskSource.CROP_CALENDAR: 2,
        TaskSource.SYSTEM: 3,
    }
    return (
        priority_rank[task.priority],
        source_rank[task.source],
        _as_utc(task.created_at),
    )


def _fresh_weather_task_specs(
    db: Session,
    farm: Farm,
    crop_period: CropPeriod | None,
    target_date: date,
    settings: Settings,
) -> list[TaskSpec]:
    snapshot = db.scalar(
        select(WeatherSnapshot)
        .where(WeatherSnapshot.farm_id == farm.id)
        .order_by(WeatherSnapshot.fetched_at.desc())
        .limit(1)
    )
    if snapshot is None:
        return []
    fetched_at = _as_utc(snapshot.fetched_at)
    if fetched_at < utcnow() - timedelta(hours=settings.weather_stale_after_hours):
        return []
    try:
        risks = evaluate_weather_risks(
            deserialize_weather_points(snapshot.payload),
            now=utcnow(),
        )
    except WeatherProviderError:
        return []
    return [
        _weather_task_spec(risk, crop_period, target_date, snapshot)
        for risk in risks
    ]


def _weather_task_spec(
    risk: WeatherRisk,
    crop_period: CropPeriod | None,
    target_date: date,
    snapshot: WeatherSnapshot,
) -> TaskSpec:
    title_by_risk = {
        "FROST": "Don riskine karşı tarlanızı kontrol edin",
        "STRONG_WIND": "Kuvvetli rüzgâr riskini değerlendirin",
        "HEAVY_RAIN": "Yoğun yağış riskini değerlendirin",
    }
    return TaskSpec(
        title=title_by_risk[risk.risk_type.value],
        description=risk.suggested_action,
        reason=(
            f"{risk.message} Hava verisi {snapshot.provider} tarafından "
            f"{_as_utc(snapshot.fetched_at).isoformat()} tarihinde güncellendi; "
            "tahminler değişebilir."
        ),
        priority=(
            TaskPriority.CRITICAL
            if risk.severity == RiskSeverity.CRITICAL
            else TaskPriority.HIGH
        ),
        source=TaskSource.WEATHER,
        confidence=TaskConfidence.MEDIUM,
        due_date=target_date,
        crop_period_id=crop_period.id if crop_period else None,
        dedupe_discriminator=risk.risk_type.value,
    )


def _as_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)

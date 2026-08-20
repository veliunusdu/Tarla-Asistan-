import enum
import uuid
from datetime import date, datetime, timezone

from sqlalchemy import (
    JSON,
    Boolean,
    Date,
    DateTime,
    Enum,
    Float,
    ForeignKey,
    Index,
    Integer,
    String,
    Text,
    UniqueConstraint,
    text,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.database import Base


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


class UserRole(str, enum.Enum):
    FARMER = "FARMER"
    AGRONOMIST = "AGRONOMIST"


class IrrigationMethod(str, enum.Enum):
    DRIP = "DRIP"
    SPRINKLER = "SPRINKLER"
    FLOOD = "FLOOD"
    RAINFED = "RAINFED"
    OTHER = "OTHER"


class CropType(str, enum.Enum):
    WHEAT = "WHEAT"
    BARLEY = "BARLEY"
    CORN = "CORN"
    SUNFLOWER = "SUNFLOWER"
    TOMATO = "TOMATO"


class CropPeriodStatus(str, enum.Enum):
    ACTIVE = "ACTIVE"
    ARCHIVED = "ARCHIVED"


class TaskPriority(str, enum.Enum):
    LOW = "LOW"
    MEDIUM = "MEDIUM"
    HIGH = "HIGH"
    CRITICAL = "CRITICAL"


class TaskStatus(str, enum.Enum):
    NEW = "NEW"
    VIEWED = "VIEWED"
    PLANNED = "PLANNED"
    COMPLETED = "COMPLETED"
    NOT_APPLIED = "NOT_APPLIED"
    OVERDUE = "OVERDUE"
    CANCELLED = "CANCELLED"


class TaskSource(str, enum.Enum):
    SYSTEM = "SYSTEM"
    CROP_CALENDAR = "CROP_CALENDAR"
    WEATHER = "WEATHER"
    EXPERT = "EXPERT"


class TaskConfidence(str, enum.Enum):
    LOW = "LOW"
    MEDIUM = "MEDIUM"
    HIGH = "HIGH"


class ActivityType(str, enum.Enum):
    IRRIGATION = "IRRIGATION"
    FERTILIZATION = "FERTILIZATION"
    SPRAYING = "SPRAYING"
    PRUNING = "PRUNING"
    FIELD_CHECK = "FIELD_CHECK"
    HARVEST = "HARVEST"
    OTHER = "OTHER"


class ActivityStatus(str, enum.Enum):
    DRAFT = "DRAFT"
    CONFIRMED = "CONFIRMED"


class ActivitySource(str, enum.Enum):
    MANUAL = "MANUAL"
    VOICE = "VOICE"
    TASK = "TASK"


class MediaKind(str, enum.Enum):
    IMAGE = "IMAGE"
    AUDIO = "AUDIO"


class CaseCategory(str, enum.Enum):
    DISEASE = "DISEASE"
    PEST = "PEST"
    IRRIGATION = "IRRIGATION"
    NUTRITION = "NUTRITION"
    WEATHER = "WEATHER"
    OTHER = "OTHER"


class CasePriority(str, enum.Enum):
    LOW = "LOW"
    MEDIUM = "MEDIUM"
    HIGH = "HIGH"
    CRITICAL = "CRITICAL"


class CaseStatus(str, enum.Enum):
    OPEN = "OPEN"
    IN_REVIEW = "IN_REVIEW"
    WAITING_FARMER = "WAITING_FARMER"
    ANSWERED = "ANSWERED"
    CLOSED = "CLOSED"


class CaseMessageType(str, enum.Enum):
    COMMENT = "COMMENT"
    ADDITIONAL_INFO_REQUEST = "ADDITIONAL_INFO_REQUEST"
    EXPERT_RESPONSE = "EXPERT_RESPONSE"


class DevicePlatform(str, enum.Enum):
    ANDROID = "ANDROID"
    IOS = "IOS"
    WEB = "WEB"


class NotificationType(str, enum.Enum):
    TASK_ASSIGNED = "TASK_ASSIGNED"
    CRITICAL_WEATHER = "CRITICAL_WEATHER"
    EXPERT_RESPONSE = "EXPERT_RESPONSE"


class NotificationStatus(str, enum.Enum):
    PENDING = "PENDING"
    SENT = "SENT"
    FAILED = "FAILED"


class FeedbackType(str, enum.Enum):
    WEEKLY_CHECKIN = "WEEKLY_CHECKIN"
    FALSE_ALERT = "FALSE_ALERT"
    BUG = "BUG"
    SUGGESTION = "SUGGESTION"


class FeedbackStatus(str, enum.Enum):
    OPEN = "OPEN"
    REVIEWED = "REVIEWED"
    RESOLVED = "RESOLVED"


class User(Base):
    __tablename__ = "users"
    __table_args__ = (Index("ix_users_phone_number", "phone_number"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    phone_number: Mapped[str] = mapped_column(String(20), unique=True)
    firebase_uid: Mapped[str | None] = mapped_column(
        String(128), unique=True, index=True, nullable=True
    )
    role: Mapped[UserRole] = mapped_column(
        Enum(UserRole, name="user_role"), default=UserRole.FARMER, index=True
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    farms: Mapped[list["Farm"]] = relationship(
        back_populates="owner", cascade="all, delete-orphan"
    )
    profile: Mapped["Profile | None"] = relationship(
        back_populates="user", cascade="all, delete-orphan", uselist=False
    )
    refresh_tokens: Mapped[list["RefreshToken"]] = relationship(
        back_populates="user", cascade="all, delete-orphan"
    )

    @property
    def full_name(self) -> str | None:
        return self.profile.full_name if self.profile else None

    @property
    def province(self) -> str | None:
        return self.profile.province if self.profile else None

    @property
    def district(self) -> str | None:
        return self.profile.district if self.profile else None

    @property
    def terms_accepted(self) -> bool:
        return self.profile.terms_accepted if self.profile else False

    @property
    def notifications_enabled(self) -> bool:
        return self.profile.notifications_enabled if self.profile else True

    @property
    def profile_complete(self) -> bool:
        return bool(
            self.profile
            and self.profile.full_name
            and self.phone_number
            and self.profile.province
            and self.profile.district
            and self.role
            and self.profile.terms_accepted
        )


class Profile(Base):
    __tablename__ = "profiles"

    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), primary_key=True
    )
    full_name: Mapped[str] = mapped_column(String(120))
    province: Mapped[str] = mapped_column(String(80))
    district: Mapped[str] = mapped_column(String(80))
    terms_accepted: Mapped[bool] = mapped_column(Boolean, default=False)
    notifications_enabled: Mapped[bool] = mapped_column(Boolean, default=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    user: Mapped[User] = relationship(back_populates="profile")


class RefreshToken(Base):
    __tablename__ = "refresh_tokens"
    __table_args__ = (Index("ix_refresh_tokens_token_hash", "token_hash"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    family_id: Mapped[uuid.UUID] = mapped_column(default=uuid.uuid4, index=True)
    token_hash: Mapped[str] = mapped_column(String(64), unique=True)
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    revoked_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )

    user: Mapped[User] = relationship(back_populates="refresh_tokens")


class Farm(Base):
    __tablename__ = "farms"
    __table_args__ = (
        Index("ix_farms_owner_archived", "owner_id", "archived_at"),
        Index("ix_farms_owner_name", "owner_id", "name"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    owner_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    name: Mapped[str] = mapped_column(String(120))
    latitude: Mapped[float | None] = mapped_column(Float)
    longitude: Mapped[float | None] = mapped_column(Float)
    size_in_hectares: Mapped[float | None] = mapped_column(Float)
    irrigation_method: Mapped[IrrigationMethod | None] = mapped_column(
        Enum(IrrigationMethod, name="irrigation_method")
    )
    soil_type: Mapped[str | None] = mapped_column(String(80))
    note: Mapped[str | None] = mapped_column(String(1000))
    archived_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    owner: Mapped[User] = relationship(back_populates="farms")
    crop_periods: Mapped[list["CropPeriod"]] = relationship(
        back_populates="farm",
        cascade="all, delete-orphan",
        order_by="CropPeriod.planted_at.desc()",
    )
    weather_snapshots: Mapped[list["WeatherSnapshot"]] = relationship(
        back_populates="farm", cascade="all, delete-orphan"
    )
    tasks: Mapped[list["Task"]] = relationship(
        back_populates="farm", cascade="all, delete-orphan"
    )
    activities: Mapped[list["Activity"]] = relationship(
        back_populates="farm", cascade="all, delete-orphan"
    )

    @property
    def current_crop(self) -> "CropPeriod | None":
        return next(
            (
                period
                for period in self.crop_periods
                if period.status == CropPeriodStatus.ACTIVE
            ),
            None,
        )


class CropPeriod(Base):
    __tablename__ = "crop_periods"
    __table_args__ = (
        Index("ix_crop_periods_farm_status", "farm_id", "status"),
        Index("ix_crop_periods_farm_planted", "farm_id", "planted_at"),
        Index(
            "uq_crop_periods_one_active_per_farm",
            "farm_id",
            unique=True,
            postgresql_where=text("status = 'ACTIVE'"),
            sqlite_where=text("status = 'ACTIVE'"),
        ),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    farm_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("farms.id", ondelete="CASCADE"), index=True
    )
    crop_type: Mapped[CropType] = mapped_column(Enum(CropType, name="crop_type"))
    variety: Mapped[str | None] = mapped_column(String(120))
    planted_at: Mapped[date] = mapped_column(Date)
    harvested_at: Mapped[date | None] = mapped_column(Date)
    status: Mapped[CropPeriodStatus] = mapped_column(
        Enum(CropPeriodStatus, name="crop_period_status"),
        default=CropPeriodStatus.ACTIVE,
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    farm: Mapped[Farm] = relationship(back_populates="crop_periods")


class WeatherSnapshot(Base):
    __tablename__ = "weather_snapshots"
    __table_args__ = (
        Index("ix_weather_snapshots_farm_fetched", "farm_id", "fetched_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    farm_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("farms.id", ondelete="CASCADE"), index=True
    )
    provider: Mapped[str] = mapped_column(String(50))
    payload: Mapped[dict] = mapped_column(JSON)
    fetched_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )

    farm: Mapped[Farm] = relationship(back_populates="weather_snapshots")


class Task(Base):
    __tablename__ = "tasks"
    __table_args__ = (
        Index("ix_tasks_farm_due_status", "farm_id", "due_date", "status"),
        Index("ix_tasks_farm_created", "farm_id", "created_at"),
        Index(
            "uq_tasks_farm_due_dedupe",
            "farm_id",
            "due_date",
            "dedupe_key",
            unique=True,
        ),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    farm_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("farms.id", ondelete="CASCADE"), index=True
    )
    crop_period_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("crop_periods.id", ondelete="SET NULL"), index=True
    )
    created_by_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    title: Mapped[str] = mapped_column(String(160))
    description: Mapped[str] = mapped_column(Text)
    reason: Mapped[str] = mapped_column(Text)
    priority: Mapped[TaskPriority] = mapped_column(
        Enum(TaskPriority, name="task_priority")
    )
    status: Mapped[TaskStatus] = mapped_column(
        Enum(TaskStatus, name="task_status"), default=TaskStatus.NEW
    )
    source: Mapped[TaskSource] = mapped_column(Enum(TaskSource, name="task_source"))
    confidence: Mapped[TaskConfidence] = mapped_column(
        Enum(TaskConfidence, name="task_confidence"),
        default=TaskConfidence.MEDIUM,
    )
    due_date: Mapped[date] = mapped_column(Date)
    dedupe_key: Mapped[str] = mapped_column(String(64))
    not_applied_reason: Mapped[str | None] = mapped_column(String(500))
    completion_note: Mapped[str | None] = mapped_column(String(1000))
    photo_url: Mapped[str | None] = mapped_column(String(2048))
    viewed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    completed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    farm: Mapped[Farm] = relationship(back_populates="tasks")
    crop_period: Mapped[CropPeriod | None] = relationship()
    created_by: Mapped[User | None] = relationship()
    completion_activity: Mapped["Activity | None"] = relationship(
        back_populates="task", uselist=False
    )

    @property
    def expert_review_recommended(self) -> bool:
        return self.confidence == TaskConfidence.LOW


class Activity(Base):
    __tablename__ = "activities"
    __table_args__ = (
        Index("ix_activities_farm_occurred", "farm_id", "occurred_at"),
        Index("ix_activities_farm_archived", "farm_id", "archived_at"),
        Index("uq_activities_task_id", "task_id", unique=True),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    farm_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("farms.id", ondelete="CASCADE"), index=True
    )
    crop_period_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("crop_periods.id", ondelete="SET NULL"), index=True
    )
    task_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("tasks.id", ondelete="SET NULL"), index=True
    )
    created_by_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    activity_type: Mapped[ActivityType] = mapped_column(
        Enum(ActivityType, name="activity_type")
    )
    status: Mapped[ActivityStatus] = mapped_column(
        Enum(ActivityStatus, name="activity_status"),
        default=ActivityStatus.CONFIRMED,
    )
    source: Mapped[ActivitySource] = mapped_column(
        Enum(ActivitySource, name="activity_source"),
        default=ActivitySource.MANUAL,
    )
    description: Mapped[str] = mapped_column(Text)
    occurred_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    duration_minutes: Mapped[int | None] = mapped_column(Integer)
    amount: Mapped[float | None] = mapped_column(Float)
    unit: Mapped[str | None] = mapped_column(String(40))
    photo_url: Mapped[str | None] = mapped_column(String(2048))
    voice_url: Mapped[str | None] = mapped_column(String(2048))
    voice_transcript: Mapped[str | None] = mapped_column(Text)
    performed_by: Mapped[str | None] = mapped_column(String(120))
    cost: Mapped[float | None] = mapped_column(Float)
    confirmed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    archived_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    farm: Mapped[Farm] = relationship(back_populates="activities")
    crop_period: Mapped[CropPeriod | None] = relationship()
    task: Mapped[Task | None] = relationship(back_populates="completion_activity")
    created_by: Mapped[User | None] = relationship()
    revisions: Mapped[list["ActivityRevision"]] = relationship(
        back_populates="activity",
        cascade="all, delete-orphan",
        order_by="ActivityRevision.changed_at.desc()",
    )


class ActivityRevision(Base):
    __tablename__ = "activity_revisions"
    __table_args__ = (
        Index("ix_activity_revisions_activity_changed", "activity_id", "changed_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    activity_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("activities.id", ondelete="CASCADE"), index=True
    )
    changed_by_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    previous_values: Mapped[dict] = mapped_column(JSON)
    changed_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )

    activity: Mapped[Activity] = relationship(back_populates="revisions")
    changed_by: Mapped[User | None] = relationship()


class MediaAsset(Base):
    __tablename__ = "media_assets"
    __table_args__ = (Index("ix_media_assets_owner_created", "owner_id", "created_at"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    owner_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    kind: Mapped[MediaKind] = mapped_column(Enum(MediaKind, name="media_kind"))
    original_name: Mapped[str] = mapped_column(String(255))
    content_type: Mapped[str] = mapped_column(String(100))
    size_bytes: Mapped[int] = mapped_column(Integer)
    storage_key: Mapped[str] = mapped_column(String(255), unique=True)
    checksum_sha256: Mapped[str] = mapped_column(String(64))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )

    owner: Mapped[User] = relationship()

    @property
    def url(self) -> str:
        return f"/api/v1/media/{self.id}/content"


class SupportCase(Base):
    __tablename__ = "support_cases"
    __table_args__ = (
        Index("ix_support_cases_status_priority", "status", "priority"),
        Index("ix_support_cases_farm_created", "farm_id", "created_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    farm_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("farms.id", ondelete="CASCADE"), index=True
    )
    created_by_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    assigned_expert_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    category: Mapped[CaseCategory] = mapped_column(
        Enum(CaseCategory, name="support_case_category")
    )
    priority: Mapped[CasePriority] = mapped_column(
        Enum(CasePriority, name="support_case_priority"),
        default=CasePriority.MEDIUM,
    )
    status: Mapped[CaseStatus] = mapped_column(
        Enum(CaseStatus, name="support_case_status"),
        default=CaseStatus.OPEN,
    )
    title: Mapped[str] = mapped_column(String(160))
    description: Mapped[str] = mapped_column(Text)
    closed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    farm: Mapped[Farm] = relationship()
    created_by: Mapped[User] = relationship(foreign_keys=[created_by_id])
    assigned_expert: Mapped[User | None] = relationship(
        foreign_keys=[assigned_expert_id]
    )
    messages: Mapped[list["CaseMessage"]] = relationship(
        back_populates="case",
        cascade="all, delete-orphan",
        order_by="CaseMessage.created_at.asc()",
    )
    media_links: Mapped[list["CaseMedia"]] = relationship(
        back_populates="case", cascade="all, delete-orphan"
    )

    @property
    def farm_name(self) -> str:
        return self.farm.name

    @property
    def farmer_name(self) -> str:
        return self.farm.owner.full_name or self.farm.owner.phone_number

    @property
    def media(self) -> list[MediaAsset]:
        return [link.media for link in self.media_links]


class CaseMessage(Base):
    __tablename__ = "case_messages"
    __table_args__ = (Index("ix_case_messages_case_created", "case_id", "created_at"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    case_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("support_cases.id", ondelete="CASCADE"), index=True
    )
    sender_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    message_type: Mapped[CaseMessageType] = mapped_column(
        Enum(CaseMessageType, name="case_message_type"),
        default=CaseMessageType.COMMENT,
    )
    body: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )

    case: Mapped[SupportCase] = relationship(back_populates="messages")
    sender: Mapped[User] = relationship()
    media_links: Mapped[list["CaseMessageMedia"]] = relationship(
        back_populates="message", cascade="all, delete-orphan"
    )

    @property
    def sender_name(self) -> str:
        return self.sender.full_name or self.sender.phone_number

    @property
    def sender_role(self) -> UserRole:
        return self.sender.role

    @property
    def media(self) -> list[MediaAsset]:
        return [link.media for link in self.media_links]


class CaseMedia(Base):
    __tablename__ = "case_media"

    case_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("support_cases.id", ondelete="CASCADE"), primary_key=True
    )
    media_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("media_assets.id", ondelete="CASCADE"), primary_key=True
    )

    case: Mapped[SupportCase] = relationship(back_populates="media_links")
    media: Mapped[MediaAsset] = relationship()


class CaseMessageMedia(Base):
    __tablename__ = "case_message_media"

    message_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("case_messages.id", ondelete="CASCADE"), primary_key=True
    )
    media_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("media_assets.id", ondelete="CASCADE"), primary_key=True
    )

    message: Mapped[CaseMessage] = relationship(back_populates="media_links")
    media: Mapped[MediaAsset] = relationship()


class ClientOperation(Base):
    __tablename__ = "client_operations"
    __table_args__ = (
        UniqueConstraint(
            "actor_id", "client_operation_id", name="uq_client_operations_actor_key"
        ),
        Index("ix_client_operations_actor_created", "actor_id", "created_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    actor_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    client_operation_id: Mapped[uuid.UUID] = mapped_column()
    scope: Mapped[str] = mapped_column(String(80))
    payload_hash: Mapped[str] = mapped_column(String(64))
    resource_type: Mapped[str] = mapped_column(String(50))
    resource_id: Mapped[uuid.UUID] = mapped_column()
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )


class DeviceToken(Base):
    __tablename__ = "device_tokens"
    __table_args__ = (Index("ix_device_tokens_user_active", "user_id", "active"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    token: Mapped[str] = mapped_column(String(512), unique=True)
    platform: Mapped[DevicePlatform] = mapped_column(
        Enum(DevicePlatform, name="device_platform")
    )
    active: Mapped[bool] = mapped_column(Boolean, default=True)
    last_seen_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    user: Mapped[User] = relationship()


class Notification(Base):
    __tablename__ = "notifications"
    __table_args__ = (
        Index("ix_notifications_user_created", "user_id", "created_at"),
        Index("ix_notifications_status_created", "status", "created_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    notification_type: Mapped[NotificationType] = mapped_column(
        Enum(NotificationType, name="notification_type")
    )
    title: Mapped[str] = mapped_column(String(160))
    body: Mapped[str] = mapped_column(String(1000))
    deep_link: Mapped[str] = mapped_column(String(500))
    data: Mapped[dict] = mapped_column(JSON, default=dict)
    dedupe_key: Mapped[str] = mapped_column(String(160), unique=True)
    status: Mapped[NotificationStatus] = mapped_column(
        Enum(NotificationStatus, name="notification_status"),
        default=NotificationStatus.PENDING,
    )
    provider_message_id: Mapped[str | None] = mapped_column(String(255))
    attempt_count: Mapped[int] = mapped_column(Integer, default=0)
    last_error: Mapped[str | None] = mapped_column(String(1000))
    sent_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    read_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    user: Mapped[User] = relationship()


class PilotFeedback(Base):
    __tablename__ = "pilot_feedback"
    __table_args__ = (
        Index("ix_pilot_feedback_type_created", "feedback_type", "created_at"),
        Index("ix_pilot_feedback_status_created", "status", "created_at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    created_by_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    feedback_type: Mapped[FeedbackType] = mapped_column(
        Enum(FeedbackType, name="feedback_type")
    )
    status: Mapped[FeedbackStatus] = mapped_column(
        Enum(FeedbackStatus, name="feedback_status"),
        default=FeedbackStatus.OPEN,
    )
    rating: Mapped[int | None] = mapped_column(Integer)
    comment: Mapped[str] = mapped_column(Text)
    related_task_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("tasks.id", ondelete="SET NULL"), index=True
    )
    related_case_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("support_cases.id", ondelete="SET NULL"), index=True
    )
    reviewed_by_id: Mapped[uuid.UUID | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), index=True
    )
    reviewed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), default=utcnow, onupdate=utcnow
    )

    created_by: Mapped[User] = relationship(foreign_keys=[created_by_id])
    reviewed_by: Mapped[User | None] = relationship(foreign_keys=[reviewed_by_id])
    related_task: Mapped[Task | None] = relationship()
    related_case: Mapped[SupportCase | None] = relationship()

    @property
    def created_by_name(self) -> str:
        return self.created_by.full_name or self.created_by.phone_number

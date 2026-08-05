import uuid
from datetime import date, datetime, timezone
from typing import Annotated

import phonenumbers
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from app.models import (
    ActivitySource,
    ActivityStatus,
    ActivityType,
    CaseCategory,
    CaseMessageType,
    CasePriority,
    CaseStatus,
    CropPeriodStatus,
    CropType,
    DevicePlatform,
    FeedbackStatus,
    FeedbackType,
    IrrigationMethod,
    MediaKind,
    NotificationStatus,
    NotificationType,
    TaskConfidence,
    TaskPriority,
    TaskSource,
    TaskStatus,
    UserRole,
)


PhoneNumber = Annotated[str, Field(examples=["+905551234567"])]


def normalize_phone(value: str) -> str:
    try:
        parsed = phonenumbers.parse(value, None)
    except phonenumbers.NumberParseException as exc:
        raise ValueError("Telefon numarası uluslararası biçimde olmalıdır.") from exc
    if not phonenumbers.is_valid_number(parsed):
        raise ValueError("Geçerli bir telefon numarası girin.")
    return phonenumbers.format_number(parsed, phonenumbers.PhoneNumberFormat.E164)


class RequestOtpRequest(BaseModel):
    phone_number: PhoneNumber

    _normalize = field_validator("phone_number")(normalize_phone)


class RequestOtpResponse(BaseModel):
    message: str = "Doğrulama kodu gönderildi."
    expires_in: int
    debug_otp: str | None = Field(
        default=None,
        description="Yalnızca açıkça etkinleştirilen yerel geliştirme ortamında döner.",
    )


class VerifyOtpRequest(BaseModel):
    phone_number: PhoneNumber
    otp_code: str = Field(pattern=r"^\d{6}$", examples=["123456"])

    _normalize = field_validator("phone_number")(normalize_phone)


class UserResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    phone_number: str
    full_name: str | None
    province: str | None
    district: str | None
    role: UserRole
    terms_accepted: bool
    notifications_enabled: bool
    profile_complete: bool


class TokenResponse(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"
    expires_in: int
    user: UserResponse


class RefreshTokenRequest(BaseModel):
    refresh_token: str = Field(min_length=32)


class ProfileUpdate(BaseModel):
    full_name: str = Field(min_length=2, max_length=120)
    province: str = Field(min_length=2, max_length=80)
    district: str = Field(min_length=2, max_length=80)
    terms_accepted: bool
    notifications_enabled: bool = True

    @field_validator("full_name", "province", "district")
    @classmethod
    def strip_text(cls, value: str) -> str:
        return value.strip()

    @field_validator("terms_accepted")
    @classmethod
    def require_terms(cls, value: bool) -> bool:
        if not value:
            raise ValueError("Kullanım koşulları kabul edilmelidir.")
        return value


class CropPeriodResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    farm_id: uuid.UUID
    crop_type: CropType
    variety: str | None
    planted_at: date
    harvested_at: date | None
    status: CropPeriodStatus
    created_at: datetime
    updated_at: datetime


class FarmCreate(BaseModel):
    name: str = Field(min_length=2, max_length=120)
    latitude: float = Field(ge=-90, le=90)
    longitude: float = Field(ge=-180, le=180)
    size_in_hectares: float | None = Field(default=None, gt=0, le=1_000_000)
    irrigation_method: IrrigationMethod | None = None
    soil_type: str | None = Field(default=None, max_length=80)
    note: str | None = Field(default=None, max_length=1000)
    crop_type: CropType
    variety: str | None = Field(default=None, max_length=120)
    planted_at: date

    @field_validator("name", "soil_type", "note", "variety")
    @classmethod
    def strip_optional_text(cls, value: str | None) -> str | None:
        if value is None:
            return None
        stripped = value.strip()
        return stripped or None

    @field_validator("name")
    @classmethod
    def require_name(cls, value: str | None) -> str:
        if not value:
            raise ValueError("Tarla adı boş olamaz.")
        return value

    @field_validator("planted_at")
    @classmethod
    def reject_future_planting_date(cls, value: date) -> date:
        if value > date.today():
            raise ValueError("Ekim tarihi gelecekte olamaz.")
        return value


class FarmUpdate(BaseModel):
    name: str | None = Field(default=None, min_length=2, max_length=120)
    latitude: float | None = Field(default=None, ge=-90, le=90)
    longitude: float | None = Field(default=None, ge=-180, le=180)
    size_in_hectares: float | None = Field(default=None, gt=0, le=1_000_000)
    irrigation_method: IrrigationMethod | None = None
    soil_type: str | None = Field(default=None, max_length=80)
    note: str | None = Field(default=None, max_length=1000)

    @field_validator("name", "soil_type", "note")
    @classmethod
    def strip_optional_text(cls, value: str | None) -> str | None:
        if value is None:
            return None
        stripped = value.strip()
        return stripped or None

    @model_validator(mode="after")
    def require_values_for_location_and_name(self) -> "FarmUpdate":
        for field_name in ("name", "latitude", "longitude"):
            if (
                field_name in self.model_fields_set
                and getattr(self, field_name) is None
            ):
                raise ValueError(f"{field_name} null olamaz.")
        return self


class FarmResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    owner_id: uuid.UUID
    name: str
    latitude: float | None
    longitude: float | None
    size_in_hectares: float | None
    irrigation_method: IrrigationMethod | None
    soil_type: str | None
    note: str | None
    archived_at: datetime | None
    created_at: datetime
    updated_at: datetime
    current_crop: CropPeriodResponse | None


class FarmMutationResponse(BaseModel):
    farm: FarmResponse
    warnings: list[str] = Field(default_factory=list)


class FarmListResponse(BaseModel):
    items: list[FarmResponse]
    total: int
    limit: int
    offset: int


class CropPeriodCreate(BaseModel):
    crop_type: CropType
    variety: str | None = Field(default=None, max_length=120)
    planted_at: date
    close_existing: bool = False

    @field_validator("variety")
    @classmethod
    def strip_variety(cls, value: str | None) -> str | None:
        if value is None:
            return None
        return value.strip() or None

    @field_validator("planted_at")
    @classmethod
    def reject_future_planting_date(cls, value: date) -> date:
        if value > date.today():
            raise ValueError("Ekim tarihi gelecekte olamaz.")
        return value


class CropPeriodClose(BaseModel):
    harvested_at: date = Field(default_factory=date.today)

    @field_validator("harvested_at")
    @classmethod
    def reject_future_harvest_date(cls, value: date) -> date:
        if value > date.today():
            raise ValueError("Hasat tarihi gelecekte olamaz.")
        return value


class CropPeriodListResponse(BaseModel):
    items: list[CropPeriodResponse]


class WeatherPointResponse(BaseModel):
    observed_at: datetime
    temperature_c: float | None
    precipitation_probability: float | None
    precipitation_mm: float | None
    wind_speed_kmh: float | None


class WeatherRiskResponse(BaseModel):
    risk_type: str
    severity: str
    starts_at: datetime
    ends_at: datetime
    message: str
    suggested_action: str


class FarmWeatherResponse(BaseModel):
    farm_id: uuid.UUID
    provider: str
    fetched_at: datetime
    is_stale: bool
    stale_reason: str | None
    points: list[WeatherPointResponse]
    risks: list[WeatherRiskResponse]


class TaskCreate(BaseModel):
    title: str = Field(min_length=2, max_length=160)
    description: str = Field(min_length=2, max_length=4000)
    reason: str = Field(min_length=2, max_length=2000)
    priority: TaskPriority = TaskPriority.HIGH
    confidence: TaskConfidence = TaskConfidence.HIGH
    due_date: date = Field(default_factory=date.today)
    crop_period_id: uuid.UUID | None = None

    @field_validator("title", "description", "reason")
    @classmethod
    def strip_required_text(cls, value: str) -> str:
        stripped = value.strip()
        if not stripped:
            raise ValueError("Metin alanı boş olamaz.")
        return stripped


class TaskResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    farm_id: uuid.UUID
    crop_period_id: uuid.UUID | None
    created_by_id: uuid.UUID | None
    title: str
    description: str
    reason: str
    priority: TaskPriority
    status: TaskStatus
    source: TaskSource
    confidence: TaskConfidence
    due_date: date
    not_applied_reason: str | None
    completion_note: str | None
    photo_url: str | None
    viewed_at: datetime | None
    completed_at: datetime | None
    created_at: datetime
    updated_at: datetime
    expert_review_recommended: bool


class DailyTaskListResponse(BaseModel):
    date: date
    items: list[TaskResponse]
    critical_weather_alerts: list[TaskResponse]
    overdue: list[TaskResponse]
    visible_limit: int = 3


class TaskStatusUpdate(BaseModel):
    status: TaskStatus
    not_applied_reason: str | None = Field(default=None, max_length=500)
    note: str | None = Field(default=None, max_length=1000)
    photo_url: str | None = Field(default=None, max_length=2048)

    @field_validator("not_applied_reason", "note", "photo_url")
    @classmethod
    def strip_optional_task_text(cls, value: str | None) -> str | None:
        return value.strip() or None if value is not None else None

    @model_validator(mode="after")
    def validate_transition_payload(self) -> "TaskStatusUpdate":
        allowed = {
            TaskStatus.VIEWED,
            TaskStatus.PLANNED,
            TaskStatus.COMPLETED,
            TaskStatus.NOT_APPLIED,
            TaskStatus.CANCELLED,
        }
        if self.status not in allowed:
            raise ValueError("Bu görev durumuna doğrudan geçilemez.")
        if self.status == TaskStatus.NOT_APPLIED and not self.not_applied_reason:
            raise ValueError("Uygulanmama nedeni zorunludur.")
        return self


class TaskCompleteRequest(BaseModel):
    note: str | None = Field(default=None, max_length=1000)
    photo_url: str | None = Field(default=None, max_length=2048)

    @field_validator("note", "photo_url")
    @classmethod
    def strip_optional_completion_text(cls, value: str | None) -> str | None:
        return value.strip() or None if value is not None else None


class ActivityCreate(BaseModel):
    client_operation_id: uuid.UUID | None = Field(
        default=None,
        description="Çevrimdışı tekrar gönderimlerini güvenle tekilleştiren istemci işlem kimliği.",
    )
    activity_type: ActivityType
    description: str = Field(min_length=2, max_length=4000)
    occurred_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    crop_period_id: uuid.UUID | None = None
    input_method: ActivitySource = ActivitySource.MANUAL
    duration_minutes: int | None = Field(default=None, gt=0, le=100_000)
    amount: float | None = Field(default=None, gt=0, le=1_000_000_000)
    unit: str | None = Field(default=None, max_length=40)
    photo_url: str | None = Field(default=None, max_length=2048)
    voice_url: str | None = Field(default=None, max_length=2048)
    voice_transcript: str | None = Field(default=None, max_length=10_000)
    performed_by: str | None = Field(default=None, max_length=120)
    cost: float | None = Field(default=None, ge=0, le=1_000_000_000)

    @field_validator(
        "description",
        "unit",
        "photo_url",
        "voice_url",
        "voice_transcript",
        "performed_by",
    )
    @classmethod
    def strip_activity_text(cls, value: str | None) -> str | None:
        if value is None:
            return None
        stripped = value.strip()
        return stripped or None

    @field_validator("description")
    @classmethod
    def require_activity_description(cls, value: str | None) -> str:
        if not value:
            raise ValueError("Faaliyet açıklaması boş olamaz.")
        return value

    @field_validator("occurred_at")
    @classmethod
    def reject_future_activity(cls, value: datetime) -> datetime:
        normalized = (
            value.replace(tzinfo=timezone.utc)
            if value.tzinfo is None
            else value.astimezone(timezone.utc)
        )
        if normalized > datetime.now(timezone.utc):
            raise ValueError("Tamamlanmış faaliyet tarihi gelecekte olamaz.")
        return normalized

    @model_validator(mode="after")
    def validate_activity_source(self) -> "ActivityCreate":
        if self.input_method == ActivitySource.TASK:
            raise ValueError(
                "TASK kaynaklı faaliyet yalnızca görev tamamlama ile oluşur."
            )
        if (
            self.input_method == ActivitySource.VOICE
            and not self.voice_url
            and not self.voice_transcript
        ):
            raise ValueError("Sesli faaliyet için kayıt veya döküm gereklidir.")
        return self


class ActivityUpdate(BaseModel):
    activity_type: ActivityType | None = None
    description: str | None = Field(default=None, min_length=2, max_length=4000)
    occurred_at: datetime | None = None
    crop_period_id: uuid.UUID | None = None
    duration_minutes: int | None = Field(default=None, gt=0, le=100_000)
    amount: float | None = Field(default=None, gt=0, le=1_000_000_000)
    unit: str | None = Field(default=None, max_length=40)
    photo_url: str | None = Field(default=None, max_length=2048)
    voice_url: str | None = Field(default=None, max_length=2048)
    voice_transcript: str | None = Field(default=None, max_length=10_000)
    performed_by: str | None = Field(default=None, max_length=120)
    cost: float | None = Field(default=None, ge=0, le=1_000_000_000)

    @field_validator(
        "description",
        "unit",
        "photo_url",
        "voice_url",
        "voice_transcript",
        "performed_by",
    )
    @classmethod
    def strip_activity_update_text(cls, value: str | None) -> str | None:
        return value.strip() or None if value is not None else None

    @field_validator("occurred_at")
    @classmethod
    def reject_future_activity_update(cls, value: datetime | None) -> datetime | None:
        if value is None:
            return None
        normalized = (
            value.replace(tzinfo=timezone.utc)
            if value.tzinfo is None
            else value.astimezone(timezone.utc)
        )
        if normalized > datetime.now(timezone.utc):
            raise ValueError("Tamamlanmış faaliyet tarihi gelecekte olamaz.")
        return normalized

    @model_validator(mode="after")
    def require_activity_update(self) -> "ActivityUpdate":
        if not self.model_fields_set:
            raise ValueError("En az bir alan güncellenmelidir.")
        required_fields = ("activity_type", "description", "occurred_at")
        if any(
            field_name in self.model_fields_set and getattr(self, field_name) is None
            for field_name in required_fields
        ):
            raise ValueError(
                "Faaliyet türü, açıklaması ve gerçekleşme tarihi boş olamaz."
            )
        return self


class ActivityResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    farm_id: uuid.UUID
    crop_period_id: uuid.UUID | None
    task_id: uuid.UUID | None
    created_by_id: uuid.UUID | None
    activity_type: ActivityType
    status: ActivityStatus
    source: ActivitySource
    description: str
    occurred_at: datetime
    duration_minutes: int | None
    amount: float | None
    unit: str | None
    photo_url: str | None
    voice_url: str | None
    voice_transcript: str | None
    performed_by: str | None
    cost: float | None
    confirmed_at: datetime | None
    archived_at: datetime | None
    created_at: datetime
    updated_at: datetime


class ActivityListResponse(BaseModel):
    items: list[ActivityResponse]
    total: int
    limit: int
    offset: int


class ActivityRevisionResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    activity_id: uuid.UUID
    changed_by_id: uuid.UUID | None
    previous_values: dict
    changed_at: datetime


class JournalEntryResponse(BaseModel):
    entry_type: str
    id: uuid.UUID
    occurred_at: datetime
    title: str
    description: str
    metadata: dict[str, str | int | float | bool | None]


class FarmJournalResponse(BaseModel):
    items: list[JournalEntryResponse]
    total: int
    limit: int
    offset: int


class MediaAssetResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    owner_id: uuid.UUID
    kind: MediaKind
    original_name: str
    content_type: str
    size_bytes: int
    checksum_sha256: str
    url: str
    created_at: datetime


class CaseCreate(BaseModel):
    client_operation_id: uuid.UUID | None = None
    farm_id: uuid.UUID
    category: CaseCategory
    title: str = Field(min_length=2, max_length=160)
    description: str = Field(min_length=2, max_length=6000)
    media_ids: list[uuid.UUID] = Field(default_factory=list, max_length=10)

    @field_validator("title", "description")
    @classmethod
    def strip_case_text(cls, value: str) -> str:
        return value.strip()

    @field_validator("media_ids")
    @classmethod
    def unique_case_media(cls, value: list[uuid.UUID]) -> list[uuid.UUID]:
        if len(set(value)) != len(value):
            raise ValueError("Aynı medya birden fazla eklenemez.")
        return value


class CaseStatusUpdate(BaseModel):
    status: CaseStatus
    priority: CasePriority | None = None
    assign_to_me: bool = False


class CaseMessageCreate(BaseModel):
    client_operation_id: uuid.UUID | None = None
    message_type: CaseMessageType = CaseMessageType.COMMENT
    body: str = Field(min_length=2, max_length=6000)
    media_ids: list[uuid.UUID] = Field(default_factory=list, max_length=10)

    @field_validator("body")
    @classmethod
    def strip_message_body(cls, value: str) -> str:
        return value.strip()

    @field_validator("media_ids")
    @classmethod
    def unique_message_media(cls, value: list[uuid.UUID]) -> list[uuid.UUID]:
        if len(set(value)) != len(value):
            raise ValueError("Aynı medya birden fazla eklenemez.")
        return value


class ExpertResponseCreate(BaseModel):
    client_operation_id: uuid.UUID | None = None
    body: str = Field(min_length=2, max_length=6000)
    media_ids: list[uuid.UUID] = Field(default_factory=list, max_length=10)
    close_case: bool = False

    @field_validator("body")
    @classmethod
    def strip_expert_response(cls, value: str) -> str:
        return value.strip()


class CaseMessageResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    case_id: uuid.UUID
    sender_id: uuid.UUID
    sender_name: str
    sender_role: UserRole
    message_type: CaseMessageType
    body: str
    media: list[MediaAssetResponse]
    created_at: datetime


class CaseSummaryResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    farm_id: uuid.UUID
    farm_name: str
    farmer_name: str
    created_by_id: uuid.UUID
    assigned_expert_id: uuid.UUID | None
    category: CaseCategory
    priority: CasePriority
    status: CaseStatus
    title: str
    description: str
    media: list[MediaAssetResponse]
    closed_at: datetime | None
    created_at: datetime
    updated_at: datetime


class CaseDetailResponse(CaseSummaryResponse):
    messages: list[CaseMessageResponse]


class CaseListResponse(BaseModel):
    items: list[CaseSummaryResponse]
    total: int
    limit: int
    offset: int


class DeviceTokenRegister(BaseModel):
    token: str = Field(min_length=16, max_length=512)
    platform: DevicePlatform

    @field_validator("token")
    @classmethod
    def strip_device_token(cls, value: str) -> str:
        return value.strip()


class DeviceTokenResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    user_id: uuid.UUID
    platform: DevicePlatform
    active: bool
    last_seen_at: datetime
    created_at: datetime


class NotificationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    user_id: uuid.UUID
    notification_type: NotificationType
    title: str
    body: str
    deep_link: str
    data: dict
    status: NotificationStatus
    attempt_count: int
    last_error: str | None
    sent_at: datetime | None
    read_at: datetime | None
    created_at: datetime


class NotificationListResponse(BaseModel):
    items: list[NotificationResponse]
    total: int
    unread: int
    limit: int
    offset: int


class PilotFeedbackCreate(BaseModel):
    feedback_type: FeedbackType
    rating: int | None = Field(default=None, ge=1, le=5)
    comment: str = Field(min_length=2, max_length=6000)
    related_task_id: uuid.UUID | None = None
    related_case_id: uuid.UUID | None = None

    @field_validator("comment")
    @classmethod
    def strip_feedback_comment(cls, value: str) -> str:
        return value.strip()

    @model_validator(mode="after")
    def validate_feedback_context(self) -> "PilotFeedbackCreate":
        if self.feedback_type == FeedbackType.WEEKLY_CHECKIN and self.rating is None:
            raise ValueError("Haftalık geri bildirim için puan zorunludur.")
        if (
            self.feedback_type == FeedbackType.FALSE_ALERT
            and self.related_task_id is None
        ):
            raise ValueError("Yanlış uyarı kaydı için görev kimliği zorunludur.")
        return self


class PilotFeedbackUpdate(BaseModel):
    status: FeedbackStatus


class PilotFeedbackResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    created_by_id: uuid.UUID
    created_by_name: str
    feedback_type: FeedbackType
    status: FeedbackStatus
    rating: int | None
    comment: str
    related_task_id: uuid.UUID | None
    related_case_id: uuid.UUID | None
    reviewed_by_id: uuid.UUID | None
    reviewed_at: datetime | None
    created_at: datetime
    updated_at: datetime


class PilotFeedbackListResponse(BaseModel):
    items: list[PilotFeedbackResponse]
    total: int
    limit: int
    offset: int


class PilotMetricsResponse(BaseModel):
    window_days: int
    active_farmers: int
    tasks_created: int
    tasks_completed: int
    task_completion_rate: float
    critical_weather_alerts: int
    false_alerts: int
    false_alert_rate: float
    cases_created: int
    cases_answered: int
    average_expert_response_minutes: float | None
    notifications_created: int
    notifications_sent: int
    notification_delivery_rate: float
    feedback_count: int
    average_feedback_rating: float | None

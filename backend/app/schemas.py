import uuid
from datetime import date, datetime
from typing import Annotated

import phonenumbers
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from app.models import (
    CropPeriodStatus,
    CropType,
    IrrigationMethod,
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
            if field_name in self.model_fields_set and getattr(self, field_name) is None:
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

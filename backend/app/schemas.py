import uuid
from typing import Annotated

import phonenumbers
from pydantic import BaseModel, ConfigDict, Field, field_validator

from app.models import UserRole


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


class FarmResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    owner_id: uuid.UUID
    name: str
    latitude: float | None
    longitude: float | None
    size_in_hectares: float | None

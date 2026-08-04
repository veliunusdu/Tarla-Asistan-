from functools import lru_cache
from typing import Annotated

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, NoDecode, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    app_name: str = "Tarla Asistanı API"
    environment: str = "local"
    api_v1_prefix: str = "/api/v1"
    database_url: str = "postgresql+psycopg://tarla:tarla-local@localhost:5432/tarla_asistani"
    redis_url: str = "redis://localhost:6379/0"
    jwt_secret: str = Field(default="local-only-jwt-secret-change-before-deploy", min_length=32)
    jwt_algorithm: str = "HS256"
    access_token_expire_minutes: int = 60
    refresh_token_expire_days: int = 30
    otp_hash_secret: str = Field(default="local-only-otp-secret-change-before-deploy", min_length=32)
    otp_ttl_seconds: int = 180
    otp_max_attempts: int = 5
    otp_request_cooldown_seconds: int = 60
    otp_expose_in_response: bool = False
    agronomist_phone_numbers: Annotated[list[str], NoDecode] = Field(
        default_factory=list
    )
    cors_origins: Annotated[list[str], NoDecode] = Field(
        default_factory=lambda: ["http://localhost:3000"]
    )
    weather_provider: str = "open_meteo"
    open_meteo_base_url: str = "https://api.open-meteo.com/v1/forecast"
    weather_timeout_seconds: float = Field(default=8, gt=0, le=30)
    weather_stale_after_hours: int = Field(default=3, ge=1, le=24)
    media_storage_path: str = "data/media"
    media_max_upload_mb: int = Field(default=15, ge=1, le=100)

    @field_validator("cors_origins", "agronomist_phone_numbers", mode="before")
    @classmethod
    def parse_csv(cls, value: object) -> object:
        if isinstance(value, str):
            return [item.strip() for item in value.split(",") if item.strip()]
        return value


@lru_cache
def get_settings() -> Settings:
    return Settings()

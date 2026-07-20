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

    @field_validator("cors_origins", "agronomist_phone_numbers", mode="before")
    @classmethod
    def parse_csv(cls, value: object) -> object:
        if isinstance(value, str):
            return [item.strip() for item in value.split(",") if item.strip()]
        return value


@lru_cache
def get_settings() -> Settings:
    return Settings()

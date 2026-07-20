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
    String,
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


class User(Base):
    __tablename__ = "users"
    __table_args__ = (Index("ix_users_phone_number", "phone_number"),)

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    phone_number: Mapped[str] = mapped_column(String(20), unique=True)
    role: Mapped[UserRole] = mapped_column(
        Enum(UserRole, name="user_role"), default=UserRole.FARMER, index=True
    )
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
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
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
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
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)

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
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
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
    crop_type: Mapped[CropType] = mapped_column(
        Enum(CropType, name="crop_type")
    )
    variety: Mapped[str | None] = mapped_column(String(120))
    planted_at: Mapped[date] = mapped_column(Date)
    harvested_at: Mapped[date | None] = mapped_column(Date)
    status: Mapped[CropPeriodStatus] = mapped_column(
        Enum(CropPeriodStatus, name="crop_period_status"),
        default=CropPeriodStatus.ACTIVE,
    )
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
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
    fetched_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)

    farm: Mapped[Farm] = relationship(back_populates="weather_snapshots")

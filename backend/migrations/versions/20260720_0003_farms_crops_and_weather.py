"""add farm details, crop periods and weather snapshots

Revision ID: 20260720_0003
Revises: 20260720_0002
Create Date: 2026-07-20
"""
from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "20260720_0003"
down_revision: str | None = "20260720_0002"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

irrigation_method = postgresql.ENUM(
    "DRIP",
    "SPRINKLER",
    "FLOOD",
    "RAINFED",
    "OTHER",
    name="irrigation_method",
    create_type=False,
)
crop_type = postgresql.ENUM(
    "WHEAT",
    "BARLEY",
    "CORN",
    "SUNFLOWER",
    "TOMATO",
    name="crop_type",
    create_type=False,
)
crop_period_status = postgresql.ENUM(
    "ACTIVE",
    "ARCHIVED",
    name="crop_period_status",
    create_type=False,
)


def upgrade() -> None:
    bind = op.get_bind()
    irrigation_method.create(bind, checkfirst=True)
    crop_type.create(bind, checkfirst=True)
    crop_period_status.create(bind, checkfirst=True)

    op.add_column(
        "farms",
        sa.Column("irrigation_method", irrigation_method, nullable=True),
    )
    op.add_column("farms", sa.Column("soil_type", sa.String(length=80), nullable=True))
    op.add_column("farms", sa.Column("note", sa.String(length=1000), nullable=True))
    op.create_index(
        "ix_farms_owner_archived",
        "farms",
        ["owner_id", "archived_at"],
    )
    op.create_index("ix_farms_owner_name", "farms", ["owner_id", "name"])

    op.create_table(
        "crop_periods",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("farm_id", sa.Uuid(), nullable=False),
        sa.Column("crop_type", crop_type, nullable=False),
        sa.Column("variety", sa.String(length=120), nullable=True),
        sa.Column("planted_at", sa.Date(), nullable=False),
        sa.Column("harvested_at", sa.Date(), nullable=True),
        sa.Column(
            "status",
            crop_period_status,
            server_default="ACTIVE",
            nullable=False,
        ),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(["farm_id"], ["farms.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_crop_periods_farm_id", "crop_periods", ["farm_id"])
    op.create_index(
        "ix_crop_periods_farm_status",
        "crop_periods",
        ["farm_id", "status"],
    )
    op.create_index(
        "ix_crop_periods_farm_planted",
        "crop_periods",
        ["farm_id", "planted_at"],
    )
    op.create_index(
        "uq_crop_periods_one_active_per_farm",
        "crop_periods",
        ["farm_id"],
        unique=True,
        postgresql_where=sa.text("status = 'ACTIVE'"),
    )

    op.create_table(
        "weather_snapshots",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("farm_id", sa.Uuid(), nullable=False),
        sa.Column("provider", sa.String(length=50), nullable=False),
        sa.Column("payload", sa.JSON(), nullable=False),
        sa.Column(
            "fetched_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(["farm_id"], ["farms.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_weather_snapshots_farm_id", "weather_snapshots", ["farm_id"])
    op.create_index(
        "ix_weather_snapshots_farm_fetched",
        "weather_snapshots",
        ["farm_id", "fetched_at"],
    )


def downgrade() -> None:
    op.drop_index(
        "ix_weather_snapshots_farm_fetched",
        table_name="weather_snapshots",
    )
    op.drop_index("ix_weather_snapshots_farm_id", table_name="weather_snapshots")
    op.drop_table("weather_snapshots")

    op.drop_index(
        "uq_crop_periods_one_active_per_farm",
        table_name="crop_periods",
    )
    op.drop_index("ix_crop_periods_farm_planted", table_name="crop_periods")
    op.drop_index("ix_crop_periods_farm_status", table_name="crop_periods")
    op.drop_index("ix_crop_periods_farm_id", table_name="crop_periods")
    op.drop_table("crop_periods")

    op.drop_index("ix_farms_owner_name", table_name="farms")
    op.drop_index("ix_farms_owner_archived", table_name="farms")
    op.drop_column("farms", "note")
    op.drop_column("farms", "soil_type")
    op.drop_column("farms", "irrigation_method")

    crop_period_status.drop(op.get_bind(), checkfirst=True)
    crop_type.drop(op.get_bind(), checkfirst=True)
    irrigation_method.drop(op.get_bind(), checkfirst=True)

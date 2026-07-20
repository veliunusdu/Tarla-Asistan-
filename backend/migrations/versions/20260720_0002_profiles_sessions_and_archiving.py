"""create profiles and refresh sessions

Revision ID: 20260720_0002
Revises: 20260720_0001
Create Date: 2026-07-20
"""
from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

revision: str = "20260720_0002"
down_revision: str | None = "20260720_0001"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.create_table(
        "profiles",
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("full_name", sa.String(length=120), nullable=False),
        sa.Column("province", sa.String(length=80), nullable=False),
        sa.Column("district", sa.String(length=80), nullable=False),
        sa.Column("terms_accepted", sa.Boolean(), server_default=sa.false(), nullable=False),
        sa.Column(
            "notifications_enabled", sa.Boolean(), server_default=sa.true(), nullable=False
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
        sa.ForeignKeyConstraint(["user_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("user_id"),
    )
    op.execute(
        """
        INSERT INTO profiles (
            user_id, full_name, province, district, terms_accepted,
            notifications_enabled, created_at, updated_at
        )
        SELECT
            id, full_name, province, district, terms_accepted,
            notifications_enabled, created_at, updated_at
        FROM users
        WHERE full_name IS NOT NULL
          AND province IS NOT NULL
          AND district IS NOT NULL
        """
    )
    op.drop_column("users", "notifications_enabled")
    op.drop_column("users", "terms_accepted")
    op.drop_column("users", "district")
    op.drop_column("users", "province")
    op.drop_column("users", "full_name")

    op.add_column(
        "farms", sa.Column("archived_at", sa.DateTime(timezone=True), nullable=True)
    )
    op.create_table(
        "refresh_tokens",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("family_id", sa.Uuid(), nullable=False),
        sa.Column("token_hash", sa.String(length=64), nullable=False),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("revoked_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(["user_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("token_hash"),
    )
    op.create_index("ix_refresh_tokens_user_id", "refresh_tokens", ["user_id"])
    op.create_index("ix_refresh_tokens_family_id", "refresh_tokens", ["family_id"])
    op.create_index("ix_refresh_tokens_token_hash", "refresh_tokens", ["token_hash"])


def downgrade() -> None:
    op.drop_index("ix_refresh_tokens_token_hash", table_name="refresh_tokens")
    op.drop_index("ix_refresh_tokens_family_id", table_name="refresh_tokens")
    op.drop_index("ix_refresh_tokens_user_id", table_name="refresh_tokens")
    op.drop_table("refresh_tokens")
    op.drop_column("farms", "archived_at")

    op.add_column("users", sa.Column("full_name", sa.String(length=120), nullable=True))
    op.add_column("users", sa.Column("province", sa.String(length=80), nullable=True))
    op.add_column("users", sa.Column("district", sa.String(length=80), nullable=True))
    op.add_column(
        "users",
        sa.Column(
            "terms_accepted", sa.Boolean(), server_default=sa.false(), nullable=False
        ),
    )
    op.add_column(
        "users",
        sa.Column(
            "notifications_enabled",
            sa.Boolean(),
            server_default=sa.true(),
            nullable=False,
        ),
    )
    op.execute(
        """
        UPDATE users
        SET full_name = profiles.full_name,
            province = profiles.province,
            district = profiles.district,
            terms_accepted = profiles.terms_accepted,
            notifications_enabled = profiles.notifications_enabled
        FROM profiles
        WHERE users.id = profiles.user_id
        """
    )
    op.drop_table("profiles")

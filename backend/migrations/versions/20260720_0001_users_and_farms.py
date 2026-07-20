"""create users and farms

Revision ID: 20260720_0001
Revises:
Create Date: 2026-07-20
"""
from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

revision: str = "20260720_0001"
down_revision: str | None = None
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

user_role = sa.Enum("FARMER", "AGRONOMIST", name="user_role")


def upgrade() -> None:
    op.create_table(
        "users",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("phone_number", sa.String(length=20), nullable=False),
        sa.Column("full_name", sa.String(length=120), nullable=True),
        sa.Column("province", sa.String(length=80), nullable=True),
        sa.Column("district", sa.String(length=80), nullable=True),
        sa.Column("role", user_role, nullable=False),
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
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("phone_number"),
    )
    op.create_index("ix_users_phone_number", "users", ["phone_number"])
    op.create_index("ix_users_role", "users", ["role"])
    op.create_table(
        "farms",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("owner_id", sa.Uuid(), nullable=False),
        sa.Column("name", sa.String(length=120), nullable=False),
        sa.Column("latitude", sa.Float(), nullable=True),
        sa.Column("longitude", sa.Float(), nullable=True),
        sa.Column("size_in_hectares", sa.Float(), nullable=True),
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
        sa.ForeignKeyConstraint(["owner_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_farms_owner_id", "farms", ["owner_id"])


def downgrade() -> None:
    op.drop_index("ix_farms_owner_id", table_name="farms")
    op.drop_table("farms")
    op.drop_index("ix_users_role", table_name="users")
    op.drop_index("ix_users_phone_number", table_name="users")
    op.drop_table("users")
    user_role.drop(op.get_bind(), checkfirst=True)

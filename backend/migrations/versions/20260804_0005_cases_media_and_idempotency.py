"""add cases, secure media and client idempotency

Revision ID: 20260804_0005
Revises: 20260720_0004
Create Date: 2026-08-04
"""
from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "20260804_0005"
down_revision: str | None = "20260720_0004"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

media_kind = postgresql.ENUM("IMAGE", "AUDIO", name="media_kind", create_type=False)
case_category = postgresql.ENUM(
    "DISEASE", "PEST", "IRRIGATION", "NUTRITION", "WEATHER", "OTHER",
    name="support_case_category", create_type=False,
)
case_priority = postgresql.ENUM(
    "LOW", "MEDIUM", "HIGH", "CRITICAL",
    name="support_case_priority", create_type=False,
)
case_status = postgresql.ENUM(
    "OPEN", "IN_REVIEW", "WAITING_FARMER", "ANSWERED", "CLOSED",
    name="support_case_status", create_type=False,
)
message_type = postgresql.ENUM(
    "COMMENT", "ADDITIONAL_INFO_REQUEST", "EXPERT_RESPONSE",
    name="case_message_type", create_type=False,
)


def upgrade() -> None:
    bind = op.get_bind()
    for enum_type in (media_kind, case_category, case_priority, case_status, message_type):
        enum_type.create(bind, checkfirst=True)

    op.create_table(
        "media_assets",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("owner_id", sa.Uuid(), nullable=False),
        sa.Column("kind", media_kind, nullable=False),
        sa.Column("original_name", sa.String(length=255), nullable=False),
        sa.Column("content_type", sa.String(length=100), nullable=False),
        sa.Column("size_bytes", sa.Integer(), nullable=False),
        sa.Column("storage_key", sa.String(length=255), nullable=False),
        sa.Column("checksum_sha256", sa.String(length=64), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["owner_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("storage_key"),
    )
    op.create_index("ix_media_assets_owner_id", "media_assets", ["owner_id"])
    op.create_index("ix_media_assets_owner_created", "media_assets", ["owner_id", "created_at"])

    op.create_table(
        "support_cases",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("farm_id", sa.Uuid(), nullable=False),
        sa.Column("created_by_id", sa.Uuid(), nullable=False),
        sa.Column("assigned_expert_id", sa.Uuid(), nullable=True),
        sa.Column("category", case_category, nullable=False),
        sa.Column("priority", case_priority, server_default="MEDIUM", nullable=False),
        sa.Column("status", case_status, server_default="OPEN", nullable=False),
        sa.Column("title", sa.String(length=160), nullable=False),
        sa.Column("description", sa.Text(), nullable=False),
        sa.Column("closed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["farm_id"], ["farms.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["created_by_id"], ["users.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["assigned_expert_id"], ["users.id"], ondelete="SET NULL"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_support_cases_farm_id", "support_cases", ["farm_id"])
    op.create_index("ix_support_cases_created_by_id", "support_cases", ["created_by_id"])
    op.create_index("ix_support_cases_assigned_expert_id", "support_cases", ["assigned_expert_id"])
    op.create_index("ix_support_cases_status_priority", "support_cases", ["status", "priority"])
    op.create_index("ix_support_cases_farm_created", "support_cases", ["farm_id", "created_at"])

    op.create_table(
        "case_messages",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("case_id", sa.Uuid(), nullable=False),
        sa.Column("sender_id", sa.Uuid(), nullable=False),
        sa.Column("message_type", message_type, server_default="COMMENT", nullable=False),
        sa.Column("body", sa.Text(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["case_id"], ["support_cases.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["sender_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_case_messages_case_id", "case_messages", ["case_id"])
    op.create_index("ix_case_messages_sender_id", "case_messages", ["sender_id"])
    op.create_index("ix_case_messages_case_created", "case_messages", ["case_id", "created_at"])

    op.create_table(
        "case_media",
        sa.Column("case_id", sa.Uuid(), nullable=False),
        sa.Column("media_id", sa.Uuid(), nullable=False),
        sa.ForeignKeyConstraint(["case_id"], ["support_cases.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["media_id"], ["media_assets.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("case_id", "media_id"),
    )
    op.create_table(
        "case_message_media",
        sa.Column("message_id", sa.Uuid(), nullable=False),
        sa.Column("media_id", sa.Uuid(), nullable=False),
        sa.ForeignKeyConstraint(["message_id"], ["case_messages.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["media_id"], ["media_assets.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("message_id", "media_id"),
    )
    op.create_table(
        "client_operations",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("actor_id", sa.Uuid(), nullable=False),
        sa.Column("client_operation_id", sa.Uuid(), nullable=False),
        sa.Column("scope", sa.String(length=80), nullable=False),
        sa.Column("payload_hash", sa.String(length=64), nullable=False),
        sa.Column("resource_type", sa.String(length=50), nullable=False),
        sa.Column("resource_id", sa.Uuid(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["actor_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("actor_id", "client_operation_id", name="uq_client_operations_actor_key"),
    )
    op.create_index("ix_client_operations_actor_id", "client_operations", ["actor_id"])
    op.create_index("ix_client_operations_actor_created", "client_operations", ["actor_id", "created_at"])


def downgrade() -> None:
    op.drop_index("ix_client_operations_actor_created", table_name="client_operations")
    op.drop_index("ix_client_operations_actor_id", table_name="client_operations")
    op.drop_table("client_operations")
    op.drop_table("case_message_media")
    op.drop_table("case_media")
    op.drop_index("ix_case_messages_case_created", table_name="case_messages")
    op.drop_index("ix_case_messages_sender_id", table_name="case_messages")
    op.drop_index("ix_case_messages_case_id", table_name="case_messages")
    op.drop_table("case_messages")
    op.drop_index("ix_support_cases_farm_created", table_name="support_cases")
    op.drop_index("ix_support_cases_status_priority", table_name="support_cases")
    op.drop_index("ix_support_cases_assigned_expert_id", table_name="support_cases")
    op.drop_index("ix_support_cases_created_by_id", table_name="support_cases")
    op.drop_index("ix_support_cases_farm_id", table_name="support_cases")
    op.drop_table("support_cases")
    op.drop_index("ix_media_assets_owner_created", table_name="media_assets")
    op.drop_index("ix_media_assets_owner_id", table_name="media_assets")
    op.drop_table("media_assets")
    for enum_type in (message_type, case_status, case_priority, case_category, media_kind):
        enum_type.drop(op.get_bind(), checkfirst=True)

"""add push notifications and pilot feedback metrics

Revision ID: 20260805_0006
Revises: 20260804_0005
Create Date: 2026-08-05
"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "20260805_0006"
down_revision: str | None = "20260804_0005"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

device_platform = postgresql.ENUM(
    "ANDROID", "IOS", "WEB", name="device_platform", create_type=False
)
notification_type = postgresql.ENUM(
    "TASK_ASSIGNED",
    "CRITICAL_WEATHER",
    "EXPERT_RESPONSE",
    name="notification_type",
    create_type=False,
)
notification_status = postgresql.ENUM(
    "PENDING", "SENT", "FAILED", name="notification_status", create_type=False
)
feedback_type = postgresql.ENUM(
    "WEEKLY_CHECKIN",
    "FALSE_ALERT",
    "BUG",
    "SUGGESTION",
    name="feedback_type",
    create_type=False,
)
feedback_status = postgresql.ENUM(
    "OPEN", "REVIEWED", "RESOLVED", name="feedback_status", create_type=False
)


def upgrade() -> None:
    bind = op.get_bind()
    for enum_type in (
        device_platform,
        notification_type,
        notification_status,
        feedback_type,
        feedback_status,
    ):
        enum_type.create(bind, checkfirst=True)

    op.create_table(
        "device_tokens",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("token", sa.String(length=512), nullable=False),
        sa.Column("platform", device_platform, nullable=False),
        sa.Column("active", sa.Boolean(), server_default=sa.true(), nullable=False),
        sa.Column(
            "last_seen_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
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
        sa.ForeignKeyConstraint(["user_id"], ["users.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("token"),
    )
    op.create_index("ix_device_tokens_user_id", "device_tokens", ["user_id"])
    op.create_index(
        "ix_device_tokens_user_active", "device_tokens", ["user_id", "active"]
    )

    op.create_table(
        "notifications",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("notification_type", notification_type, nullable=False),
        sa.Column("title", sa.String(length=160), nullable=False),
        sa.Column("body", sa.String(length=1000), nullable=False),
        sa.Column("deep_link", sa.String(length=500), nullable=False),
        sa.Column("data", sa.JSON(), nullable=False),
        sa.Column("dedupe_key", sa.String(length=160), nullable=False),
        sa.Column(
            "status", notification_status, server_default="PENDING", nullable=False
        ),
        sa.Column("provider_message_id", sa.String(length=255), nullable=True),
        sa.Column("attempt_count", sa.Integer(), server_default="0", nullable=False),
        sa.Column("last_error", sa.String(length=1000), nullable=True),
        sa.Column("sent_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("read_at", sa.DateTime(timezone=True), nullable=True),
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
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("dedupe_key"),
    )
    op.create_index("ix_notifications_user_id", "notifications", ["user_id"])
    op.create_index(
        "ix_notifications_user_created", "notifications", ["user_id", "created_at"]
    )
    op.create_index(
        "ix_notifications_status_created", "notifications", ["status", "created_at"]
    )

    op.create_table(
        "pilot_feedback",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("created_by_id", sa.Uuid(), nullable=False),
        sa.Column("feedback_type", feedback_type, nullable=False),
        sa.Column("status", feedback_status, server_default="OPEN", nullable=False),
        sa.Column("rating", sa.Integer(), nullable=True),
        sa.Column("comment", sa.Text(), nullable=False),
        sa.Column("related_task_id", sa.Uuid(), nullable=True),
        sa.Column("related_case_id", sa.Uuid(), nullable=True),
        sa.Column("reviewed_by_id", sa.Uuid(), nullable=True),
        sa.Column("reviewed_at", sa.DateTime(timezone=True), nullable=True),
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
        sa.ForeignKeyConstraint(["created_by_id"], ["users.id"], ondelete="CASCADE"),
        sa.ForeignKeyConstraint(["related_task_id"], ["tasks.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(
            ["related_case_id"], ["support_cases.id"], ondelete="SET NULL"
        ),
        sa.ForeignKeyConstraint(["reviewed_by_id"], ["users.id"], ondelete="SET NULL"),
        sa.PrimaryKeyConstraint("id"),
        sa.CheckConstraint(
            "rating IS NULL OR (rating >= 1 AND rating <= 5)",
            name="ck_pilot_feedback_rating",
        ),
    )
    op.create_index(
        "ix_pilot_feedback_created_by_id", "pilot_feedback", ["created_by_id"]
    )
    op.create_index(
        "ix_pilot_feedback_related_task_id", "pilot_feedback", ["related_task_id"]
    )
    op.create_index(
        "ix_pilot_feedback_related_case_id", "pilot_feedback", ["related_case_id"]
    )
    op.create_index(
        "ix_pilot_feedback_reviewed_by_id", "pilot_feedback", ["reviewed_by_id"]
    )
    op.create_index(
        "ix_pilot_feedback_type_created",
        "pilot_feedback",
        ["feedback_type", "created_at"],
    )
    op.create_index(
        "ix_pilot_feedback_status_created", "pilot_feedback", ["status", "created_at"]
    )


def downgrade() -> None:
    op.drop_index("ix_pilot_feedback_status_created", table_name="pilot_feedback")
    op.drop_index("ix_pilot_feedback_type_created", table_name="pilot_feedback")
    op.drop_index("ix_pilot_feedback_reviewed_by_id", table_name="pilot_feedback")
    op.drop_index("ix_pilot_feedback_related_case_id", table_name="pilot_feedback")
    op.drop_index("ix_pilot_feedback_related_task_id", table_name="pilot_feedback")
    op.drop_index("ix_pilot_feedback_created_by_id", table_name="pilot_feedback")
    op.drop_table("pilot_feedback")
    op.drop_index("ix_notifications_status_created", table_name="notifications")
    op.drop_index("ix_notifications_user_created", table_name="notifications")
    op.drop_index("ix_notifications_user_id", table_name="notifications")
    op.drop_table("notifications")
    op.drop_index("ix_device_tokens_user_active", table_name="device_tokens")
    op.drop_index("ix_device_tokens_user_id", table_name="device_tokens")
    op.drop_table("device_tokens")
    for enum_type in (
        feedback_status,
        feedback_type,
        notification_status,
        notification_type,
        device_platform,
    ):
        enum_type.drop(op.get_bind(), checkfirst=True)

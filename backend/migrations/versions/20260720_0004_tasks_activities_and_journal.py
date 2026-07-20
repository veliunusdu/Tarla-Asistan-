"""add daily tasks, activities and farm journal history

Revision ID: 20260720_0004
Revises: 20260720_0003
Create Date: 2026-07-20
"""
from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "20260720_0004"
down_revision: str | None = "20260720_0003"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None

task_priority = postgresql.ENUM(
    "LOW",
    "MEDIUM",
    "HIGH",
    "CRITICAL",
    name="task_priority",
    create_type=False,
)
task_status = postgresql.ENUM(
    "NEW",
    "VIEWED",
    "PLANNED",
    "COMPLETED",
    "NOT_APPLIED",
    "OVERDUE",
    "CANCELLED",
    name="task_status",
    create_type=False,
)
task_source = postgresql.ENUM(
    "SYSTEM",
    "CROP_CALENDAR",
    "WEATHER",
    "EXPERT",
    name="task_source",
    create_type=False,
)
task_confidence = postgresql.ENUM(
    "LOW",
    "MEDIUM",
    "HIGH",
    name="task_confidence",
    create_type=False,
)
activity_type = postgresql.ENUM(
    "IRRIGATION",
    "FERTILIZATION",
    "SPRAYING",
    "PRUNING",
    "FIELD_CHECK",
    "HARVEST",
    "OTHER",
    name="activity_type",
    create_type=False,
)
activity_status = postgresql.ENUM(
    "DRAFT",
    "CONFIRMED",
    name="activity_status",
    create_type=False,
)
activity_source = postgresql.ENUM(
    "MANUAL",
    "VOICE",
    "TASK",
    name="activity_source",
    create_type=False,
)


def upgrade() -> None:
    bind = op.get_bind()
    for enum_type in (
        task_priority,
        task_status,
        task_source,
        task_confidence,
        activity_type,
        activity_status,
        activity_source,
    ):
        enum_type.create(bind, checkfirst=True)

    op.create_table(
        "tasks",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("farm_id", sa.Uuid(), nullable=False),
        sa.Column("crop_period_id", sa.Uuid(), nullable=True),
        sa.Column("created_by_id", sa.Uuid(), nullable=True),
        sa.Column("title", sa.String(length=160), nullable=False),
        sa.Column("description", sa.Text(), nullable=False),
        sa.Column("reason", sa.Text(), nullable=False),
        sa.Column("priority", task_priority, nullable=False),
        sa.Column("status", task_status, server_default="NEW", nullable=False),
        sa.Column("source", task_source, nullable=False),
        sa.Column(
            "confidence",
            task_confidence,
            server_default="MEDIUM",
            nullable=False,
        ),
        sa.Column("due_date", sa.Date(), nullable=False),
        sa.Column("dedupe_key", sa.String(length=64), nullable=False),
        sa.Column("not_applied_reason", sa.String(length=500), nullable=True),
        sa.Column("completion_note", sa.String(length=1000), nullable=True),
        sa.Column("photo_url", sa.String(length=2048), nullable=True),
        sa.Column("viewed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("completed_at", sa.DateTime(timezone=True), nullable=True),
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
        sa.ForeignKeyConstraint(
            ["crop_period_id"],
            ["crop_periods.id"],
            ondelete="SET NULL",
        ),
        sa.ForeignKeyConstraint(
            ["created_by_id"],
            ["users.id"],
            ondelete="SET NULL",
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_tasks_farm_id", "tasks", ["farm_id"])
    op.create_index("ix_tasks_crop_period_id", "tasks", ["crop_period_id"])
    op.create_index("ix_tasks_created_by_id", "tasks", ["created_by_id"])
    op.create_index(
        "ix_tasks_farm_due_status",
        "tasks",
        ["farm_id", "due_date", "status"],
    )
    op.create_index(
        "ix_tasks_farm_created",
        "tasks",
        ["farm_id", "created_at"],
    )
    op.create_index(
        "uq_tasks_farm_due_dedupe",
        "tasks",
        ["farm_id", "due_date", "dedupe_key"],
        unique=True,
    )

    op.create_table(
        "activities",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("farm_id", sa.Uuid(), nullable=False),
        sa.Column("crop_period_id", sa.Uuid(), nullable=True),
        sa.Column("task_id", sa.Uuid(), nullable=True),
        sa.Column("created_by_id", sa.Uuid(), nullable=True),
        sa.Column("activity_type", activity_type, nullable=False),
        sa.Column(
            "status",
            activity_status,
            server_default="CONFIRMED",
            nullable=False,
        ),
        sa.Column(
            "source",
            activity_source,
            server_default="MANUAL",
            nullable=False,
        ),
        sa.Column("description", sa.Text(), nullable=False),
        sa.Column("occurred_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("duration_minutes", sa.Integer(), nullable=True),
        sa.Column("amount", sa.Float(), nullable=True),
        sa.Column("unit", sa.String(length=40), nullable=True),
        sa.Column("photo_url", sa.String(length=2048), nullable=True),
        sa.Column("voice_url", sa.String(length=2048), nullable=True),
        sa.Column("voice_transcript", sa.Text(), nullable=True),
        sa.Column("performed_by", sa.String(length=120), nullable=True),
        sa.Column("cost", sa.Float(), nullable=True),
        sa.Column("confirmed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("archived_at", sa.DateTime(timezone=True), nullable=True),
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
        sa.ForeignKeyConstraint(
            ["crop_period_id"],
            ["crop_periods.id"],
            ondelete="SET NULL",
        ),
        sa.ForeignKeyConstraint(["task_id"], ["tasks.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(
            ["created_by_id"],
            ["users.id"],
            ondelete="SET NULL",
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_activities_farm_id", "activities", ["farm_id"])
    op.create_index(
        "ix_activities_crop_period_id",
        "activities",
        ["crop_period_id"],
    )
    op.create_index("ix_activities_task_id", "activities", ["task_id"])
    op.create_index(
        "ix_activities_created_by_id",
        "activities",
        ["created_by_id"],
    )
    op.create_index(
        "ix_activities_farm_occurred",
        "activities",
        ["farm_id", "occurred_at"],
    )
    op.create_index(
        "ix_activities_farm_archived",
        "activities",
        ["farm_id", "archived_at"],
    )
    op.create_index(
        "uq_activities_task_id",
        "activities",
        ["task_id"],
        unique=True,
    )

    op.create_table(
        "activity_revisions",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("activity_id", sa.Uuid(), nullable=False),
        sa.Column("changed_by_id", sa.Uuid(), nullable=True),
        sa.Column("previous_values", sa.JSON(), nullable=False),
        sa.Column(
            "changed_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.ForeignKeyConstraint(
            ["activity_id"],
            ["activities.id"],
            ondelete="CASCADE",
        ),
        sa.ForeignKeyConstraint(
            ["changed_by_id"],
            ["users.id"],
            ondelete="SET NULL",
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        "ix_activity_revisions_activity_id",
        "activity_revisions",
        ["activity_id"],
    )
    op.create_index(
        "ix_activity_revisions_changed_by_id",
        "activity_revisions",
        ["changed_by_id"],
    )
    op.create_index(
        "ix_activity_revisions_activity_changed",
        "activity_revisions",
        ["activity_id", "changed_at"],
    )


def downgrade() -> None:
    op.drop_index(
        "ix_activity_revisions_activity_changed",
        table_name="activity_revisions",
    )
    op.drop_index(
        "ix_activity_revisions_changed_by_id",
        table_name="activity_revisions",
    )
    op.drop_index(
        "ix_activity_revisions_activity_id",
        table_name="activity_revisions",
    )
    op.drop_table("activity_revisions")

    op.drop_index("uq_activities_task_id", table_name="activities")
    op.drop_index("ix_activities_farm_archived", table_name="activities")
    op.drop_index("ix_activities_farm_occurred", table_name="activities")
    op.drop_index("ix_activities_created_by_id", table_name="activities")
    op.drop_index("ix_activities_task_id", table_name="activities")
    op.drop_index("ix_activities_crop_period_id", table_name="activities")
    op.drop_index("ix_activities_farm_id", table_name="activities")
    op.drop_table("activities")

    op.drop_index("uq_tasks_farm_due_dedupe", table_name="tasks")
    op.drop_index("ix_tasks_farm_created", table_name="tasks")
    op.drop_index("ix_tasks_farm_due_status", table_name="tasks")
    op.drop_index("ix_tasks_created_by_id", table_name="tasks")
    op.drop_index("ix_tasks_crop_period_id", table_name="tasks")
    op.drop_index("ix_tasks_farm_id", table_name="tasks")
    op.drop_table("tasks")

    for enum_type in (
        activity_source,
        activity_status,
        activity_type,
        task_confidence,
        task_source,
        task_status,
        task_priority,
    ):
        enum_type.drop(op.get_bind(), checkfirst=True)

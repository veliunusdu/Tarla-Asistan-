"""add secure account management persistence

Revision ID: 20260820_0008
Revises: 20260820_0007
Create Date: 2026-08-20
"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql


revision: str = "20260820_0008"
down_revision: str | None = "20260820_0007"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


account_status = sa.Enum(
    "ACTIVE",
    "DELETION_PENDING",
    "ANONYMIZED",
    name="account_status",
)
account_deletion_status = postgresql.ENUM(
    "PENDING",
    "PROCESSING",
    "RETRY_REQUIRED",
    "COMPLETED",
    name="account_deletion_status",
    create_type=False,
)


def upgrade() -> None:
    bind = op.get_bind()
    account_status.create(bind, checkfirst=True)
    account_deletion_status.create(bind, checkfirst=True)

    op.add_column(
        "users",
        sa.Column(
            "account_status",
            account_status,
            nullable=False,
            server_default=sa.text("'ACTIVE'"),
        ),
    )
    op.add_column(
        "users", sa.Column("deleted_at", sa.DateTime(timezone=True), nullable=True)
    )
    op.add_column(
        "users",
        sa.Column("anonymized_subject_id", sa.String(length=64), nullable=True),
    )
    op.create_unique_constraint(
        "uq_users_anonymized_subject_id", "users", ["anonymized_subject_id"]
    )
    op.alter_column(
        "users",
        "phone_number",
        existing_type=sa.String(length=20),
        type_=sa.String(length=64),
        existing_nullable=False,
    )
    op.alter_column(
        "profiles",
        "full_name",
        existing_type=sa.String(length=120),
        nullable=True,
    )
    op.alter_column(
        "profiles",
        "province",
        existing_type=sa.String(length=80),
        nullable=True,
    )
    op.alter_column(
        "profiles",
        "district",
        existing_type=sa.String(length=80),
        nullable=True,
    )

    op.create_table(
        "firebase_link_approvals",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("firebase_uid", sa.String(length=128), nullable=False),
        sa.Column("approved_by", sa.String(length=120), nullable=False),
        sa.Column("approved_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("consumed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.ForeignKeyConstraint(["user_id"], ["users.id"]),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        "ix_firebase_link_approvals_user_id",
        "firebase_link_approvals",
        ["user_id"],
        unique=False,
    )
    op.create_index(
        "ix_firebase_link_approvals_expires_at",
        "firebase_link_approvals",
        ["expires_at"],
        unique=False,
    )
    op.create_index(
        "uq_firebase_link_approvals_one_unconsumed_per_user",
        "firebase_link_approvals",
        ["user_id"],
        unique=True,
        postgresql_where=sa.text("consumed_at IS NULL"),
    )
    op.create_index(
        "uq_firebase_link_approvals_one_unconsumed_per_uid",
        "firebase_link_approvals",
        ["firebase_uid"],
        unique=True,
        postgresql_where=sa.text("consumed_at IS NULL"),
    )

    op.create_table(
        "account_deletion_jobs",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("firebase_uid_snapshot", sa.String(length=128), nullable=True),
        sa.Column("status", account_deletion_status, nullable=False),
        sa.Column("attempt_count", sa.Integer(), nullable=False),
        sa.Column("last_error_code", sa.String(length=80), nullable=True),
        sa.Column("next_retry_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("processing_started_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("lease_until", sa.DateTime(timezone=True), nullable=True),
        sa.Column("processing_owner_token", sa.String(length=64), nullable=True),
        sa.Column(
            "firebase_tokens_revoked_at", sa.DateTime(timezone=True), nullable=True
        ),
        sa.Column("firestore_anonymized_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("media_deleted_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "firebase_auth_deleted_at", sa.DateTime(timezone=True), nullable=True
        ),
        sa.Column("postgres_anonymized_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("completed_at", sa.DateTime(timezone=True), nullable=True),
        sa.ForeignKeyConstraint(["user_id"], ["users.id"]),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("user_id"),
    )
    op.create_index(
        "ix_account_deletion_jobs_retry_schedule",
        "account_deletion_jobs",
        ["status", "next_retry_at", "lease_until", "created_at"],
        unique=False,
    )

    op.alter_column(
        "users",
        "account_status",
        existing_type=account_status,
        existing_nullable=False,
        server_default=None,
    )


def downgrade() -> None:
    op.drop_table("account_deletion_jobs")
    op.drop_index(
        "uq_firebase_link_approvals_one_unconsumed_per_uid",
        table_name="firebase_link_approvals",
    )
    op.drop_index(
        "uq_firebase_link_approvals_one_unconsumed_per_user",
        table_name="firebase_link_approvals",
    )
    op.drop_index(
        "ix_firebase_link_approvals_expires_at",
        table_name="firebase_link_approvals",
    )
    op.drop_index(
        "ix_firebase_link_approvals_user_id",
        table_name="firebase_link_approvals",
    )
    op.drop_table("firebase_link_approvals")

    op.alter_column(
        "profiles",
        "district",
        existing_type=sa.String(length=80),
        nullable=False,
    )
    op.alter_column(
        "profiles",
        "province",
        existing_type=sa.String(length=80),
        nullable=False,
    )
    op.alter_column(
        "profiles",
        "full_name",
        existing_type=sa.String(length=120),
        nullable=False,
    )
    op.alter_column(
        "users",
        "phone_number",
        existing_type=sa.String(length=64),
        type_=sa.String(length=20),
        existing_nullable=False,
    )
    op.drop_constraint("uq_users_anonymized_subject_id", "users", type_="unique")
    op.drop_column("users", "anonymized_subject_id")
    op.drop_column("users", "deleted_at")
    op.drop_column("users", "account_status")

    bind = op.get_bind()
    account_deletion_status.drop(bind, checkfirst=True)
    account_status.drop(bind, checkfirst=True)

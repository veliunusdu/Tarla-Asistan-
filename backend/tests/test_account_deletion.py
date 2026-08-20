import os
from pathlib import Path
import subprocess
import sys
from datetime import timedelta

import pytest
from sqlalchemy.exc import IntegrityError

from app.models import (
    AccountDeletionJob,
    AccountDeletionStatus,
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
    utcnow,
)


def test_secure_account_models_have_safe_defaults(db_session):
    user = User(phone_number="+905551234580", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.commit()
    approval = FirebaseLinkApproval(
        user_id=user.id,
        firebase_uid="approved-uid",
        approved_by="staging-operator",
        approved_at=utcnow(),
        expires_at=utcnow() + timedelta(hours=24),
    )
    job = AccountDeletionJob(user_id=user.id, firebase_uid_snapshot="approved-uid")
    db_session.add_all([approval, job])
    db_session.commit()

    assert user.account_status is AccountStatus.ACTIVE
    assert approval.consumed_at is None
    assert job.status is AccountDeletionStatus.PENDING
    assert job.attempt_count == 0


def test_only_one_unconsumed_link_approval_is_allowed_per_user(db_session):
    user = User(phone_number="+905551234581", role=UserRole.FARMER)
    db_session.add(user)
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=user.id,
            firebase_uid="first-approved-uid",
            approved_by="staging-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=user.id,
            firebase_uid="second-approved-uid",
            approved_by="staging-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    with pytest.raises(IntegrityError):
        db_session.commit()


def test_consumed_link_approval_preserves_history_without_blocking_same_identity(
    db_session,
):
    users = [
        User(phone_number=f"test-phone-approval-{index}", role=UserRole.AGRONOMIST)
        for index in range(3)
    ]
    db_session.add_all(users)
    db_session.flush()
    db_session.add(
        FirebaseLinkApproval(
            user_id=users[0].id,
            firebase_uid="expert-identity-shared",
            approved_by="first-operator",
            approved_at=utcnow() - timedelta(days=2),
            expires_at=utcnow() - timedelta(days=1),
            consumed_at=utcnow(),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=users[1].id,
            firebase_uid="expert-identity-shared",
            approved_by="second-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=users[2].id,
            firebase_uid="expert-identity-shared",
            approved_by="third-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    with pytest.raises(IntegrityError):
        db_session.commit()


def test_postgresql_migration_uses_partial_unconsumed_approval_indexes():
    backend_dir = Path(__file__).resolve().parents[1]
    environment = os.environ.copy()
    environment["DATABASE_URL"] = (
        "postgresql+psycopg://unused:unused@localhost/unused"
    )
    completed = subprocess.run(
        [
            sys.executable,
            "-m",
            "alembic",
            "upgrade",
            "20260820_0007:20260820_0008",
            "--sql",
        ],
        cwd=backend_dir,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    sql = " ".join(completed.stdout.split())
    approval_table_start = sql.index("CREATE TABLE firebase_link_approvals")
    approval_table_end = sql.index(
        "CREATE INDEX ix_firebase_link_approvals_user_id",
        approval_table_start,
    )
    approval_table_sql = sql[approval_table_start:approval_table_end]
    assert "UNIQUE (firebase_uid)" not in approval_table_sql
    assert (
        "CREATE UNIQUE INDEX "
        "uq_firebase_link_approvals_one_unconsumed_per_user "
        "ON firebase_link_approvals (user_id) WHERE consumed_at IS NULL"
    ) in sql
    assert (
        "CREATE UNIQUE INDEX "
        "uq_firebase_link_approvals_one_unconsumed_per_uid "
        "ON firebase_link_approvals (firebase_uid) WHERE consumed_at IS NULL"
    ) in sql

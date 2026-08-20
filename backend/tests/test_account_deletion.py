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

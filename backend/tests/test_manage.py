from contextlib import nullcontext
from datetime import datetime, timedelta, timezone

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import sessionmaker

from app.manage import ManagementCommandError, approve_firebase_link, main
from app.models import (
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
)


TEST_NOW = datetime(2026, 8, 20, 12, 0, tzinfo=timezone.utc)


def add_user(
    db_session,
    *,
    role: UserRole = UserRole.AGRONOMIST,
    account_status: AccountStatus = AccountStatus.ACTIVE,
    firebase_uid: str | None = None,
) -> User:
    user = User(
        phone_number=f"test-phone-{role.value.lower()}-{id(db_session)}",
        role=role,
        account_status=account_status,
        firebase_uid=firebase_uid,
    )
    db_session.add(user)
    db_session.commit()
    return user


def approval_count(db_session) -> int:
    return db_session.scalar(select(func.count(FirebaseLinkApproval.id))) or 0


def as_utc(value: datetime) -> datetime:
    return value.replace(tzinfo=value.tzinfo or timezone.utc).astimezone(timezone.utc)


def test_approve_firebase_link_creates_auditable_24_hour_approval(db_session):
    expert = add_user(db_session)

    approval = approve_firebase_link(
        db_session,
        user_id=expert.id,
        firebase_uid="expert-identity-a",
        operator="test-operator",
        now=TEST_NOW,
    )

    assert approval is not None
    assert approval.user_id == expert.id
    assert approval.approved_by == "test-operator"
    assert as_utc(approval.approved_at) == TEST_NOW
    assert as_utc(approval.expires_at) == TEST_NOW + timedelta(hours=24)
    assert approval.consumed_at is None


def test_approve_firebase_link_dry_run_has_no_side_effects(db_session):
    expert = add_user(db_session)
    expired = FirebaseLinkApproval(
        user_id=expert.id,
        firebase_uid="expert-identity-old",
        approved_by="test-operator",
        approved_at=TEST_NOW - timedelta(days=2),
        expires_at=TEST_NOW - timedelta(days=1),
    )
    db_session.add(expired)
    db_session.commit()

    result = approve_firebase_link(
        db_session,
        user_id=expert.id,
        firebase_uid="expert-identity-new",
        operator="test-operator",
        now=TEST_NOW,
        dry_run=True,
    )

    db_session.refresh(expired)
    assert result is None
    assert expired.consumed_at is None
    assert approval_count(db_session) == 1


def test_approve_firebase_link_consumes_expired_open_approval_before_replacing_it(
    db_session,
):
    expert = add_user(db_session)
    expired = FirebaseLinkApproval(
        user_id=expert.id,
        firebase_uid="expert-identity-old",
        approved_by="test-operator",
        approved_at=TEST_NOW - timedelta(days=2),
        expires_at=TEST_NOW - timedelta(days=1),
    )
    db_session.add(expired)
    db_session.commit()

    replacement = approve_firebase_link(
        db_session,
        user_id=expert.id,
        firebase_uid="expert-identity-new",
        operator="test-operator",
        now=TEST_NOW,
    )

    db_session.refresh(expired)
    assert as_utc(expired.consumed_at) == TEST_NOW
    assert replacement is not None
    assert replacement.firebase_uid == "expert-identity-new"
    assert approval_count(db_session) == 2


def test_approve_firebase_link_renews_expired_approval_for_same_identity(
    db_session,
):
    expert = add_user(db_session)
    expired = FirebaseLinkApproval(
        user_id=expert.id,
        firebase_uid="expert-identity-a",
        approved_by="old-operator",
        approved_at=TEST_NOW - timedelta(days=2),
        expires_at=TEST_NOW - timedelta(days=1),
    )
    db_session.add(expired)
    db_session.commit()

    renewed = approve_firebase_link(
        db_session,
        user_id=expert.id,
        firebase_uid=expired.firebase_uid,
        operator="new-operator",
        now=TEST_NOW,
    )

    assert renewed is not None
    assert renewed.id == expired.id
    assert renewed.approved_by == "new-operator"
    assert as_utc(renewed.approved_at) == TEST_NOW
    assert as_utc(renewed.expires_at) == TEST_NOW + timedelta(hours=24)
    assert renewed.consumed_at is None
    assert approval_count(db_session) == 1


@pytest.mark.parametrize(
    ("role", "account_status", "existing_uid"),
    [
        (UserRole.FARMER, AccountStatus.ACTIVE, None),
        (UserRole.AGRONOMIST, AccountStatus.DELETION_PENDING, None),
        (UserRole.AGRONOMIST, AccountStatus.ACTIVE, "expert-identity-existing"),
    ],
    ids=["farmer", "inactive-expert", "already-linked-expert"],
)
def test_approve_firebase_link_rejects_ineligible_targets(
    db_session, role, account_status, existing_uid
):
    user = add_user(
        db_session,
        role=role,
        account_status=account_status,
        firebase_uid=existing_uid,
    )

    with pytest.raises(ManagementCommandError) as error:
        approve_firebase_link(
            db_session,
            user_id=user.id,
            firebase_uid="expert-identity-new",
            operator="test-operator",
            now=TEST_NOW,
        )

    assert "expert-identity" not in str(error.value)
    assert "test-phone" not in str(error.value)
    assert approval_count(db_session) == 0


def test_approve_firebase_link_rejects_identity_already_bound_to_another_user(
    db_session,
):
    expert = add_user(db_session)
    add_user(
        db_session,
        role=UserRole.FARMER,
        firebase_uid="expert-identity-taken",
    )

    with pytest.raises(ManagementCommandError) as error:
        approve_firebase_link(
            db_session,
            user_id=expert.id,
            firebase_uid="expert-identity-taken",
            operator="test-operator",
            now=TEST_NOW,
        )

    assert "expert-identity-taken" not in str(error.value)
    assert approval_count(db_session) == 0


def test_approve_firebase_link_rejects_a_second_unexpired_approval(db_session):
    expert = add_user(db_session)
    approve_firebase_link(
        db_session,
        user_id=expert.id,
        firebase_uid="expert-identity-a",
        operator="test-operator",
        now=TEST_NOW,
    )

    with pytest.raises(ManagementCommandError) as error:
        approve_firebase_link(
            db_session,
            user_id=expert.id,
            firebase_uid="expert-identity-b",
            operator="test-operator",
            now=TEST_NOW,
        )

    assert "expert-identity" not in str(error.value)
    assert approval_count(db_session) == 1


def test_approval_unique_race_returns_a_safe_management_error(
    db_session, monkeypatch
):
    expert = add_user(db_session)
    independent_session = sessionmaker(
        bind=db_session.bind,
        autoflush=False,
        expire_on_commit=False,
    )
    original_commit = db_session.commit
    raced = False

    def commit_after_winner():
        nonlocal raced
        if not raced:
            raced = True
            with independent_session() as winner:
                winner.add(
                    FirebaseLinkApproval(
                        user_id=expert.id,
                        firebase_uid="expert-identity-winner",
                        approved_by="other-operator",
                        approved_at=TEST_NOW,
                        expires_at=TEST_NOW + timedelta(hours=24),
                    )
                )
                winner.commit()
        return original_commit()

    monkeypatch.setattr(db_session, "commit", commit_after_winner)

    with pytest.raises(ManagementCommandError) as error:
        approve_firebase_link(
            db_session,
            user_id=expert.id,
            firebase_uid="expert-identity-loser",
            operator="test-operator",
            now=TEST_NOW,
        )

    assert raced is True
    assert "expert-identity" not in str(error.value)
    assert approval_count(db_session) == 1


def test_approval_cli_success_output_excludes_sensitive_identifiers(
    db_session, capsys
):
    expert = add_user(db_session)

    exit_code = main(
        [
            "approve-firebase-link",
            "--user-id",
            str(expert.id),
            "--firebase-uid",
            "expert-identity-secret",
            "--operator",
            "test-operator",
        ],
        session_factory=lambda: nullcontext(db_session),
        now_factory=lambda: TEST_NOW,
    )

    output = capsys.readouterr()
    assert exit_code == 0
    assert str(expert.id) in output.out
    assert (TEST_NOW + timedelta(hours=24)).date().isoformat() in output.out
    assert "expert-identity-secret" not in output.out + output.err
    assert expert.phone_number not in output.out + output.err


def test_approval_cli_dry_run_reports_without_persisting(db_session, capsys):
    expert = add_user(db_session)

    exit_code = main(
        [
            "approve-firebase-link",
            "--user-id",
            str(expert.id),
            "--firebase-uid",
            "expert-identity-secret",
            "--operator",
            "test-operator",
            "--dry-run",
        ],
        session_factory=lambda: nullcontext(db_session),
        now_factory=lambda: TEST_NOW,
    )

    output = capsys.readouterr()
    assert exit_code == 0
    assert "dry-run" in output.out.lower()
    assert "expert-identity-secret" not in output.out + output.err
    assert approval_count(db_session) == 0


def test_approval_cli_error_output_excludes_sensitive_identifiers(
    db_session, capsys
):
    farmer = add_user(db_session, role=UserRole.FARMER)

    exit_code = main(
        [
            "approve-firebase-link",
            "--user-id",
            str(farmer.id),
            "--firebase-uid",
            "expert-identity-secret",
            "--operator",
            "test-operator",
        ],
        session_factory=lambda: nullcontext(db_session),
        now_factory=lambda: TEST_NOW,
    )

    output = capsys.readouterr()
    assert exit_code == 2
    assert "expert-identity-secret" not in output.out + output.err
    assert farmer.phone_number not in output.out + output.err

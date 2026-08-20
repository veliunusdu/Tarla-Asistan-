from contextlib import nullcontext
from datetime import datetime, timedelta, timezone
from pathlib import Path
from tempfile import gettempdir
from uuid import uuid4

import pytest
from sqlalchemy import create_engine, event, func, select
from sqlalchemy.orm import sessionmaker

from app.database import Base
from app.manage import ManagementCommandError, approve_firebase_link, main
from app.models import (
    AccountDeletionJob,
    AccountDeletionStatus,
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
)


TEST_NOW = datetime(2026, 8, 20, 12, 0, tzinfo=timezone.utc)


@pytest.fixture
def independent_database():
    database_path = Path(gettempdir()) / f"approval-races-{uuid4().hex}.sqlite3"
    engine = create_engine(
        f"sqlite+pysqlite:///{database_path}",
        connect_args={"check_same_thread": False, "timeout": 5},
    )
    Base.metadata.create_all(engine)
    sessions = sessionmaker(
        bind=engine,
        autoflush=False,
        expire_on_commit=False,
    )
    try:
        yield engine, sessions
    finally:
        engine.dispose()
        for database_file in (
            database_path,
            Path(f"{database_path}-journal"),
            Path(f"{database_path}-shm"),
            Path(f"{database_path}-wal"),
        ):
            database_file.unlink(missing_ok=True)


def run_after_committed_winner(
    engine,
    *,
    pause_on: str,
    loser,
    winner,
):
    winner_ran = False

    def commit_winner(connection, _cursor, statement, _parameters, _context, _many):
        nonlocal winner_ran
        if (
            connection.info.get("race_actor") == "loser"
            and statement.lstrip().upper().startswith(pause_on)
        ):
            connection.info["race_actor"] = "winner-committed"
            connection.connection.commit()
            winner()
            winner_ran = True

    event.listen(engine, "before_cursor_execute", commit_winner)
    try:
        result = loser()
    finally:
        event.remove(engine, "before_cursor_execute", commit_winner)
    assert winner_ran is True
    return result


def add_user(
    db_session,
    *,
    role: UserRole = UserRole.AGRONOMIST,
    account_status: AccountStatus = AccountStatus.ACTIVE,
    firebase_uid: str | None = None,
) -> User:
    user = User(
        phone_number=f"test-phone-{role.value.lower()}-{uuid4().hex}",
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
    assert renewed.id != expired.id
    assert renewed.approved_by == "new-operator"
    assert as_utc(renewed.approved_at) == TEST_NOW
    assert as_utc(renewed.expires_at) == TEST_NOW + timedelta(hours=24)
    assert renewed.consumed_at is None
    db_session.refresh(expired)
    assert expired.approved_by == "old-operator"
    assert as_utc(expired.approved_at) == TEST_NOW - timedelta(days=2)
    assert as_utc(expired.expires_at) == TEST_NOW - timedelta(days=1)
    assert as_utc(expired.consumed_at) == TEST_NOW
    assert approval_count(db_session) == 2


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
    independent_database,
):
    engine, sessions = independent_database
    with sessions() as setup:
        expert = add_user(setup)
        expert_id = expert.id

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return approve_firebase_link(
                loser,
                user_id=expert_id,
                firebase_uid="expert-identity-loser",
                operator="test-operator",
                now=TEST_NOW,
            )

    def win_race():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.add(
                FirebaseLinkApproval(
                    user_id=expert_id,
                    firebase_uid="expert-identity-winner",
                    approved_by="other-operator",
                    approved_at=TEST_NOW,
                    expires_at=TEST_NOW + timedelta(hours=24),
                )
            )
            winner.commit()

    with pytest.raises(ManagementCommandError) as error:
        run_after_committed_winner(
            engine,
            pause_on="INSERT INTO FIREBASE_LINK_APPROVALS",
            loser=lose_race,
            winner=win_race,
        )

    assert "expert-identity" not in str(error.value)
    with sessions() as verification:
        assert approval_count(verification) == 1


def test_approval_identity_unique_race_returns_a_safe_management_error(
    independent_database,
):
    engine, sessions = independent_database
    with sessions() as setup:
        target = add_user(setup)
        winner_user = add_user(setup)
        target_id = target.id
        winner_user_id = winner_user.id

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return approve_firebase_link(
                loser,
                user_id=target_id,
                firebase_uid="expert-identity-shared-race",
                operator="test-operator",
                now=TEST_NOW,
            )

    def win_race():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.add(
                FirebaseLinkApproval(
                    user_id=winner_user_id,
                    firebase_uid="expert-identity-shared-race",
                    approved_by="other-operator",
                    approved_at=TEST_NOW,
                    expires_at=TEST_NOW + timedelta(hours=24),
                )
            )
            winner.commit()

    with pytest.raises(ManagementCommandError) as error:
        run_after_committed_winner(
            engine,
            pause_on="INSERT INTO FIREBASE_LINK_APPROVALS",
            loser=lose_race,
            winner=win_race,
        )

    assert "expert-identity" not in str(error.value)
    with sessions() as verification:
        assert approval_count(verification) == 1


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


def add_deletion_retry_job(
    db_session,
    *,
    status=AccountDeletionStatus.RETRY_REQUIRED,
    attempt_count=0,
    next_retry_at=TEST_NOW,
):
    user = User(
        phone_number=f"test-deletion-retry-{uuid4().hex}",
        account_status=AccountStatus.DELETION_PENDING,
        anonymized_subject_id=f"anon-{uuid4().hex}",
    )
    db_session.add(user)
    db_session.flush()
    job = AccountDeletionJob(
        user_id=user.id,
        status=status,
        attempt_count=attempt_count,
        next_retry_at=next_retry_at,
    )
    db_session.add(job)
    db_session.commit()
    return job


def test_retry_account_deletions_dry_run_lists_eligible_jobs_without_processing(
    db_session, capsys, monkeypatch
):
    eligible = add_deletion_retry_job(db_session)
    future = add_deletion_retry_job(
        db_session, next_retry_at=TEST_NOW + timedelta(minutes=1)
    )
    processed = []

    def process(job_id, *, force=False):
        processed.append((job_id, force))

    monkeypatch.setattr("app.manage.process_account_deletion_safely", process)

    exit_code = main(
        ["retry-account-deletions", "--dry-run"],
        session_factory=lambda: nullcontext(db_session),
        now_factory=lambda: TEST_NOW,
    )

    output = capsys.readouterr()
    assert exit_code == 0
    assert str(eligible.id) in output.out
    assert eligible.status.value in output.out
    assert str(future.id) not in output.out
    assert processed == []


def test_retry_account_deletions_explicit_job_overrides_automatic_attempt_limit(
    db_session, capsys, monkeypatch
):
    exhausted = add_deletion_retry_job(
        db_session,
        attempt_count=5,
        next_retry_at=TEST_NOW + timedelta(days=1),
    )
    processed = []

    def process(job_id, *, force=False):
        processed.append((job_id, force))

    monkeypatch.setattr("app.manage.process_account_deletion_safely", process)

    exit_code = main(
        ["retry-account-deletions", "--job-id", str(exhausted.id)],
        session_factory=lambda: nullcontext(db_session),
        now_factory=lambda: TEST_NOW,
    )

    output = capsys.readouterr()
    assert exit_code == 0
    assert processed == [(exhausted.id, True)]
    assert str(exhausted.id) in output.out

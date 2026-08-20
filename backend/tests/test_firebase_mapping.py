from datetime import datetime, timedelta, timezone
from pathlib import Path
from tempfile import gettempdir
from uuid import uuid4

import pytest
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials
from sqlalchemy import create_engine, event, update
from sqlalchemy.orm import sessionmaker

from app.config import Settings
from app.database import Base
from app.dependencies import get_current_user
from app.firebase_auth import FirebaseIdentity
from app.firebase_mapping import resolve_firebase_user
from app.models import (
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
)
from app.security import create_access_token


PAST = datetime(2000, 1, 1, tzinfo=timezone.utc)
FUTURE = datetime(2999, 1, 1, tzinfo=timezone.utc)


@pytest.fixture
def independent_database():
    database_path = Path(gettempdir()) / f"firebase-races-{uuid4().hex}.sqlite3"
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


def add_approved_expert(
    db_session,
    *,
    uid: str = "expert-identity-a",
    expires_at: datetime | None = None,
    consumed_at: datetime | None = None,
):
    user = User(phone_number="test-phone-expert-a", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.flush()
    approval = FirebaseLinkApproval(
        user_id=user.id,
        firebase_uid=uid,
        approved_by="test-operator",
        approved_at=PAST,
        expires_at=expires_at or FUTURE,
        consumed_at=consumed_at,
    )
    db_session.add(approval)
    db_session.commit()
    return user, approval


def resolve(db_session, *, uid: str, phone_number: str | None):
    return resolve_firebase_user(
        db_session,
        FirebaseIdentity(uid=uid, phone_number=phone_number),
    )


def assert_approval_rejected(db_session, user: User, *, uid: str) -> None:
    with pytest.raises(HTTPException) as error:
        resolve(db_session, uid=uid, phone_number=user.phone_number)

    assert error.value.status_code == 403
    assert "test-phone" not in str(error.value.detail)
    assert uid not in str(error.value.detail)
    db_session.refresh(user)
    assert user.firebase_uid is None


def test_unapproved_agronomist_link_is_forbidden(db_session):
    user = User(phone_number="test-phone-expert-a", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.commit()

    assert_approval_rejected(db_session, user, uid="expert-identity-a")


def test_approved_agronomist_link_consumes_approval(db_session):
    user, approval = add_approved_expert(db_session)

    resolved = resolve(
        db_session,
        uid=approval.firebase_uid,
        phone_number=user.phone_number,
    )

    db_session.refresh(approval)
    assert resolved.id == user.id
    assert resolved.firebase_uid == approval.firebase_uid
    assert approval.consumed_at is not None


@pytest.mark.parametrize(
    ("approval_kwargs", "attempted_uid"),
    [
        ({"expires_at": PAST}, "expert-identity-a"),
        (
            {"consumed_at": PAST + timedelta(minutes=1)},
            "expert-identity-a",
        ),
        ({}, "expert-identity-b"),
    ],
    ids=["expired", "consumed", "wrong-identity"],
)
def test_invalid_agronomist_approval_states_are_forbidden(
    db_session, approval_kwargs, attempted_uid
):
    user, _approval = add_approved_expert(db_session, **approval_kwargs)

    assert_approval_rejected(db_session, user, uid=attempted_uid)


def test_farmer_phone_match_still_auto_links(db_session):
    farmer = User(phone_number="test-phone-farmer-a", role=UserRole.FARMER)
    db_session.add(farmer)
    db_session.commit()

    resolved = resolve(
        db_session,
        uid="farmer-identity-a",
        phone_number=farmer.phone_number,
    )

    db_session.refresh(farmer)
    assert resolved.id == farmer.id
    assert farmer.firebase_uid == "farmer-identity-a"


def test_existing_same_link_is_idempotent_without_an_approval(db_session):
    expert = User(
        phone_number="test-phone-expert-a",
        firebase_uid="expert-identity-a",
        role=UserRole.AGRONOMIST,
    )
    db_session.add(expert)
    db_session.commit()

    resolved = resolve(
        db_session,
        uid=expert.firebase_uid,
        phone_number=expert.phone_number,
    )

    assert resolved.id == expert.id


def test_phone_linked_to_a_different_identity_is_a_conflict(db_session):
    farmer = User(
        phone_number="test-phone-farmer-a",
        firebase_uid="farmer-identity-a",
        role=UserRole.FARMER,
    )
    db_session.add(farmer)
    db_session.commit()

    with pytest.raises(HTTPException) as error:
        resolve(
            db_session,
            uid="farmer-identity-b",
            phone_number=farmer.phone_number,
        )

    assert error.value.status_code == 409
    db_session.refresh(farmer)
    assert farmer.firebase_uid == "farmer-identity-a"


def test_firebase_mapping_rejects_inactive_accounts(db_session):
    user = User(
        phone_number="test-phone-farmer-a",
        firebase_uid="farmer-identity-a",
        role=UserRole.FARMER,
        account_status=AccountStatus.DELETION_PENDING,
    )
    db_session.add(user)
    db_session.commit()

    with pytest.raises(HTTPException) as error:
        resolve(
            db_session,
            uid=user.firebase_uid,
            phone_number=user.phone_number,
        )

    assert error.value.status_code == 403


@pytest.mark.parametrize(
    "account_status",
    [AccountStatus.DELETION_PENDING, AccountStatus.ANONYMIZED],
)
def test_legacy_jwt_mapping_rejects_inactive_accounts(
    db_session, account_status
):
    settings = Settings(firebase_auth_enabled=False)
    user = User(
        phone_number=f"test-phone-{account_status.value.lower()}",
        role=UserRole.FARMER,
        account_status=account_status,
    )
    db_session.add(user)
    db_session.commit()
    token = create_access_token(str(user.id), user.role.value, settings)

    with pytest.raises(HTTPException) as error:
        get_current_user(
            HTTPAuthorizationCredentials(scheme="Bearer", credentials=token),
            db_session,
            settings,
        )

    assert error.value.status_code == 403


def test_new_farmer_unique_race_recovers_winner_from_independent_session(
    independent_database,
):
    engine, sessions = independent_database

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return resolve(
                loser,
                uid="farmer-identity-race",
                phone_number="test-phone-race-loser",
            )

    def win_race():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.add(
                User(
                    phone_number="test-phone-race-winner",
                    firebase_uid="farmer-identity-race",
                    role=UserRole.FARMER,
                )
            )
            winner.commit()

    resolved = run_after_committed_winner(
        engine,
        pause_on="INSERT INTO USERS",
        loser=lose_race,
        winner=win_race,
    )

    assert resolved.phone_number == "test-phone-race-winner"


def test_farmer_link_race_recovers_same_winner_from_independent_session(
    independent_database,
):
    engine, sessions = independent_database
    with sessions() as setup:
        farmer = User(phone_number="test-phone-farmer-a", role=UserRole.FARMER)
        setup.add(farmer)
        setup.commit()
        farmer_id = farmer.id
        farmer_phone = farmer.phone_number

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return resolve(
                loser,
                uid="farmer-identity-race",
                phone_number=farmer_phone,
            )

    def win_race():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.execute(
                update(User)
                .where(User.id == farmer_id)
                .values(firebase_uid="farmer-identity-race")
            )
            winner.commit()

    resolved = run_after_committed_winner(
        engine,
        pause_on="UPDATE USERS",
        loser=lose_race,
        winner=win_race,
    )

    assert resolved.id == farmer_id
    assert resolved.firebase_uid == "farmer-identity-race"


def test_farmer_role_transition_race_cannot_bypass_expert_approval(
    independent_database,
):
    engine, sessions = independent_database
    with sessions() as setup:
        farmer = User(phone_number="test-phone-role-race", role=UserRole.FARMER)
        setup.add(farmer)
        setup.commit()
        farmer_id = farmer.id
        farmer_phone = farmer.phone_number

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return resolve(
                loser,
                uid="expert-identity-role-race",
                phone_number=farmer_phone,
            )

    def promote_to_expert():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.execute(
                update(User)
                .where(User.id == farmer_id)
                .values(role=UserRole.AGRONOMIST)
            )
            winner.commit()

    with pytest.raises(HTTPException) as error:
        run_after_committed_winner(
            engine,
            pause_on="UPDATE USERS",
            loser=lose_race,
            winner=promote_to_expert,
        )

    assert error.value.status_code == 409
    assert farmer_phone not in str(error.value.detail)
    assert "expert-identity-role-race" not in str(error.value.detail)
    with sessions() as verification:
        promoted = verification.get(User, farmer_id)
        assert promoted.role is UserRole.AGRONOMIST
        assert promoted.firebase_uid is None


def test_agronomist_approval_race_is_idempotent_across_sessions(
    independent_database,
):
    engine, sessions = independent_database
    with sessions() as setup:
        expert, approval = add_approved_expert(setup)
        expert_id = expert.id
        expert_phone = expert.phone_number
        approval_id = approval.id
        approval_uid = approval.firebase_uid

    def lose_race():
        with sessions() as loser:
            loser.connection().info["race_actor"] = "loser"
            return resolve(
                loser,
                uid=approval_uid,
                phone_number=expert_phone,
            )

    def win_race():
        with sessions() as winner:
            assert winner.connection().info.get("race_actor") is None
            winner.execute(
                update(User)
                .where(User.id == expert_id)
                .values(firebase_uid=approval_uid)
            )
            winner.execute(
                update(FirebaseLinkApproval)
                .where(FirebaseLinkApproval.id == approval_id)
                .values(consumed_at=datetime.now(timezone.utc))
            )
            winner.commit()

    resolved = run_after_committed_winner(
        engine,
        pause_on="UPDATE USERS",
        loser=lose_race,
        winner=win_race,
    )

    assert resolved.id == expert_id
    with sessions() as verification:
        consumed = verification.get(FirebaseLinkApproval, approval_id)
        assert consumed.consumed_at is not None

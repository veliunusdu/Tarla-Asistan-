from datetime import datetime, timedelta, timezone

import pytest
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials
from sqlalchemy import update
from sqlalchemy.orm import sessionmaker

from app.config import Settings
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
    db_session, monkeypatch
):
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
                    User(
                        phone_number="test-phone-race-winner",
                        firebase_uid="farmer-identity-race",
                        role=UserRole.FARMER,
                    )
                )
                winner.commit()
        return original_commit()

    monkeypatch.setattr(db_session, "commit", commit_after_winner)

    resolved = resolve(
        db_session,
        uid="farmer-identity-race",
        phone_number="test-phone-race-loser",
    )

    assert raced is True
    assert resolved.phone_number == "test-phone-race-winner"


def test_farmer_link_race_recovers_same_winner_from_independent_session(
    db_session, monkeypatch
):
    farmer = User(phone_number="test-phone-farmer-a", role=UserRole.FARMER)
    db_session.add(farmer)
    db_session.commit()
    independent_session = sessionmaker(
        bind=db_session.bind,
        autoflush=False,
        expire_on_commit=False,
    )
    original_execute = db_session.execute
    raced = False

    def execute_after_winner(statement, *args, **kwargs):
        nonlocal raced
        if not raced and isinstance(statement, type(update(User))):
            raced = True
            with independent_session() as winner:
                winner.execute(
                    update(User)
                    .where(User.id == farmer.id)
                    .values(firebase_uid="farmer-identity-race")
                )
                winner.commit()
        return original_execute(statement, *args, **kwargs)

    monkeypatch.setattr(db_session, "execute", execute_after_winner)

    resolved = resolve(
        db_session,
        uid="farmer-identity-race",
        phone_number=farmer.phone_number,
    )

    assert raced is True
    assert resolved.id == farmer.id
    assert resolved.firebase_uid == "farmer-identity-race"


def test_agronomist_approval_race_is_idempotent_across_sessions(
    db_session, monkeypatch
):
    expert, approval = add_approved_expert(db_session)
    independent_session = sessionmaker(
        bind=db_session.bind,
        autoflush=False,
        expire_on_commit=False,
    )
    original_execute = db_session.execute
    raced = False

    def execute_after_winner(statement, *args, **kwargs):
        nonlocal raced
        if not raced and isinstance(statement, type(update(User))):
            raced = True
            with independent_session() as winner:
                winner.execute(
                    update(User)
                    .where(User.id == expert.id)
                    .values(firebase_uid=approval.firebase_uid)
                )
                winner.execute(
                    update(FirebaseLinkApproval)
                    .where(FirebaseLinkApproval.id == approval.id)
                    .values(consumed_at=datetime.now(timezone.utc))
                )
                winner.commit()
        return original_execute(statement, *args, **kwargs)

    monkeypatch.setattr(db_session, "execute", execute_after_winner)

    resolved = resolve(
        db_session,
        uid=approval.firebase_uid,
        phone_number=expert.phone_number,
    )

    assert raced is True
    assert resolved.id == expert.id
    db_session.refresh(approval)
    assert approval.consumed_at is not None

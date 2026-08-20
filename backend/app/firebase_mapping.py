import uuid
from datetime import datetime, timezone

from fastapi import HTTPException, status
from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.firebase_auth import FirebaseIdentity
from app.models import (
    AccountDeletionJob,
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
)


def _unauthorized() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="Geçersiz veya süresi dolmuş oturum.",
        headers={"WWW-Authenticate": "Bearer"},
    )


def _mapping_conflict() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_409_CONFLICT,
        detail="Firebase hesabı yerel hesaba bağlanamadı.",
    )


def _approval_required() -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_403_FORBIDDEN,
        detail="Uzman hesabı bağlantısı için operatör onayı gerekiyor.",
    )


def require_active(user: User) -> User:
    if user.account_status is not AccountStatus.ACTIVE:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Hesap aktif değil.",
        )
    return user


def lock_active_user_for_update(db: Session, user_id: uuid.UUID) -> User:
    user = db.scalar(
        select(User)
        .where(User.id == user_id)
        .with_for_update()
        .execution_options(populate_existing=True)
    )
    if user is None:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Hesap aktif değil.",
        )
    return require_active(user)


def _resolve_after_race(
    db: Session, *, uid: str, phone_number: str | None
) -> User:
    user = db.scalar(select(User).where(User.firebase_uid == uid))
    if user is not None:
        return require_active(user)
    if phone_number is not None:
        user = db.scalar(select(User).where(User.phone_number == phone_number))
        if user is not None:
            require_active(user)
            if user.firebase_uid == uid:
                return user
    raise _mapping_conflict()


def _finish_conditional_link(
    db: Session, *, uid: str, phone_number: str
) -> User:
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        try:
            return _resolve_after_race(
                db,
                uid=uid,
                phone_number=phone_number,
            )
        except HTTPException as conflict:
            raise conflict from None
    db.expire_all()
    return _resolve_after_race(db, uid=uid, phone_number=phone_number)


def _create_farmer_with_uid(db: Session, identity: FirebaseIdentity) -> User:
    if identity.phone_number is None:
        raise _unauthorized()
    user = User(
        phone_number=identity.phone_number,
        firebase_uid=identity.uid,
        role=UserRole.FARMER,
    )
    db.add(user)
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        try:
            return _resolve_after_race(
                db,
                uid=identity.uid,
                phone_number=identity.phone_number,
            )
        except HTTPException as conflict:
            raise conflict from None
    db.refresh(user)
    return require_active(user)


def _link_farmer(db: Session, *, user: User, uid: str) -> User:
    result = db.execute(
        update(User)
        .where(
            User.id == user.id,
            User.firebase_uid.is_(None),
            User.account_status == AccountStatus.ACTIVE,
            User.role == UserRole.FARMER,
        )
        .values(firebase_uid=uid)
        .execution_options(synchronize_session=False)
    )
    if result.rowcount != 1:
        db.rollback()
        return _resolve_after_race(
            db,
            uid=uid,
            phone_number=user.phone_number,
        )
    return _finish_conditional_link(
        db,
        uid=uid,
        phone_number=user.phone_number,
    )


def _consume_approval_and_link(db: Session, *, user: User, uid: str) -> User:
    now = datetime.now(timezone.utc)
    user_result = db.execute(
        update(User)
        .where(
            User.id == user.id,
            User.firebase_uid.is_(None),
            User.account_status == AccountStatus.ACTIVE,
        )
        .values(firebase_uid=uid)
        .execution_options(synchronize_session=False)
    )
    if user_result.rowcount != 1:
        db.rollback()
        return _resolve_after_race(
            db,
            uid=uid,
            phone_number=user.phone_number,
        )

    approval_result = db.execute(
        update(FirebaseLinkApproval)
        .where(
            FirebaseLinkApproval.user_id == user.id,
            FirebaseLinkApproval.firebase_uid == uid,
            FirebaseLinkApproval.consumed_at.is_(None),
            FirebaseLinkApproval.expires_at > now,
        )
        .values(consumed_at=now)
        .execution_options(synchronize_session=False)
    )
    if approval_result.rowcount != 1:
        db.rollback()
        try:
            return _resolve_after_race(
                db,
                uid=uid,
                phone_number=user.phone_number,
            )
        except HTTPException as error:
            if error.status_code != status.HTTP_409_CONFLICT:
                raise
            raise _approval_required() from None

    return _finish_conditional_link(
        db,
        uid=uid,
        phone_number=user.phone_number,
    )


def resolve_firebase_user(db: Session, identity: FirebaseIdentity) -> User:
    deletion_job = db.scalar(
        select(AccountDeletionJob).where(
            AccountDeletionJob.firebase_uid_snapshot == identity.uid
        )
    )
    if deletion_job is not None:
        return require_active(deletion_job.user)
    existing_uid = db.scalar(select(User).where(User.firebase_uid == identity.uid))
    if existing_uid is not None:
        return require_active(existing_uid)
    if not identity.phone_number:
        raise _unauthorized()

    user = db.scalar(select(User).where(User.phone_number == identity.phone_number))
    if user is None:
        return _create_farmer_with_uid(db, identity)
    require_active(user)
    if user.firebase_uid is not None:
        raise _mapping_conflict()
    if user.role is UserRole.AGRONOMIST:
        return _consume_approval_and_link(db, user=user, uid=identity.uid)
    return _link_farmer(db, user=user, uid=identity.uid)

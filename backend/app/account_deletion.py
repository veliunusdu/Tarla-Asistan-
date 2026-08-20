import uuid
import logging
from collections.abc import Callable
from datetime import datetime, timedelta
from typing import Any

from sqlalchemy import select, update
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import SessionLocal
from app.firebase_account import (
    AccountDeletionProviderError,
    FirebaseAdminAccountGateway,
    FirebaseAccountGateway,
)
from app.media_storage import (
    MediaStorage,
    MediaStorageError,
    MediaStorageMissing,
    create_media_storage,
)
from app.models import (
    AccountDeletionJob,
    AccountDeletionStatus,
    AccountStatus,
    DeviceToken,
    MediaAsset,
    RefreshToken,
    User,
    utcnow,
)

MEDIA_STORAGE_UNAVAILABLE = "MEDIA_STORAGE_UNAVAILABLE"
ACCOUNT_DELETION_UNEXPECTED = "ACCOUNT_DELETION_UNEXPECTED"
logger = logging.getLogger(__name__)


class _NoopFirebaseAccountGateway:
    def revoke_tokens(self, uid: str) -> None:
        pass

    def anonymize_firestore(self, uid: str, anonymous_subject: str) -> None:
        pass

    def delete_auth_user(self, uid: str) -> None:
        pass


def request_account_deletion(
    db: Session, user: User, now: datetime
) -> AccountDeletionJob:
    locked_user = db.scalar(select(User).where(User.id == user.id).with_for_update())
    if locked_user is None:
        raise ValueError("Account deletion user was not found.")
    existing = db.scalar(
        select(AccountDeletionJob).where(AccountDeletionJob.user_id == locked_user.id)
    )
    if existing is not None:
        return existing

    locked_user.account_status = AccountStatus.DELETION_PENDING
    if locked_user.anonymized_subject_id is None:
        locked_user.anonymized_subject_id = f"anon-{uuid.uuid4().hex}"
    db.execute(
        update(RefreshToken)
        .where(
            RefreshToken.user_id == locked_user.id,
            RefreshToken.revoked_at.is_(None),
        )
        .values(revoked_at=now)
        .execution_options(synchronize_session=False)
    )
    job = AccountDeletionJob(
        user_id=locked_user.id,
        firebase_uid_snapshot=locked_user.firebase_uid,
        status=AccountDeletionStatus.PENDING,
    )
    db.add(job)
    db.commit()
    db.refresh(job)
    return job


def _commit_step(db: Session, job: AccountDeletionJob, field: str, now: datetime) -> None:
    setattr(job, field, now)
    db.commit()


def _mark_retry_required(
    db: Session,
    job: AccountDeletionJob,
    *,
    error_code: str,
    settings: Settings,
    now: datetime,
) -> AccountDeletionJob:
    job.status = AccountDeletionStatus.RETRY_REQUIRED
    job.last_error_code = error_code
    job.next_retry_at = now + timedelta(
        minutes=settings.account_deletion_retry_minutes
    )
    db.commit()
    return job


def process_account_deletion(
    db: Session,
    job_id: uuid.UUID,
    gateway: FirebaseAccountGateway,
    storage: MediaStorage,
    settings: Settings,
    now: datetime,
) -> AccountDeletionJob:
    job = db.scalar(
        select(AccountDeletionJob)
        .where(AccountDeletionJob.id == job_id)
        .with_for_update()
    )
    if job is None:
        raise ValueError("Account deletion job was not found.")
    if job.status in {
        AccountDeletionStatus.PROCESSING,
        AccountDeletionStatus.COMPLETED,
    }:
        return job

    job.status = AccountDeletionStatus.PROCESSING
    job.attempt_count += 1
    job.last_error_code = None
    job.next_retry_at = None
    db.commit()

    uid = job.firebase_uid_snapshot
    try:
        if job.firebase_tokens_revoked_at is None:
            if uid is not None:
                gateway.revoke_tokens(uid)
            _commit_step(db, job, "firebase_tokens_revoked_at", now)

        if job.firestore_anonymized_at is None:
            if uid is not None:
                gateway.anonymize_firestore(uid, job.user.anonymized_subject_id)
            _commit_step(db, job, "firestore_anonymized_at", now)
    except AccountDeletionProviderError as error:
        return _mark_retry_required(
            db,
            job,
            error_code=error.code,
            settings=settings,
            now=now,
        )

    if job.media_deleted_at is None:
        media_assets = db.scalars(
            select(MediaAsset).where(MediaAsset.owner_id == job.user_id)
        ).all()
        try:
            for asset in media_assets:
                try:
                    storage.delete(asset.storage_key)
                except MediaStorageMissing:
                    pass
        except MediaStorageError:
            return _mark_retry_required(
                db,
                job,
                error_code=MEDIA_STORAGE_UNAVAILABLE,
                settings=settings,
                now=now,
            )
        _commit_step(db, job, "media_deleted_at", now)

    try:
        if job.firebase_auth_deleted_at is None:
            if uid is not None:
                gateway.delete_auth_user(uid)
            _commit_step(db, job, "firebase_auth_deleted_at", now)
    except AccountDeletionProviderError as error:
        return _mark_retry_required(
            db,
            job,
            error_code=error.code,
            settings=settings,
            now=now,
        )

    user = job.user
    user.phone_number = f"deleted-{user.anonymized_subject_id}"
    user.firebase_uid = None
    user.account_status = AccountStatus.ANONYMIZED
    user.deleted_at = now
    if user.profile is not None:
        user.profile.full_name = None
        user.profile.province = None
        user.profile.district = None
        user.profile.notifications_enabled = False
    db.execute(
        update(DeviceToken)
        .where(DeviceToken.user_id == user.id)
        .values(active=False, updated_at=now)
        .execution_options(synchronize_session=False)
    )
    job.postgres_anonymized_at = now
    job.status = AccountDeletionStatus.COMPLETED
    job.completed_at = now
    job.last_error_code = None
    job.next_retry_at = None
    db.commit()
    return job


def _create_gateway(settings: Settings) -> FirebaseAccountGateway:
    return FirebaseAdminAccountGateway(settings=settings)


def process_account_deletion_by_id(
    job_id: uuid.UUID,
    *,
    session_factory: Callable[[], Any] = SessionLocal,
    gateway_factory: Callable[[Settings], FirebaseAccountGateway] = _create_gateway,
    storage_factory: Callable[[Settings], MediaStorage] = create_media_storage,
    settings: Settings | None = None,
    now_factory: Callable[[], datetime] = utcnow,
    force: bool = False,
) -> AccountDeletionJob:
    resolved_settings = settings or get_settings()
    now = now_factory()
    with session_factory() as db:
        job = db.get(AccountDeletionJob, job_id)
        if job is None:
            raise ValueError("Account deletion job was not found.")
        if job.status is AccountDeletionStatus.COMPLETED:
            return job
        if not force and not _is_automatic_retry_eligible(
            job, resolved_settings, now
        ):
            return job
        if job.firebase_uid_snapshot is None:
            gateway: FirebaseAccountGateway = _NoopFirebaseAccountGateway()
        else:
            try:
                gateway = gateway_factory(resolved_settings)
            except AccountDeletionProviderError as error:
                job.attempt_count += 1
                return _mark_retry_required(
                    db,
                    job,
                    error_code=error.code,
                    settings=resolved_settings,
                    now=now,
                )
        try:
            storage = storage_factory(resolved_settings)
        except Exception:
            job.attempt_count += 1
            return _mark_retry_required(
                db,
                job,
                error_code=MEDIA_STORAGE_UNAVAILABLE,
                settings=resolved_settings,
                now=now,
            )
        return process_account_deletion(
            db,
            job_id,
            gateway,
            storage,
            resolved_settings,
            now,
        )


def _aware(value: datetime) -> datetime:
    return value if value.tzinfo is not None else value.replace(tzinfo=utcnow().tzinfo)


def _is_automatic_retry_eligible(
    job: AccountDeletionJob, settings: Settings, now: datetime
) -> bool:
    return (
        job.status
        in {
            AccountDeletionStatus.PENDING,
            AccountDeletionStatus.RETRY_REQUIRED,
        }
        and job.attempt_count < settings.account_deletion_max_automatic_attempts
        and (
            job.status is AccountDeletionStatus.PENDING
            or job.next_retry_at is None
            or _aware(job.next_retry_at) <= _aware(now)
        )
    )


def eligible_account_deletion_jobs(
    db: Session,
    settings: Settings,
    now: datetime,
    *,
    automatic: bool,
    job_id: uuid.UUID | None = None,
) -> list[AccountDeletionJob]:
    statement = (
        select(AccountDeletionJob)
        .where(
            AccountDeletionJob.status.in_(
                [
                    AccountDeletionStatus.PENDING,
                    AccountDeletionStatus.RETRY_REQUIRED,
                ]
            )
        )
        .order_by(AccountDeletionJob.created_at, AccountDeletionJob.id)
    )
    if job_id is not None:
        statement = statement.where(AccountDeletionJob.id == job_id)
    jobs = list(db.scalars(statement))
    if not automatic:
        return jobs
    return [
        job
        for job in jobs
        if _is_automatic_retry_eligible(job, settings, now)
    ]


def run_account_deletion_retries(
    db: Session,
    *,
    settings: Settings,
    now: datetime,
    processor: Callable[[uuid.UUID], AccountDeletionJob | None],
    automatic: bool,
    job_id: uuid.UUID | None = None,
) -> list[uuid.UUID]:
    jobs = eligible_account_deletion_jobs(
        db,
        settings,
        now,
        automatic=automatic,
        job_id=job_id,
    )
    processed: list[uuid.UUID] = []
    for job in jobs:
        processed.append(job.id)
        try:
            result = processor(job.id)
        except Exception:
            logger.error(
                "account_deletion_retry_failed job_id=%s error_code=%s",
                job.id,
                ACCOUNT_DELETION_UNEXPECTED,
            )
            continue
        if (
            result is not None
            and result.status is AccountDeletionStatus.RETRY_REQUIRED
        ):
            logger.warning(
                "account_deletion_retry_required job_id=%s error_code=%s",
                job.id,
                result.last_error_code or ACCOUNT_DELETION_UNEXPECTED,
            )
    return processed


def run_startup_account_deletion_retries() -> list[uuid.UUID]:
    resolved_settings = get_settings()
    with SessionLocal() as db:
        return run_account_deletion_retries(
            db,
            settings=resolved_settings,
            now=utcnow(),
            processor=process_account_deletion_by_id,
            automatic=True,
        )

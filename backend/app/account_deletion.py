import logging
import uuid
from collections.abc import Callable
from datetime import datetime, timedelta, timezone
from typing import Any

from sqlalchemy import and_, or_, select, update
from sqlalchemy.orm import Session, attributes

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

_JOB_DATETIME_FIELDS = (
    "next_retry_at",
    "processing_started_at",
    "lease_until",
    "firebase_tokens_revoked_at",
    "firestore_anonymized_at",
    "media_deleted_at",
    "firebase_auth_deleted_at",
    "postgres_anonymized_at",
    "created_at",
    "updated_at",
    "completed_at",
)


def _normalize_job_datetimes(job: AccountDeletionJob) -> AccountDeletionJob:
    for field in _JOB_DATETIME_FIELDS:
        value = getattr(job, field)
        if value is not None and value.tzinfo is None:
            attributes.set_committed_value(
                job,
                field,
                value.replace(tzinfo=timezone.utc),
            )
    return job


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


def _automatic_eligibility_clause(settings: Settings, now: datetime):
    return and_(
        AccountDeletionJob.attempt_count
        < settings.account_deletion_max_automatic_attempts,
        or_(
            AccountDeletionJob.status == AccountDeletionStatus.PENDING,
            and_(
                AccountDeletionJob.status == AccountDeletionStatus.RETRY_REQUIRED,
                or_(
                    AccountDeletionJob.next_retry_at.is_(None),
                    AccountDeletionJob.next_retry_at <= now,
                ),
            ),
            and_(
                AccountDeletionJob.status == AccountDeletionStatus.PROCESSING,
                or_(
                    AccountDeletionJob.lease_until.is_(None),
                    AccountDeletionJob.lease_until <= now,
                ),
            ),
        ),
    )


def _forced_eligibility_clause(now: datetime):
    return or_(
        AccountDeletionJob.status.in_(
            [
                AccountDeletionStatus.PENDING,
                AccountDeletionStatus.RETRY_REQUIRED,
            ]
        ),
        and_(
            AccountDeletionJob.status == AccountDeletionStatus.PROCESSING,
            or_(
                AccountDeletionJob.lease_until.is_(None),
                AccountDeletionJob.lease_until <= now,
            ),
        ),
    )


def _claim_account_deletion_job(
    db: Session,
    job_id: uuid.UUID,
    *,
    settings: Settings,
    now: datetime,
    owner_token: str,
    force: bool,
) -> tuple[AccountDeletionJob | None, bool]:
    eligibility = (
        _forced_eligibility_clause(now)
        if force
        else _automatic_eligibility_clause(settings, now)
    )
    claimed_id = db.execute(
        update(AccountDeletionJob)
        .where(AccountDeletionJob.id == job_id, eligibility)
        .values(
            status=AccountDeletionStatus.PROCESSING,
            attempt_count=AccountDeletionJob.attempt_count + 1,
            last_error_code=None,
            next_retry_at=None,
            processing_started_at=now,
            lease_until=now
            + timedelta(minutes=settings.account_deletion_processing_lease_minutes),
            processing_owner_token=owner_token,
        )
        .returning(AccountDeletionJob.id)
        .execution_options(synchronize_session=False)
    ).scalar_one_or_none()
    if claimed_id is None:
        db.rollback()
        current = db.get(AccountDeletionJob, job_id, populate_existing=True)
        return (
            _normalize_job_datetimes(current) if current is not None else None,
            False,
        )
    db.commit()
    claimed = db.get(AccountDeletionJob, claimed_id, populate_existing=True)
    return (
        _normalize_job_datetimes(claimed) if claimed is not None else None,
        True,
    )


def _owns_processing_lease(job: AccountDeletionJob, owner_token: str) -> bool:
    return (
        job.status is AccountDeletionStatus.PROCESSING
        and job.processing_owner_token == owner_token
    )


def _owned_job(
    db: Session, job_id: uuid.UUID, owner_token: str
) -> AccountDeletionJob | None:
    job = db.get(AccountDeletionJob, job_id, populate_existing=True)
    if job is None:
        return None
    _normalize_job_datetimes(job)
    return job if _owns_processing_lease(job, owner_token) else None


def _current_job(db: Session, job_id: uuid.UUID) -> AccountDeletionJob:
    job = db.get(AccountDeletionJob, job_id, populate_existing=True)
    if job is None:
        raise ValueError("Account deletion job was not found.")
    return _normalize_job_datetimes(job)


def _commit_step(
    db: Session,
    job: AccountDeletionJob,
    field: str,
    *,
    owner_token: str,
    now: datetime,
) -> AccountDeletionJob:
    result = db.execute(
        update(AccountDeletionJob)
        .where(
            AccountDeletionJob.id == job.id,
            AccountDeletionJob.status == AccountDeletionStatus.PROCESSING,
            AccountDeletionJob.processing_owner_token == owner_token,
            getattr(AccountDeletionJob, field).is_(None),
        )
        .values({field: now})
        .execution_options(synchronize_session="fetch")
    )
    if result.rowcount != 1:
        db.rollback()
        return _current_job(db, job.id)
    db.refresh(job)
    _normalize_job_datetimes(job)
    db.commit()
    return job


def _mark_retry_required(
    db: Session,
    job: AccountDeletionJob,
    *,
    owner_token: str,
    error_code: str,
    settings: Settings,
    now: datetime,
) -> AccountDeletionJob:
    result = db.execute(
        update(AccountDeletionJob)
        .where(
            AccountDeletionJob.id == job.id,
            AccountDeletionJob.status == AccountDeletionStatus.PROCESSING,
            AccountDeletionJob.processing_owner_token == owner_token,
        )
        .values(
            status=AccountDeletionStatus.RETRY_REQUIRED,
            last_error_code=error_code,
            next_retry_at=now
            + timedelta(minutes=settings.account_deletion_retry_minutes),
            processing_started_at=None,
            lease_until=None,
            processing_owner_token=None,
        )
        .execution_options(synchronize_session="fetch")
    )
    if result.rowcount != 1:
        db.rollback()
        return _current_job(db, job.id)
    db.refresh(job)
    _normalize_job_datetimes(job)
    db.commit()
    return job


def _process_claimed_account_deletion(
    db: Session,
    job: AccountDeletionJob,
    *,
    owner_token: str,
    gateway_factory: Callable[[Settings], FirebaseAccountGateway],
    storage_factory: Callable[[Settings], MediaStorage],
    settings: Settings,
    now: datetime,
) -> AccountDeletionJob:
    gateway: FirebaseAccountGateway | None = None
    uid = job.firebase_uid_snapshot

    try:
        job = _owned_job(db, job.id, owner_token) or _current_job(db, job.id)
        if not _owns_processing_lease(job, owner_token):
            return job
        if job.firebase_tokens_revoked_at is None:
            if uid is not None:
                gateway = gateway_factory(settings)
                gateway.revoke_tokens(uid)
            job = _commit_step(
                db,
                job,
                "firebase_tokens_revoked_at",
                owner_token=owner_token,
                now=now,
            )
            if not _owns_processing_lease(job, owner_token):
                return job

        job = _owned_job(db, job.id, owner_token) or _current_job(db, job.id)
        if not _owns_processing_lease(job, owner_token):
            return job
        if job.firestore_anonymized_at is None:
            if uid is not None:
                gateway = gateway or gateway_factory(settings)
                gateway.anonymize_firestore(uid, job.user.anonymized_subject_id)
            job = _commit_step(
                db,
                job,
                "firestore_anonymized_at",
                owner_token=owner_token,
                now=now,
            )
            if not _owns_processing_lease(job, owner_token):
                return job
    except AccountDeletionProviderError as error:
        return _mark_retry_required(
            db,
            job,
            owner_token=owner_token,
            error_code=error.code,
            settings=settings,
            now=now,
        )

    job = _owned_job(db, job.id, owner_token) or _current_job(db, job.id)
    if not _owns_processing_lease(job, owner_token):
        return job
    if job.media_deleted_at is None:
        media_assets = db.scalars(
            select(MediaAsset).where(MediaAsset.owner_id == job.user_id)
        ).all()
        if media_assets:
            try:
                storage = storage_factory(settings)
            except Exception:
                return _mark_retry_required(
                    db,
                    job,
                    owner_token=owner_token,
                    error_code=MEDIA_STORAGE_UNAVAILABLE,
                    settings=settings,
                    now=now,
                )
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
                    owner_token=owner_token,
                    error_code=MEDIA_STORAGE_UNAVAILABLE,
                    settings=settings,
                    now=now,
                )
        job = _commit_step(
            db,
            job,
            "media_deleted_at",
            owner_token=owner_token,
            now=now,
        )
        if not _owns_processing_lease(job, owner_token):
            return job

    try:
        job = _owned_job(db, job.id, owner_token) or _current_job(db, job.id)
        if not _owns_processing_lease(job, owner_token):
            return job
        if job.firebase_auth_deleted_at is None:
            if uid is not None:
                gateway = gateway or gateway_factory(settings)
                gateway.delete_auth_user(uid)
            job = _commit_step(
                db,
                job,
                "firebase_auth_deleted_at",
                owner_token=owner_token,
                now=now,
            )
            if not _owns_processing_lease(job, owner_token):
                return job
    except AccountDeletionProviderError as error:
        return _mark_retry_required(
            db,
            job,
            owner_token=owner_token,
            error_code=error.code,
            settings=settings,
            now=now,
        )

    job_id = job.id
    job = db.scalar(
        select(AccountDeletionJob)
        .where(
            AccountDeletionJob.id == job_id,
            AccountDeletionJob.status == AccountDeletionStatus.PROCESSING,
            AccountDeletionJob.processing_owner_token == owner_token,
        )
        .with_for_update()
        .execution_options(populate_existing=True)
    )
    if job is None:
        db.rollback()
        return _current_job(db, job_id)
    user = db.scalar(
        select(User)
        .where(User.id == job.user_id)
        .with_for_update()
        .execution_options(populate_existing=True)
    )
    if user is None:
        raise ValueError("Account deletion user was not found.")
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
    job.processing_started_at = None
    job.lease_until = None
    job.processing_owner_token = None
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
    owner_token = uuid.uuid4().hex
    job, claimed = _claim_account_deletion_job(
        db,
        job_id,
        settings=settings,
        now=now,
        owner_token=owner_token,
        force=True,
    )
    if job is None:
        raise ValueError("Account deletion job was not found.")
    if not claimed:
        return job
    return _process_claimed_account_deletion(
        db,
        job,
        owner_token=owner_token,
        gateway_factory=lambda _settings: gateway,
        storage_factory=lambda _settings: storage,
        settings=settings,
        now=now,
    )


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
    owner_token: str | None = None,
) -> AccountDeletionJob:
    resolved_settings = settings or get_settings()
    now = now_factory()
    resolved_owner_token = owner_token or uuid.uuid4().hex
    with session_factory() as db:
        job, claimed = _claim_account_deletion_job(
            db,
            job_id,
            settings=resolved_settings,
            now=now,
            owner_token=resolved_owner_token,
            force=force,
        )
        if job is None:
            raise ValueError("Account deletion job was not found.")
        if not claimed:
            return job
        try:
            return _process_claimed_account_deletion(
                db,
                job,
                owner_token=resolved_owner_token,
                gateway_factory=gateway_factory,
                storage_factory=storage_factory,
                settings=resolved_settings,
                now=now,
            )
        except Exception:
            db.rollback()
            raise


def _mark_retry_required_in_fresh_session(
    job_id: uuid.UUID,
    *,
    owner_token: str,
    error_code: str,
    session_factory: Callable[[], Any],
    settings: Settings,
    now: datetime,
) -> AccountDeletionJob | None:
    with session_factory() as db:
        job = db.get(AccountDeletionJob, job_id, populate_existing=True)
        if job is None:
            return None
        return _mark_retry_required(
            db,
            job,
            owner_token=owner_token,
            error_code=error_code,
            settings=settings,
            now=now,
        )


def process_account_deletion_safely(
    job_id: uuid.UUID,
    *,
    session_factory: Callable[[], Any] = SessionLocal,
    gateway_factory: Callable[[Settings], FirebaseAccountGateway] = _create_gateway,
    storage_factory: Callable[[Settings], MediaStorage] = create_media_storage,
    settings: Settings | None = None,
    now_factory: Callable[[], datetime] = utcnow,
    force: bool = False,
) -> AccountDeletionJob | None:
    resolved_settings = settings or get_settings()
    now = now_factory()
    owner_token = uuid.uuid4().hex
    try:
        return process_account_deletion_by_id(
            job_id,
            session_factory=session_factory,
            gateway_factory=gateway_factory,
            storage_factory=storage_factory,
            settings=resolved_settings,
            now_factory=lambda: now,
            force=force,
            owner_token=owner_token,
        )
    except Exception:
        try:
            result = _mark_retry_required_in_fresh_session(
                job_id,
                owner_token=owner_token,
                error_code=ACCOUNT_DELETION_UNEXPECTED,
                session_factory=session_factory,
                settings=resolved_settings,
                now=now,
            )
        except Exception:
            result = None
        logger.error(
            "account_deletion_processing_failed job_id=%s error_code=%s",
            job_id,
            ACCOUNT_DELETION_UNEXPECTED,
        )
        return result


def eligible_account_deletion_jobs(
    db: Session,
    settings: Settings,
    now: datetime,
    *,
    automatic: bool,
    job_id: uuid.UUID | None = None,
    limit: int | None = None,
) -> list[AccountDeletionJob]:
    eligibility = (
        _automatic_eligibility_clause(settings, now)
        if automatic
        else _forced_eligibility_clause(now)
    )
    statement = select(AccountDeletionJob).where(eligibility).order_by(
        AccountDeletionJob.created_at, AccountDeletionJob.id
    )
    if job_id is not None:
        statement = statement.where(AccountDeletionJob.id == job_id)
    if limit is not None:
        statement = statement.limit(limit)
    return list(db.scalars(statement))


def run_account_deletion_retries(
    db: Session,
    *,
    settings: Settings,
    now: datetime,
    processor: Callable[[uuid.UUID], AccountDeletionJob | None],
    automatic: bool,
    job_id: uuid.UUID | None = None,
    limit: int | None = None,
) -> list[uuid.UUID]:
    jobs = eligible_account_deletion_jobs(
        db,
        settings,
        now,
        automatic=automatic,
        job_id=job_id,
        limit=limit,
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
            processor=process_account_deletion_safely,
            automatic=True,
            limit=resolved_settings.account_deletion_startup_batch_limit,
        )

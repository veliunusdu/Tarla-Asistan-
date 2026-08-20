import argparse
import sys
from collections.abc import Callable, Sequence
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import UUID

from sqlalchemy import or_, select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.account_deletion import (
    eligible_account_deletion_jobs,
    process_account_deletion_safely,
)
from app.config import get_settings
from app.database import SessionLocal
from app.models import AccountStatus, FirebaseLinkApproval, User, UserRole


class ManagementCommandError(RuntimeError):
    pass


class _SafeArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise ManagementCommandError("Geçersiz komut seçenekleri.")


def _validate_approval_input(*, firebase_uid: str, operator: str) -> None:
    if not firebase_uid or len(firebase_uid) > 128:
        raise ManagementCommandError("Firebase kimliği geçerli değil.")
    if not operator.strip() or len(operator) > 120:
        raise ManagementCommandError("Operatör tanımı geçerli değil.")


def approve_firebase_link(
    db: Session,
    *,
    user_id: UUID,
    firebase_uid: str,
    operator: str,
    now: datetime,
    dry_run: bool = False,
) -> FirebaseLinkApproval | None:
    _validate_approval_input(firebase_uid=firebase_uid, operator=operator)
    user = db.get(User, user_id)
    if (
        user is None
        or user.role is not UserRole.AGRONOMIST
        or user.account_status is not AccountStatus.ACTIVE
        or user.firebase_uid is not None
    ):
        raise ManagementCommandError("Hedef kullanıcı uygun bir uzman hesabı değil.")

    linked_user = db.scalar(select(User).where(User.firebase_uid == firebase_uid))
    if linked_user is not None:
        raise ManagementCommandError("Firebase kimliği zaten bağlı.")

    active_approval = db.scalar(
        select(FirebaseLinkApproval).where(
            FirebaseLinkApproval.consumed_at.is_(None),
            FirebaseLinkApproval.expires_at > now,
            or_(
                FirebaseLinkApproval.user_id == user_id,
                FirebaseLinkApproval.firebase_uid == firebase_uid,
            ),
        )
    )
    if active_approval is not None:
        raise ManagementCommandError(
            "Hedef kullanıcı veya Firebase kimliği için aktif bir onay var."
        )
    if dry_run:
        return None

    expired_approvals = (
        update(FirebaseLinkApproval)
        .where(
            FirebaseLinkApproval.consumed_at.is_(None),
            FirebaseLinkApproval.expires_at <= now,
            or_(
                FirebaseLinkApproval.user_id == user_id,
                FirebaseLinkApproval.firebase_uid == firebase_uid,
            ),
        )
        .values(consumed_at=now)
        .execution_options(synchronize_session=False)
    )
    db.execute(expired_approvals)
    approval = FirebaseLinkApproval(
        user_id=user_id,
        firebase_uid=firebase_uid,
        approved_by=operator.strip(),
        approved_at=now,
        expires_at=now + timedelta(hours=24),
    )
    db.add(approval)
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        raise ManagementCommandError(
            "Onay başka bir işlemle çakıştı; yeniden deneyin."
        ) from None
    db.refresh(approval)
    return approval


def _build_parser() -> argparse.ArgumentParser:
    parser = _SafeArgumentParser(prog="python -m app.manage")
    commands = parser.add_subparsers(dest="command", required=True)
    approval = commands.add_parser("approve-firebase-link")
    approval.add_argument("--user-id", required=True)
    approval.add_argument("--firebase-uid", required=True)
    approval.add_argument("--operator", required=True)
    approval.add_argument("--dry-run", action="store_true")
    deletion_retry = commands.add_parser("retry-account-deletions")
    deletion_retry.add_argument("--job-id")
    deletion_retry.add_argument("--dry-run", action="store_true")
    return parser


def main(
    argv: Sequence[str] | None = None,
    *,
    session_factory: Callable[[], Any] | None = None,
    now_factory: Callable[[], datetime] | None = None,
) -> int:
    try:
        args = _build_parser().parse_args(argv)
        create_session = session_factory or SessionLocal
        current_time = (now_factory or (lambda: datetime.now(timezone.utc)))()
        if args.command == "approve-firebase-link":
            try:
                user_id = UUID(args.user_id)
            except (TypeError, ValueError) as exc:
                raise ManagementCommandError(
                    "Kullanıcı kimliği geçerli değil."
                ) from exc
            with create_session() as db:
                approval = approve_firebase_link(
                    db,
                    user_id=user_id,
                    firebase_uid=args.firebase_uid,
                    operator=args.operator,
                    now=current_time,
                    dry_run=args.dry_run,
                )
            if args.dry_run:
                print(f"Dry-run başarılı: user_id={user_id}")
            else:
                assert approval is not None
                print(
                    "Onay oluşturuldu: "
                    f"user_id={user_id} expires_at={approval.expires_at.isoformat()}"
                )
            return 0

        requested_job_id = None
        if args.job_id is not None:
            try:
                requested_job_id = UUID(args.job_id)
            except (TypeError, ValueError) as exc:
                raise ManagementCommandError(
                    "Silme işi kimliği geçerli değil."
                ) from exc
        with create_session() as db:
            jobs = eligible_account_deletion_jobs(
                db,
                get_settings(),
                current_time,
                automatic=requested_job_id is None,
                job_id=requested_job_id,
            )
        for job in jobs:
            if not args.dry_run:
                process_account_deletion_safely(
                    job.id, force=requested_job_id is not None
                )
            print(f"job_id={job.id} status={job.status.value}")
        return 0
    except ManagementCommandError as exc:
        print(f"Hata: {exc}", file=sys.stderr)
        return 2
    except Exception:
        print("Hata: Komut güvenli şekilde tamamlanamadı.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

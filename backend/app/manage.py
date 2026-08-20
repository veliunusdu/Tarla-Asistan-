import argparse
import sys
from collections.abc import Callable, Sequence
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import UUID

from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

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

    identity_approval = db.scalar(
        select(FirebaseLinkApproval).where(
            FirebaseLinkApproval.firebase_uid == firebase_uid
        )
    )
    if identity_approval is not None and identity_approval.user_id != user_id:
        raise ManagementCommandError("Firebase kimliği için başka bir onay var.")

    active_approval = db.scalar(
        select(FirebaseLinkApproval).where(
            FirebaseLinkApproval.user_id == user_id,
            FirebaseLinkApproval.consumed_at.is_(None),
            FirebaseLinkApproval.expires_at > now,
        )
    )
    if active_approval is not None:
        raise ManagementCommandError("Hedef kullanıcı için aktif bir onay var.")
    if dry_run:
        return None

    expired_approvals = (
        update(FirebaseLinkApproval)
        .where(
            FirebaseLinkApproval.user_id == user_id,
            FirebaseLinkApproval.consumed_at.is_(None),
            FirebaseLinkApproval.expires_at <= now,
        )
        .values(consumed_at=now)
        .execution_options(synchronize_session=False)
    )
    if identity_approval is not None:
        expired_approvals = expired_approvals.where(
            FirebaseLinkApproval.id != identity_approval.id
        )
    db.execute(expired_approvals)
    if identity_approval is None:
        approval = FirebaseLinkApproval(
            user_id=user_id,
            firebase_uid=firebase_uid,
            approved_by=operator.strip(),
            approved_at=now,
            expires_at=now + timedelta(hours=24),
        )
        db.add(approval)
    else:
        approval = identity_approval
        approval.approved_by = operator.strip()
        approval.approved_at = now
        approval.expires_at = now + timedelta(hours=24)
        approval.consumed_at = None
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
    return parser


def main(
    argv: Sequence[str] | None = None,
    *,
    session_factory: Callable[[], Any] | None = None,
    now_factory: Callable[[], datetime] | None = None,
) -> int:
    try:
        args = _build_parser().parse_args(argv)
        try:
            user_id = UUID(args.user_id)
        except (TypeError, ValueError) as exc:
            raise ManagementCommandError("Kullanıcı kimliği geçerli değil.") from exc

        create_session = session_factory or SessionLocal
        current_time = (now_factory or (lambda: datetime.now(timezone.utc)))()
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
    except ManagementCommandError as exc:
        print(f"Hata: {exc}", file=sys.stderr)
        return 2
    except Exception:
        print("Hata: Komut güvenli şekilde tamamlanamadı.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

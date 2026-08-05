import uuid
import logging

from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.models import (
    DeviceToken,
    Notification,
    NotificationStatus,
    NotificationType,
    User,
    utcnow,
)
from app.push import PushProvider, PushProviderError

logger = logging.getLogger(__name__)


def notify_user(
    db: Session,
    provider: PushProvider,
    *,
    user_id: uuid.UUID,
    notification_type: NotificationType,
    title: str,
    body: str,
    deep_link: str,
    data: dict[str, str],
    dedupe_key: str,
) -> Notification | None:
    existing = db.scalar(
        select(Notification).where(Notification.dedupe_key == dedupe_key)
    )
    if existing is not None:
        return existing
    user = db.get(User, user_id)
    if user is None or not user.notifications_enabled:
        return None
    notification = Notification(
        user_id=user.id,
        notification_type=notification_type,
        title=title,
        body=body,
        deep_link=deep_link,
        data=data,
        dedupe_key=dedupe_key,
        status=NotificationStatus.PENDING,
    )
    db.add(notification)
    try:
        db.commit()
    except IntegrityError:
        db.rollback()
        return db.scalar(
            select(Notification).where(Notification.dedupe_key == dedupe_key)
        )
    db.refresh(notification)
    return dispatch_notification(db, provider, notification)


def safe_notify_user(
    db: Session,
    provider: PushProvider,
    **kwargs,
) -> Notification | None:
    try:
        return notify_user(db, provider, **kwargs)
    except Exception:
        db.rollback()
        logger.exception(
            "notification_delivery_failed",
            extra={"notification_dedupe_key": kwargs.get("dedupe_key")},
        )
        return None


def dispatch_notification(
    db: Session,
    provider: PushProvider,
    notification: Notification,
) -> Notification:
    tokens = db.scalars(
        select(DeviceToken).where(
            DeviceToken.user_id == notification.user_id,
            DeviceToken.active.is_(True),
        )
    ).all()
    if not tokens:
        return notification
    errors: list[str] = []
    message_ids: list[str] = []
    for device in tokens:
        notification.attempt_count += 1
        try:
            message_ids.append(
                provider.send(
                    device_token=device.token,
                    title=notification.title,
                    body=notification.body,
                    data={
                        **{key: str(value) for key, value in notification.data.items()},
                        "deep_link": notification.deep_link,
                        "notification_id": str(notification.id),
                    },
                )
            )
        except PushProviderError as exc:
            errors.append(str(exc))
    if message_ids:
        notification.status = NotificationStatus.SENT
        notification.provider_message_id = message_ids[0]
        notification.sent_at = utcnow()
        notification.last_error = "; ".join(errors)[:1000] or None
    else:
        notification.status = NotificationStatus.FAILED
        notification.last_error = (
            "; ".join(errors)[:1000] or "Etkin cihaza bildirim gönderilemedi."
        )
    db.commit()
    db.refresh(notification)
    return notification

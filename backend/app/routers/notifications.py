import uuid

from fastapi import APIRouter, Depends, HTTPException, Query, Response, status
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database import get_db
from app.dependencies import get_current_user, get_push_provider
from app.firebase_mapping import lock_active_user_for_update
from app.models import DeviceToken, Notification, NotificationStatus, User, utcnow
from app.notifications import dispatch_notification
from app.push import PushProvider
from app.schemas import (
    DeviceTokenRegister,
    DeviceTokenResponse,
    NotificationListResponse,
    NotificationResponse,
)

router = APIRouter(prefix="/notifications", tags=["Bildirimler"])


@router.post(
    "/devices",
    response_model=DeviceTokenResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Push bildirim cihaz tokenını kaydet",
)
def register_device(
    payload: DeviceTokenRegister,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    provider: PushProvider = Depends(get_push_provider),
) -> DeviceToken:
    locked_user = lock_active_user_for_update(db, user.id)
    device = db.scalar(select(DeviceToken).where(DeviceToken.token == payload.token))
    if device is None:
        device = DeviceToken(
            user_id=locked_user.id,
            token=payload.token,
            platform=payload.platform,
        )
        db.add(device)
    else:
        device.user_id = locked_user.id
        device.platform = payload.platform
        device.active = True
        device.last_seen_at = utcnow()
    db.commit()
    db.refresh(device)
    pending = db.scalars(
        select(Notification).where(
            Notification.user_id == locked_user.id,
            Notification.status == NotificationStatus.PENDING,
        )
    ).all()
    for notification in pending:
        dispatch_notification(db, provider, notification)
    return device


@router.delete(
    "/devices/{device_id}",
    status_code=status.HTTP_204_NO_CONTENT,
    summary="Cihaz tokenını pasifleştir",
)
def deactivate_device(
    device_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Response:
    device = db.scalar(
        select(DeviceToken).where(
            DeviceToken.id == device_id,
            DeviceToken.user_id == user.id,
        )
    )
    if device is None:
        raise HTTPException(status_code=404, detail="Cihaz kaydı bulunamadı.")
    device.active = False
    db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)


@router.get(
    "",
    response_model=NotificationListResponse,
    summary="Kullanıcının bildirim kutusunu getir",
)
def list_notifications(
    unread_only: bool = False,
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> NotificationListResponse:
    filters = [Notification.user_id == user.id]
    if unread_only:
        filters.append(Notification.read_at.is_(None))
    items = db.scalars(
        select(Notification)
        .where(*filters)
        .order_by(Notification.created_at.desc())
        .limit(limit)
        .offset(offset)
    ).all()
    total = db.scalar(select(func.count(Notification.id)).where(*filters)) or 0
    unread = (
        db.scalar(
            select(func.count(Notification.id)).where(
                Notification.user_id == user.id,
                Notification.read_at.is_(None),
            )
        )
        or 0
    )
    return NotificationListResponse(
        items=[NotificationResponse.model_validate(item) for item in items],
        total=total,
        unread=unread,
        limit=limit,
        offset=offset,
    )


@router.post(
    "/{notification_id}/read",
    response_model=NotificationResponse,
    summary="Bildirimi okundu olarak işaretle",
)
def mark_notification_read(
    notification_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> Notification:
    notification = db.scalar(
        select(Notification).where(
            Notification.id == notification_id,
            Notification.user_id == user.id,
        )
    )
    if notification is None:
        raise HTTPException(status_code=404, detail="Bildirim bulunamadı.")
    if notification.read_at is None:
        notification.read_at = utcnow()
        db.commit()
        db.refresh(notification)
    return notification

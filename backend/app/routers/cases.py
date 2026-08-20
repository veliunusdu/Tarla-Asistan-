import uuid

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import case as sql_case, func, select
from sqlalchemy.orm import Session, selectinload

from app.database import get_db
from app.dependencies import get_current_user, get_push_provider, require_roles
from app.idempotency import record_operation, replayed_resource_id
from app.models import (
    CaseMedia,
    CaseMessage,
    CaseMessageMedia,
    CaseMessageType,
    CasePriority,
    CaseStatus,
    Farm,
    MediaAsset,
    NotificationType,
    SupportCase,
    User,
    UserRole,
    utcnow,
)
from app.notifications import safe_notify_user
from app.push import PushProvider
from app.routers.farms import get_owned_farm
from app.schemas import (
    CaseCreate,
    CaseDetailResponse,
    CaseListResponse,
    CaseMessageCreate,
    CaseMessageResponse,
    CaseStatusUpdate,
    CaseSummaryResponse,
    ExpertResponseCreate,
)

router = APIRouter(prefix="/cases", tags=["Sorun Bildirme ve Vakalar"])

EXPERT_TRANSITIONS: dict[CaseStatus, set[CaseStatus]] = {
    CaseStatus.OPEN: {
        CaseStatus.IN_REVIEW,
        CaseStatus.WAITING_FARMER,
        CaseStatus.ANSWERED,
        CaseStatus.CLOSED,
    },
    CaseStatus.IN_REVIEW: {
        CaseStatus.WAITING_FARMER,
        CaseStatus.ANSWERED,
        CaseStatus.CLOSED,
    },
    CaseStatus.WAITING_FARMER: {
        CaseStatus.IN_REVIEW,
        CaseStatus.ANSWERED,
        CaseStatus.CLOSED,
    },
    CaseStatus.ANSWERED: {CaseStatus.IN_REVIEW, CaseStatus.CLOSED},
    CaseStatus.CLOSED: {CaseStatus.IN_REVIEW},
}


@router.post(
    "",
    response_model=CaseDetailResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Fotoğraf, ses veya yazı ile vaka oluştur",
)
def create_case(
    payload: CaseCreate,
    user: User = Depends(require_roles(UserRole.FARMER)),
    db: Session = Depends(get_db),
) -> SupportCase:
    farm = get_owned_farm(db, user, payload.farm_id)
    replayed_id = replayed_resource_id(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope="case.create",
        payload=payload,
    )
    if replayed_id is not None:
        return _get_accessible_case(db, user, replayed_id)
    media = _owned_media(db, user, payload.media_ids)
    case = SupportCase(
        farm_id=farm.id,
        created_by_id=user.id,
        category=payload.category,
        title=payload.title,
        description=payload.description,
        priority=CasePriority.MEDIUM,
        status=CaseStatus.OPEN,
    )
    db.add(case)
    db.flush()
    case.media_links.extend(CaseMedia(media_id=item.id) for item in media)
    record_operation(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope="case.create",
        payload=payload,
        resource_type="case",
        resource_id=case.id,
    )
    db.commit()
    return _get_accessible_case(db, user, case.id)


@router.get(
    "",
    response_model=CaseListResponse,
    summary="Rol bazlı vaka listesini getir",
)
def list_cases(
    case_status: CaseStatus | None = Query(default=None, alias="status"),
    priority: CasePriority | None = None,
    farm_id: uuid.UUID | None = None,
    limit: int = Query(default=50, ge=1, le=100),
    offset: int = Query(default=0, ge=0),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> CaseListResponse:
    filters = []
    if user.role == UserRole.FARMER:
        filters.append(Farm.owner_id == user.id)
    if case_status is not None:
        filters.append(SupportCase.status == case_status)
    if priority is not None:
        filters.append(SupportCase.priority == priority)
    if farm_id is not None:
        filters.append(SupportCase.farm_id == farm_id)
    query = _case_query().join(Farm, Farm.id == SupportCase.farm_id).where(*filters)
    items = (
        db.scalars(
            query.order_by(
                sql_case(
                    (SupportCase.priority == CasePriority.CRITICAL, 0),
                    (SupportCase.priority == CasePriority.HIGH, 1),
                    (SupportCase.priority == CasePriority.MEDIUM, 2),
                    else_=3,
                ),
                SupportCase.updated_at.desc(),
            )
            .limit(limit)
            .offset(offset)
        )
        .unique()
        .all()
    )
    total = (
        db.scalar(
            select(func.count(SupportCase.id))
            .join(Farm, Farm.id == SupportCase.farm_id)
            .where(*filters)
        )
        or 0
    )
    return CaseListResponse(
        items=[CaseSummaryResponse.model_validate(item) for item in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.get(
    "/{case_id}",
    response_model=CaseDetailResponse,
    summary="Vaka, tarla bağlamı ve mesajlarını getir",
)
def get_case(
    case_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> SupportCase:
    return _get_accessible_case(db, user, case_id)


@router.patch(
    "/{case_id}/status",
    response_model=CaseDetailResponse,
    summary="Uzman vaka durumunu ve önceliğini güncellesin",
)
def update_case_status(
    case_id: uuid.UUID,
    payload: CaseStatusUpdate,
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
) -> SupportCase:
    case = _get_accessible_case(db, user, case_id)
    if (
        payload.status != case.status
        and payload.status not in EXPERT_TRANSITIONS[case.status]
    ):
        raise HTTPException(status_code=409, detail="Geçersiz vaka durum geçişi.")
    case.status = payload.status
    if payload.priority is not None:
        case.priority = payload.priority
    if payload.assign_to_me:
        case.assigned_expert_id = user.id
    case.closed_at = utcnow() if payload.status == CaseStatus.CLOSED else None
    db.commit()
    return _get_accessible_case(db, user, case.id)


@router.post(
    "/{case_id}/messages",
    response_model=CaseMessageResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Vakaya mesaj veya ek bilgi isteği ekle",
)
def create_case_message(
    case_id: uuid.UUID,
    payload: CaseMessageCreate,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    provider: PushProvider = Depends(get_push_provider),
) -> CaseMessage:
    case = _get_accessible_case(db, user, case_id)
    _validate_message_type(user, payload.message_type)
    if case.status == CaseStatus.CLOSED:
        raise HTTPException(status_code=409, detail="Kapalı vakaya mesaj eklenemez.")
    replayed_id = replayed_resource_id(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"case.message:{case.id}",
        payload=payload,
    )
    if replayed_id is not None:
        replayed = db.scalar(_message_query().where(CaseMessage.id == replayed_id))
        if replayed is not None and replayed.case_id == case.id:
            return replayed
    media = _owned_media(db, user, payload.media_ids)
    message = CaseMessage(
        case_id=case.id,
        sender_id=user.id,
        message_type=payload.message_type,
        body=payload.body,
    )
    db.add(message)
    db.flush()
    message.media_links.extend(CaseMessageMedia(media_id=item.id) for item in media)
    _apply_message_status(case, user, payload.message_type)
    record_operation(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"case.message:{case.id}",
        payload=payload,
        resource_type="case_message",
        resource_id=message.id,
    )
    db.commit()
    if payload.message_type == CaseMessageType.EXPERT_RESPONSE:
        safe_notify_user(
            db,
            provider,
            user_id=case.farm.owner_id,
            notification_type=NotificationType.EXPERT_RESPONSE,
            title="Uzmanınız vakanızı yanıtladı",
            body=payload.body[:300],
            deep_link=f"tarla-asistani://cases/{case.id}",
            data={"case_id": str(case.id), "message_id": str(message.id)},
            dedupe_key=f"expert-response:{message.id}",
        )
    return db.scalar(_message_query().where(CaseMessage.id == message.id))


@router.post(
    "/{case_id}/expert-response",
    response_model=CaseDetailResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Uzman cevabı ekle ve isteğe bağlı vakayı kapat",
)
def create_expert_response(
    case_id: uuid.UUID,
    payload: ExpertResponseCreate,
    user: User = Depends(require_roles(UserRole.AGRONOMIST)),
    db: Session = Depends(get_db),
    provider: PushProvider = Depends(get_push_provider),
) -> SupportCase:
    case = _get_accessible_case(db, user, case_id)
    replayed_id = replayed_resource_id(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"case.expert-response:{case.id}",
        payload=payload,
    )
    if replayed_id is not None:
        return _get_accessible_case(db, user, case.id)
    if case.status == CaseStatus.CLOSED:
        raise HTTPException(status_code=409, detail="Kapalı vakaya cevap eklenemez.")
    media = _owned_media(db, user, payload.media_ids)
    message = CaseMessage(
        case_id=case.id,
        sender_id=user.id,
        message_type=CaseMessageType.EXPERT_RESPONSE,
        body=payload.body,
    )
    db.add(message)
    db.flush()
    message.media_links.extend(CaseMessageMedia(media_id=item.id) for item in media)
    case.status = CaseStatus.CLOSED if payload.close_case else CaseStatus.ANSWERED
    case.closed_at = utcnow() if payload.close_case else None
    case.assigned_expert_id = user.id
    record_operation(
        db,
        actor_id=user.id,
        client_operation_id=payload.client_operation_id,
        scope=f"case.expert-response:{case.id}",
        payload=payload,
        resource_type="case_message",
        resource_id=message.id,
    )
    db.commit()
    safe_notify_user(
        db,
        provider,
        user_id=case.farm.owner_id,
        notification_type=NotificationType.EXPERT_RESPONSE,
        title="Uzmanınız vakanızı yanıtladı",
        body=payload.body[:300],
        deep_link=f"tarla-asistani://cases/{case.id}",
        data={"case_id": str(case.id), "message_id": str(message.id)},
        dedupe_key=f"expert-response:{message.id}",
    )
    return _get_accessible_case(db, user, case.id)


def _case_query():
    return (
        select(SupportCase)
        .execution_options(populate_existing=True)
        .options(
            selectinload(SupportCase.farm).selectinload(Farm.owner),
            selectinload(SupportCase.media_links).selectinload(CaseMedia.media),
            selectinload(SupportCase.messages).selectinload(CaseMessage.sender),
            selectinload(SupportCase.messages)
            .selectinload(CaseMessage.media_links)
            .selectinload(CaseMessageMedia.media),
        )
    )


def _message_query():
    return select(CaseMessage).options(
        selectinload(CaseMessage.sender),
        selectinload(CaseMessage.media_links).selectinload(CaseMessageMedia.media),
    )


def _get_accessible_case(
    db: Session,
    user: User,
    case_id: uuid.UUID,
) -> SupportCase:
    query = (
        _case_query()
        .join(Farm, Farm.id == SupportCase.farm_id)
        .where(SupportCase.id == case_id)
    )
    if user.role == UserRole.FARMER:
        query = query.where(Farm.owner_id == user.id)
    case = db.scalar(query)
    if case is None:
        raise HTTPException(status_code=404, detail="Vaka bulunamadı.")
    return case


def _owned_media(
    db: Session,
    user: User,
    media_ids: list[uuid.UUID],
) -> list[MediaAsset]:
    if not media_ids:
        return []
    media = db.scalars(
        select(MediaAsset).where(
            MediaAsset.id.in_(media_ids),
            MediaAsset.owner_id == user.id,
        )
    ).all()
    if len(media) != len(media_ids):
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Medya bulunamadı veya bu kullanıcıya ait değil.",
        )
    by_id = {item.id: item for item in media}
    return [by_id[item_id] for item_id in media_ids]


def _validate_message_type(user: User, message_type: CaseMessageType) -> None:
    if user.role == UserRole.FARMER and message_type != CaseMessageType.COMMENT:
        raise HTTPException(
            status_code=403, detail="Bu mesaj türü yalnızca uzmana açıktır."
        )


def _apply_message_status(
    case: SupportCase,
    user: User,
    message_type: CaseMessageType,
) -> None:
    if message_type == CaseMessageType.ADDITIONAL_INFO_REQUEST:
        case.status = CaseStatus.WAITING_FARMER
        case.assigned_expert_id = user.id
    elif message_type == CaseMessageType.EXPERT_RESPONSE:
        case.status = CaseStatus.ANSWERED
        case.assigned_expert_id = user.id
    elif user.role == UserRole.FARMER and case.status == CaseStatus.WAITING_FARMER:
        case.status = CaseStatus.IN_REVIEW

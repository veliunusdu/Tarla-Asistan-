import hashlib
import uuid
from pathlib import Path

from fastapi import APIRouter, Depends, File, HTTPException, UploadFile, status
from fastapi.responses import Response
from sqlalchemy import or_, select
from sqlalchemy.orm import Session

from app.config import Settings, get_settings
from app.database import get_db
from app.dependencies import get_current_user
from app.dependencies import get_media_storage
from app.firebase_mapping import lock_active_user_for_update
from app.media_storage import MediaStorage, MediaStorageError, MediaStorageMissing
from app.models import (
    CaseMedia,
    CaseMessage,
    CaseMessageMedia,
    Farm,
    MediaAsset,
    MediaKind,
    SupportCase,
    User,
    UserRole,
)
from app.schemas import MediaAssetResponse

router = APIRouter(prefix="/media", tags=["Medya"])

CONTENT_TYPES: dict[str, tuple[MediaKind, str]] = {
    "image/jpeg": (MediaKind.IMAGE, ".jpg"),
    "image/png": (MediaKind.IMAGE, ".png"),
    "image/webp": (MediaKind.IMAGE, ".webp"),
    "audio/mpeg": (MediaKind.AUDIO, ".mp3"),
    "audio/mp4": (MediaKind.AUDIO, ".m4a"),
    "audio/x-m4a": (MediaKind.AUDIO, ".m4a"),
    "audio/wav": (MediaKind.AUDIO, ".wav"),
    "audio/ogg": (MediaKind.AUDIO, ".ogg"),
}


@router.post(
    "",
    response_model=MediaAssetResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Fotoğraf veya ses dosyası yükle",
)
async def upload_media(
    file: UploadFile = File(...),
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    settings: Settings = Depends(get_settings),
    storage: MediaStorage = Depends(get_media_storage),
) -> MediaAsset:
    media_type = CONTENT_TYPES.get(file.content_type or "")
    if media_type is None:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail="Yalnızca JPG, PNG, WEBP ve desteklenen ses dosyaları yüklenebilir.",
        )

    storage_key = f"{uuid.uuid4().hex}{media_type[1]}"
    limit = settings.media_max_upload_mb * 1024 * 1024
    total = 0
    digest = hashlib.sha256()
    chunks: list[bytes] = []
    try:
        while chunk := await file.read(1024 * 1024):
            total += len(chunk)
            if total > limit:
                raise HTTPException(
                    status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                    detail=f"Dosya en fazla {settings.media_max_upload_mb} MB olabilir.",
                )
            digest.update(chunk)
            chunks.append(chunk)
    finally:
        await file.close()

    if total == 0:
        raise HTTPException(status_code=422, detail="Boş dosya yüklenemez.")

    try:
        storage.save(storage_key, b"".join(chunks), file.content_type or "application/octet-stream")
    except MediaStorageError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc

    try:
        locked_user = lock_active_user_for_update(db, user.id)
        asset = MediaAsset(
            owner_id=locked_user.id,
            kind=media_type[0],
            original_name=(Path(file.filename or "dosya").name[:255] or "dosya"),
            content_type=file.content_type or "application/octet-stream",
            size_bytes=total,
            storage_key=storage_key,
            checksum_sha256=digest.hexdigest(),
        )
        db.add(asset)
        db.commit()
    except Exception:
        db.rollback()
        try:
            storage.delete(storage_key)
        except Exception:
            pass
        raise
    db.refresh(asset)
    return asset


@router.get(
    "/{media_id}/content",
    summary="Yetkili medya içeriğini getir",
)
def get_media_content(
    media_id: uuid.UUID,
    user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    storage: MediaStorage = Depends(get_media_storage),
) -> Response:
    asset = db.get(MediaAsset, media_id)
    if asset is None or not _can_access_media(db, user, asset):
        raise HTTPException(status_code=404, detail="Medya bulunamadı.")
    try:
        content = storage.load(asset.storage_key)
    except MediaStorageMissing as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except MediaStorageError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    return Response(
        content=content,
        media_type=asset.content_type,
        headers={"Cache-Control": "private, max-age=3600"},
    )


def _can_access_media(db: Session, user: User, asset: MediaAsset) -> bool:
    if asset.owner_id == user.id:
        return True
    if user.role == UserRole.AGRONOMIST:
        attached = db.scalar(
            select(CaseMedia.media_id)
            .where(CaseMedia.media_id == asset.id)
            .union(
                select(CaseMessageMedia.media_id).where(
                    CaseMessageMedia.media_id == asset.id
                )
            )
            .limit(1)
        )
        return attached is not None
    attached_to_own_case = db.scalar(
        select(SupportCase.id)
        .join(Farm, Farm.id == SupportCase.farm_id)
        .outerjoin(CaseMedia, CaseMedia.case_id == SupportCase.id)
        .outerjoin(CaseMessage, CaseMessage.case_id == SupportCase.id)
        .outerjoin(CaseMessageMedia, CaseMessageMedia.message_id == CaseMessage.id)
        .where(
            Farm.owner_id == user.id,
            or_(CaseMedia.media_id == asset.id, CaseMessageMedia.media_id == asset.id),
        )
        .limit(1)
    )
    return attached_to_own_case is not None

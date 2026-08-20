import json

from fastapi import APIRouter, Depends, HTTPException, Request, status
from pydantic import ValidationError
from starlette.datastructures import UploadFile

from app.ai_chat import (
    AIChatPhotoUnsupported,
    AIChatProvider,
    AIChatProviderError,
    AIChatRequest,
    AIChatResponse,
)
from app.dependencies import get_ai_chat_provider, get_current_user
from app.models import User

router = APIRouter(prefix="/ai", tags=["Yapay Zeka"])

MAX_PHOTO_BYTES = 5 * 1024 * 1024
ALLOWED_PHOTO_TYPES = {"image/jpeg", "image/png"}

_CHAT_FIELDS = {
    "message": {"type": "string", "minLength": 1, "maxLength": 12000},
    "field_id": {"type": ["string", "null"], "maxLength": 200},
    "conversation_id": {"type": ["string", "null"], "maxLength": 200},
    "history": {
        "type": ["array", "null"],
        "items": {
            "type": "object",
            "required": ["role", "content"],
            "properties": {
                "role": {"type": "string", "enum": ["user", "assistant"]},
                "content": {"type": "string", "minLength": 1, "maxLength": 12000},
            },
        },
    },
}


def _request_schema(*, multipart: bool) -> dict:
    properties = dict(_CHAT_FIELDS)
    if multipart:
        properties["photo"] = {
            "type": "string",
            "format": "binary",
            "description": "JPEG veya PNG, en fazla 5 MiB.",
        }
    return {
        "type": "object",
        "required": ["message"],
        "properties": properties,
    }


@router.post(
    "/chat",
    response_model=AIChatResponse,
    summary="Tarla bağlamında AI sohbeti başlat veya sürdür",
    openapi_extra={
        "requestBody": {
            "required": True,
            "content": {
                "application/json": {
                    "schema": _request_schema(multipart=False)
                },
                "multipart/form-data": {
                    "schema": _request_schema(multipart=True)
                },
            },
        }
    },
)
async def chat(
    request: Request,
    user: User = Depends(get_current_user),
    provider: AIChatProvider = Depends(get_ai_chat_provider),
) -> AIChatResponse:
    content_type = request.headers.get("content-type", "").split(";", 1)[0].lower()
    if content_type == "application/json":
        payload = await request.json()
        chat_request = _validate_payload(payload)
    elif content_type in {"multipart/form-data", "application/x-www-form-urlencoded"}:
        form = await request.form()
        chat_request = _validate_multipart(form)
        photo = form.get("photo")
        if photo is not None:
            if not isinstance(photo, UploadFile):
                raise HTTPException(status_code=422, detail="photo alanı dosya olmalıdır.")
            chat_request = chat_request.model_copy(
                update={
                    "photo_bytes": await _read_photo(photo),
                    "photo_content_type": photo.content_type,
                }
            )
    else:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail="Content-Type application/json veya multipart/form-data olmalıdır.",
        )
    try:
        return provider.generate(chat_request)
    except AIChatPhotoUnsupported as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except AIChatProviderError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc


def _validate_payload(payload: object) -> AIChatRequest:
    if not isinstance(payload, dict):
        raise HTTPException(status_code=422, detail="İstek gövdesi JSON nesnesi olmalıdır.")
    try:
        return AIChatRequest.model_validate(payload)
    except ValidationError as exc:
        raise HTTPException(status_code=422, detail=_validation_detail(exc)) from exc


def _validate_multipart(form) -> AIChatRequest:
    history = form.get("history")
    if history is not None:
        try:
            history = json.loads(str(history))
        except json.JSONDecodeError as exc:
            raise HTTPException(status_code=422, detail="history geçerli JSON olmalıdır.") from exc
    payload = {
        "message": form.get("message"),
        "field_id": form.get("field_id"),
        "conversation_id": form.get("conversation_id"),
        "history": history,
    }
    return _validate_payload(payload)


async def _read_photo(photo: UploadFile) -> bytes:
    if photo.content_type not in ALLOWED_PHOTO_TYPES:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail="photo yalnızca JPEG veya PNG olabilir.",
        )
    chunks: list[bytes] = []
    total = 0
    while chunk := await photo.read(1024 * 1024):
        total += len(chunk)
        if total > MAX_PHOTO_BYTES:
            raise HTTPException(
                status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                detail="photo en fazla 5 MB olabilir.",
            )
        chunks.append(chunk)
    if total == 0:
        raise HTTPException(status_code=422, detail="photo boş olamaz.")
    return b"".join(chunks)


def _validation_detail(exc: ValidationError) -> str:
    error = exc.errors()[0]
    location = ".".join(str(part) for part in error.get("loc", ())) or "body"
    return f"{location}: {error.get('msg', 'Geçersiz değer.')}"

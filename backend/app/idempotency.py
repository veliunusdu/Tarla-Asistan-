import hashlib
import json
import uuid

from fastapi import HTTPException, status
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models import ClientOperation


def payload_digest(payload: BaseModel) -> str:
    value = payload.model_dump(
        mode="json",
        exclude={"client_operation_id"},
        exclude_none=False,
    )
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def replayed_resource_id(
    db: Session,
    *,
    actor_id: uuid.UUID,
    client_operation_id: uuid.UUID | None,
    scope: str,
    payload: BaseModel,
) -> uuid.UUID | None:
    if client_operation_id is None:
        return None
    operation = db.scalar(
        select(ClientOperation).where(
            ClientOperation.actor_id == actor_id,
            ClientOperation.client_operation_id == client_operation_id,
        )
    )
    if operation is None:
        return None
    if operation.scope != scope or operation.payload_hash != payload_digest(payload):
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Bu client_operation_id farklı bir işlem için kullanılmış.",
        )
    return operation.resource_id


def record_operation(
    db: Session,
    *,
    actor_id: uuid.UUID,
    client_operation_id: uuid.UUID | None,
    scope: str,
    payload: BaseModel,
    resource_type: str,
    resource_id: uuid.UUID,
) -> None:
    if client_operation_id is None:
        return
    db.add(
        ClientOperation(
            actor_id=actor_id,
            client_operation_id=client_operation_id,
            scope=scope,
            payload_hash=payload_digest(payload),
            resource_type=resource_type,
            resource_id=resource_id,
        )
    )

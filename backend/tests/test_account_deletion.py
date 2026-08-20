import os
import asyncio
from contextlib import nullcontext
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from uuid import UUID, uuid4

import pytest
from fastapi import HTTPException
from fastapi.security import HTTPAuthorizationCredentials
from firebase_admin import auth, exceptions as firebase_exceptions
from google.api_core import exceptions as google_exceptions
from pydantic import ValidationError
from sqlalchemy import create_engine, event
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

from app.config import Settings
from app.database import Base
from app.firebase_account import (
    AccountDeletionProviderError,
    FirebaseAdminAccountGateway,
)
from app.firebase_auth import FirebaseIdentity
from app.models import (
    AccountDeletionJob,
    AccountDeletionStatus,
    AccountStatus,
    DevicePlatform,
    DeviceToken,
    FirebaseLinkApproval,
    MediaAsset,
    MediaKind,
    Profile,
    User,
    UserRole,
    utcnow,
)
from app.media_storage import MediaStorageError, MediaStorageMissing
from app.security import create_refresh_token
from app.schemas import DeviceTokenRegister, RefreshTokenRequest


def login_owner(client, phone_number="+905551234567"):
    requested = client.post(
        "/api/v1/auth/request-otp", json={"phone_number": phone_number}
    )
    verified = client.post(
        "/api/v1/auth/verify-otp",
        json={
            "phone_number": phone_number,
            "otp_code": requested.json()["debug_otp"],
        },
    )
    assert verified.status_code == 200
    return verified.json()


PROCESS_NOW = datetime(2026, 8, 20, 12, 0, tzinfo=timezone.utc)


@pytest.fixture
def deletion_session_factory():
    engine = create_engine(
        "sqlite+pysqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    sessions = sessionmaker(
        bind=engine,
        autoflush=False,
        expire_on_commit=False,
    )
    try:
        yield sessions
    finally:
        engine.dispose()


class FakeAccountGateway:
    def __init__(self, *, fail_on=None, committed_steps=None):
        self.fail_on = fail_on
        self.verify_commits = committed_steps is not None
        self.committed_steps = committed_steps if committed_steps is not None else []
        self.calls = []

    def _call(self, name, error_code):
        self.calls.append(name)
        if self.verify_commits and name == "firestore":
            assert "firebase_tokens" in self.committed_steps
        elif self.verify_commits and name == "firebase_auth":
            assert "media" in self.committed_steps
        if self.fail_on == name:
            raise AccountDeletionProviderError(error_code)

    def revoke_tokens(self, uid):
        self._call("firebase_tokens", "FIREBASE_AUTH_UNAVAILABLE")

    def anonymize_firestore(self, uid, anonymous_subject):
        self._call("firestore", "FIRESTORE_UNAVAILABLE")

    def delete_auth_user(self, uid):
        self._call("firebase_auth", "FIREBASE_AUTH_UNAVAILABLE")


class FakeDeletionStorage:
    def __init__(self, *, fail=False, missing=False, committed_steps=None):
        self.fail = fail
        self.missing = missing
        self.verify_commits = committed_steps is not None
        self.committed_steps = committed_steps if committed_steps is not None else []
        self.deleted = []

    def delete(self, key):
        if self.verify_commits:
            assert "firestore" in self.committed_steps
        self.deleted.append(key)
        if self.fail:
            raise MediaStorageError(
                "storage provider detail and user identifier must not persist"
            )
        if self.missing:
            raise MediaStorageMissing("missing")


class FakeUploadFile:
    content_type = "image/jpeg"
    filename = "race.jpg"

    def __init__(self, content=b"race-media"):
        self._chunks = [content]

    async def read(self, _size):
        return self._chunks.pop(0) if self._chunks else b""

    async def close(self):
        pass


class FakeRaceStorage:
    def __init__(self, *, on_save=None):
        self.on_save = on_save
        self.saved = {}
        self.deleted = []

    def save(self, key, content, content_type):
        self.saved[key] = (content, content_type)
        if self.on_save is not None:
            self.on_save()

    def delete(self, key):
        self.deleted.append(key)
        self.saved.pop(key, None)


class FakeAdminApp:
    def __init__(self, project_id="demo2-c4265"):
        self.project_id = project_id


class FakeDocumentSnapshot:
    def __init__(self, document_id: str):
        self.id = document_id
        self.reference = FakeDocumentReference("farms", document_id)


class FakeDocumentReference:
    def __init__(self, collection: str, document_id: str, firestore=None):
        self.collection = collection
        self.id = document_id
        self.firestore = firestore

    def delete(self):
        self.firestore.deleted_documents.append(f"{self.collection}/{self.id}")


class FakeQuery:
    def __init__(self, firestore, documents):
        self.firestore = firestore
        self.documents = documents

    def where(self, *, filter):
        self.firestore.query = (
            filter.field_path,
            filter.op_string,
            filter.value,
        )
        return self

    def stream(self):
        if self.firestore.stream_error is not None:
            raise self.firestore.stream_error
        return iter(self.documents)


class FakeCollection:
    def __init__(self, firestore, name: str):
        self.firestore = firestore
        self.name = name

    def where(self, *, filter):
        documents = [
            FakeDocumentSnapshot(farm["id"])
            for farm in self.firestore.farms
            if farm["ownerId"] == filter.value
        ]
        return FakeQuery(self.firestore, documents).where(filter=filter)

    def document(self, document_id: str):
        return FakeDocumentReference(self.name, document_id, self.firestore)


class FakeBatch:
    def __init__(self, firestore):
        self.firestore = firestore
        self.operations = []

    def update(self, document, values):
        self.operations.append((document, values))

    def commit(self):
        self.firestore.batch_sizes.append(len(self.operations))
        for document, values in self.operations:
            self.firestore.updated[document.id] = values


class FakeFirestoreClient:
    def __init__(self, firestore):
        self.firestore = firestore

    def collection(self, name: str):
        return FakeCollection(self.firestore, name)

    def batch(self):
        return FakeBatch(self.firestore)


class FakeFirestore:
    def __init__(self, farms=None, stream_error=None):
        self.farms = farms or []
        self.stream_error = stream_error
        self.database_id = None
        self.query = None
        self.updated = {}
        self.deleted_documents = []
        self.batch_sizes = []
        self.client_calls = 0

    def client(self, *, app, database_id):
        assert app.project_id == "demo2-c4265"
        self.client_calls += 1
        self.database_id = database_id
        return FakeFirestoreClient(self)


class FakeAuth:
    UserNotFoundError = auth.UserNotFoundError

    def __init__(self, *, missing=False, error=None):
        self.missing = missing
        self.error = error
        self.revoked = []
        self.deleted = []

    def revoke_refresh_tokens(self, uid, *, app):
        self.revoked.append(uid)
        if self.missing:
            raise self.UserNotFoundError("missing user")
        if self.error is not None:
            raise self.error

    def delete_user(self, uid, *, app):
        self.deleted.append(uid)
        if self.missing:
            raise self.UserNotFoundError("missing user")
        if self.error is not None:
            raise self.error


def test_gateway_uses_named_database_and_anonymizes_owned_farms():
    fake = FakeFirestore(farms=[{"id": "farm-1", "ownerId": "uid-1"}])
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(), auth_module=FakeAuth(), firestore_factory=fake.client
    )

    gateway.anonymize_firestore("uid-1", "anon-123")

    assert fake.database_id == "tarla-asistani"
    assert fake.query == ("ownerId", "==", "uid-1")
    assert fake.updated["farm-1"] == {
        "ownerId": "anon-123",
        "anonymousOwnerId": "anon-123",
    }
    assert fake.deleted_documents == ["users/uid-1"]


def test_gateway_anonymizes_more_than_500_farms_in_bounded_batches():
    fake = FakeFirestore(
        farms=[{"id": f"farm-{index}", "ownerId": "uid-1"} for index in range(501)]
    )
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(), auth_module=FakeAuth(), firestore_factory=fake.client
    )

    gateway.anonymize_firestore("uid-1", "anon-123")

    assert fake.batch_sizes == [450, 51]
    assert len(fake.updated) == 501
    assert max(fake.batch_sizes) <= 450


def test_gateway_treats_missing_auth_user_as_idempotent_success():
    fake_auth = FakeAuth(missing=True)
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(),
        auth_module=fake_auth,
        firestore_factory=FakeFirestore().client,
    )

    gateway.revoke_tokens("uid-1")
    gateway.delete_auth_user("uid-1")

    assert fake_auth.revoked == ["uid-1"]
    assert fake_auth.deleted == ["uid-1"]


def test_gateway_maps_firestore_failure_without_provider_detail():
    provider_text = "upstream timeout containing sensitive provider context"
    fake = FakeFirestore(
        stream_error=google_exceptions.ServiceUnavailable(provider_text)
    )
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(), auth_module=FakeAuth(), firestore_factory=fake.client
    )

    with pytest.raises(AccountDeletionProviderError) as exc_info:
        gateway.anonymize_firestore("uid-1", "anon-123")

    assert exc_info.value.code == "FIRESTORE_UNAVAILABLE"
    assert str(exc_info.value) == "FIRESTORE_UNAVAILABLE"
    assert provider_text not in str(exc_info.value)
    assert exc_info.value.__context__ is None
    assert exc_info.value.__cause__ is None


@pytest.mark.parametrize("operation", ["revoke_tokens", "delete_auth_user"])
def test_gateway_maps_auth_failure_without_provider_detail(operation):
    provider_text = "auth backend leaked text"
    fake_auth = FakeAuth(
        error=firebase_exceptions.FirebaseError("UNAVAILABLE", provider_text)
    )
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(),
        auth_module=fake_auth,
        firestore_factory=FakeFirestore().client,
    )

    with pytest.raises(AccountDeletionProviderError) as exc_info:
        getattr(gateway, operation)("uid-1")

    assert exc_info.value.code == "FIREBASE_AUTH_UNAVAILABLE"
    assert str(exc_info.value) == "FIREBASE_AUTH_UNAVAILABLE"
    assert provider_text not in str(exc_info.value)
    assert exc_info.value.__context__ is None
    assert exc_info.value.__cause__ is None


@pytest.mark.parametrize("operation", ["revoke_tokens", "delete_auth_user"])
@pytest.mark.parametrize("raw_uid", ["", "uid/invalid", "u" * 129])
def test_gateway_maps_auth_value_error_without_uid_in_exception_chain(
    operation, raw_uid
):
    fake_auth = FakeAuth(error=ValueError(f"malformed Firebase UID: {raw_uid}"))
    gateway = FirebaseAdminAccountGateway(
        app=FakeAdminApp(),
        auth_module=fake_auth,
        firestore_factory=FakeFirestore().client,
    )

    with pytest.raises(AccountDeletionProviderError) as exc_info:
        getattr(gateway, operation)(raw_uid)

    assert exc_info.value.code == "FIREBASE_AUTH_UNAVAILABLE"
    if raw_uid:
        assert raw_uid not in str(exc_info.value)
    assert exc_info.value.__context__ is None
    assert exc_info.value.__cause__ is None


def test_gateway_sanitizes_named_app_initialization_failure(monkeypatch):
    credential_text = "credential path and provider detail must not escape"
    monkeypatch.setattr(
        "app.firebase_account.get_firebase_auth_app",
        lambda: (_ for _ in ()).throw(RuntimeError(credential_text)),
    )

    with pytest.raises(AccountDeletionProviderError) as exc_info:
        FirebaseAdminAccountGateway(
            auth_module=FakeAuth(), firestore_factory=FakeFirestore().client
        )

    assert exc_info.value.code == "FIREBASE_AUTH_UNAVAILABLE"
    assert credential_text not in str(exc_info.value)
    assert exc_info.value.__context__ is None
    assert exc_info.value.__cause__ is None


@pytest.mark.parametrize("project_id", [None, "", "another-project"])
def test_gateway_rejects_missing_or_wrong_admin_project_before_firestore(project_id):
    fake = FakeFirestore()

    with pytest.raises(AccountDeletionProviderError) as exc_info:
        FirebaseAdminAccountGateway(
            app=FakeAdminApp(project_id),
            auth_module=FakeAuth(),
            firestore_factory=fake.client,
        )

    assert exc_info.value.code == "FIREBASE_CONFIGURATION_INVALID"
    assert str(exc_info.value) == "FIREBASE_CONFIGURATION_INVALID"
    assert exc_info.value.__context__ is None
    assert exc_info.value.__cause__ is None
    assert fake.client_calls == 0


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("firebase_project_id", None),
        ("firebase_project_id", ""),
        ("firebase_project_id", "another-project"),
        ("firestore_database_id", ""),
        ("firestore_database_id", "another-database"),
    ],
)
def test_account_deletion_settings_reject_alternate_firebase_targets(field, value):
    with pytest.raises(ValidationError) as exc_info:
        Settings(**{field: value})

    error_text = str(exc_info.value)
    if value:
        assert value not in error_text
    elif value is None:
        assert "input_value=None" not in error_text
    else:
        assert "input_value=''" not in error_text


def test_account_deletion_settings_default_to_exact_firebase_targets():
    settings = Settings()

    assert settings.firebase_project_id == "demo2-c4265"
    assert settings.firestore_database_id == "tarla-asistani"


def test_secure_account_models_have_safe_defaults(db_session):
    user = User(phone_number="+905551234580", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.commit()
    approval = FirebaseLinkApproval(
        user_id=user.id,
        firebase_uid="approved-uid",
        approved_by="staging-operator",
        approved_at=utcnow(),
        expires_at=utcnow() + timedelta(hours=24),
    )
    job = AccountDeletionJob(user_id=user.id, firebase_uid_snapshot="approved-uid")
    db_session.add_all([approval, job])
    db_session.commit()

    assert user.account_status is AccountStatus.ACTIVE
    assert approval.consumed_at is None
    assert job.status is AccountDeletionStatus.PENDING
    assert job.attempt_count == 0


def test_only_one_unconsumed_link_approval_is_allowed_per_user(db_session):
    user = User(phone_number="+905551234581", role=UserRole.FARMER)
    db_session.add(user)
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=user.id,
            firebase_uid="first-approved-uid",
            approved_by="staging-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=user.id,
            firebase_uid="second-approved-uid",
            approved_by="staging-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    with pytest.raises(IntegrityError):
        db_session.commit()


def test_consumed_link_approval_preserves_history_without_blocking_same_identity(
    db_session,
):
    users = [
        User(phone_number=f"test-phone-approval-{index}", role=UserRole.AGRONOMIST)
        for index in range(3)
    ]
    db_session.add_all(users)
    db_session.flush()
    db_session.add(
        FirebaseLinkApproval(
            user_id=users[0].id,
            firebase_uid="expert-identity-shared",
            approved_by="first-operator",
            approved_at=utcnow() - timedelta(days=2),
            expires_at=utcnow() - timedelta(days=1),
            consumed_at=utcnow(),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=users[1].id,
            firebase_uid="expert-identity-shared",
            approved_by="second-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    db_session.commit()

    db_session.add(
        FirebaseLinkApproval(
            user_id=users[2].id,
            firebase_uid="expert-identity-shared",
            approved_by="third-operator",
            approved_at=utcnow(),
            expires_at=utcnow() + timedelta(hours=24),
        )
    )
    with pytest.raises(IntegrityError):
        db_session.commit()


def test_postgresql_migration_uses_partial_unconsumed_approval_indexes():
    backend_dir = Path(__file__).resolve().parents[1]
    environment = os.environ.copy()
    environment["DATABASE_URL"] = (
        "postgresql+psycopg://unused:unused@localhost/unused"
    )
    completed = subprocess.run(
        [
            sys.executable,
            "-m",
            "alembic",
            "upgrade",
            "20260820_0007:20260820_0008",
            "--sql",
        ],
        cwd=backend_dir,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    sql = " ".join(completed.stdout.split())
    approval_table_start = sql.index("CREATE TABLE firebase_link_approvals")
    approval_table_end = sql.index(
        "CREATE INDEX ix_firebase_link_approvals_user_id",
        approval_table_start,
    )
    approval_table_sql = sql[approval_table_start:approval_table_end]
    assert "UNIQUE (firebase_uid)" not in approval_table_sql
    assert (
        "CREATE UNIQUE INDEX "
        "uq_firebase_link_approvals_one_unconsumed_per_user "
        "ON firebase_link_approvals (user_id) WHERE consumed_at IS NULL"
    ) in sql
    assert (
        "CREATE UNIQUE INDEX "
        "uq_firebase_link_approvals_one_unconsumed_per_uid "
        "ON firebase_link_approvals (firebase_uid) WHERE consumed_at IS NULL"
    ) in sql
    assert "processing_started_at TIMESTAMP WITH TIME ZONE" in sql
    assert "lease_until TIMESTAMP WITH TIME ZONE" in sql
    assert "processing_owner_token VARCHAR(64)" in sql
    assert (
        "CREATE INDEX ix_account_deletion_jobs_retry_schedule "
        "ON account_deletion_jobs (status, next_retry_at, lease_until, created_at)"
    ) in sql


def test_deletion_request_locks_account_and_is_idempotent(
    client, db_session, monkeypatch
):
    auth_payload = login_owner(client)
    headers = {"Authorization": f"Bearer {auth_payload['access_token']}"}
    monkeypatch.setattr(
        "app.routers.users.process_account_deletion_safely",
        lambda _job_id: None,
        raising=False,
    )

    first = client.post(
        "/api/v1/users/me/deletion-request",
        headers=headers,
        json={"confirmation": "HESABIMI SIL"},
    )
    second = client.post(
        "/api/v1/users/me/deletion-request",
        headers=headers,
        json={"confirmation": "HESABIMI SIL"},
    )

    assert first.status_code == second.status_code == 202
    assert first.json() == second.json()
    job = db_session.get(AccountDeletionJob, UUID(first.json()["request_id"]))
    assert job is not None
    assert job.user.account_status is AccountStatus.DELETION_PENDING
    assert all(token.revoked_at is not None for token in job.user.refresh_tokens)


@pytest.mark.parametrize(
    "confirmation",
    ["", "HESABIMI SİL", " HESABIMI SIL", "HESABIMI SIL ", "hesabimi sil"],
)
def test_deletion_request_requires_exact_confirmation(
    client, monkeypatch, confirmation
):
    auth_payload = login_owner(client)
    monkeypatch.setattr(
        "app.routers.users.process_account_deletion_safely",
        lambda _job_id: None,
        raising=False,
    )

    response = client.post(
        "/api/v1/users/me/deletion-request",
        headers={"Authorization": f"Bearer {auth_payload['access_token']}"},
        json={"confirmation": confirmation},
    )

    assert response.status_code == 422


def test_deletion_request_requires_authentication(client):
    response = client.post(
        "/api/v1/users/me/deletion-request",
        json={"confirmation": "HESABIMI SIL"},
    )

    assert response.status_code == 401


def test_pending_account_is_forbidden_on_normal_endpoint_but_can_repeat_deletion(
    client, monkeypatch
):
    auth_payload = login_owner(client)
    headers = {"Authorization": f"Bearer {auth_payload['access_token']}"}
    monkeypatch.setattr(
        "app.routers.users.process_account_deletion_safely",
        lambda _job_id: None,
        raising=False,
    )
    requested = client.post(
        "/api/v1/users/me/deletion-request",
        headers=headers,
        json={"confirmation": "HESABIMI SIL"},
    )

    normal_access = client.get("/api/v1/auth/me", headers=headers)
    repeated = client.post(
        "/api/v1/users/me/deletion-request",
        headers=headers,
        json={"confirmation": "HESABIMI SIL"},
    )

    assert requested.status_code == 202
    assert normal_access.status_code == 403
    assert repeated.status_code == 202
    assert repeated.json() == requested.json()


def test_deletion_lock_revokes_legacy_refresh_session(client, monkeypatch):
    auth_payload = login_owner(client)
    monkeypatch.setattr(
        "app.routers.users.process_account_deletion_safely",
        lambda _job_id: None,
        raising=False,
    )
    requested = client.post(
        "/api/v1/users/me/deletion-request",
        headers={"Authorization": f"Bearer {auth_payload['access_token']}"},
        json={"confirmation": "HESABIMI SIL"},
    )

    refreshed = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": auth_payload["refresh_token"]},
    )

    assert requested.status_code == 202
    assert refreshed.status_code == 401


def test_deletion_route_runs_background_processing_after_commit(
    client, db_session, monkeypatch
):
    auth_payload = login_owner(client)
    processed = []

    def assert_committed_then_process(job_id):
        job = db_session.get(AccountDeletionJob, job_id)
        assert job is not None
        assert job.status is AccountDeletionStatus.PENDING
        assert job.user.account_status is AccountStatus.DELETION_PENDING
        assert all(token.revoked_at is not None for token in job.user.refresh_tokens)
        processed.append(job_id)

    monkeypatch.setattr(
        "app.routers.users.process_account_deletion_safely",
        assert_committed_then_process,
    )

    response = client.post(
        "/api/v1/users/me/deletion-request",
        headers={"Authorization": f"Bearer {auth_payload['access_token']}"},
        json={"confirmation": "HESABIMI SIL"},
    )

    assert response.status_code == 202
    assert processed == [UUID(response.json()["request_id"])]


def test_deletion_route_schedules_the_safe_processor(db_session):
    from fastapi import BackgroundTasks

    from app.account_deletion import process_account_deletion_safely
    from app.routers.users import request_deletion
    from app.schemas import AccountDeletionRequest

    user = User(phone_number="+905551234569", role=UserRole.FARMER)
    db_session.add(user)
    db_session.commit()
    tasks = BackgroundTasks()

    response = request_deletion(
        AccountDeletionRequest(confirmation="HESABIMI SIL"),
        tasks,
        user,
        db_session,
    )

    assert response.request_id is not None
    assert len(tasks.tasks) == 1
    assert tasks.tasks[0].func is process_account_deletion_safely


def add_deletion_subject(db_session):
    user = User(
        phone_number="+905551234570",
        firebase_uid="firebase-user-secret",
        role=UserRole.FARMER,
    )
    user.profile = Profile(
        full_name="Deletion Subject",
        province="Konya",
        district="Selçuklu",
        terms_accepted=True,
        notifications_enabled=True,
    )
    db_session.add(user)
    db_session.flush()
    raw_token, refresh_token = create_refresh_token(user.id, Settings())
    db_session.add_all(
        [
            refresh_token,
            DeviceToken(
                user_id=user.id,
                token="device-token-secret",
                platform=DevicePlatform.ANDROID,
            ),
            MediaAsset(
                owner_id=user.id,
                kind=MediaKind.IMAGE,
                original_name="sensitive-name.jpg",
                content_type="image/jpeg",
                size_bytes=10,
                storage_key="owned-media-key",
                checksum_sha256="a" * 64,
            ),
        ]
    )
    db_session.commit()
    return user, raw_token


def test_request_account_deletion_persists_one_lock_and_job(db_session):
    from app.account_deletion import request_account_deletion

    user, _raw_token = add_deletion_subject(db_session)

    first = request_account_deletion(db_session, user, PROCESS_NOW)
    first_subject = user.anonymized_subject_id
    second = request_account_deletion(
        db_session, user, PROCESS_NOW + timedelta(minutes=1)
    )

    assert first.id == second.id
    assert first.firebase_uid_snapshot == "firebase-user-secret"
    assert first.status is AccountDeletionStatus.PENDING
    assert user.account_status is AccountStatus.DELETION_PENDING
    assert first_subject is not None
    assert user.anonymized_subject_id == first_subject
    assert all(
        token.revoked_at.replace(tzinfo=token.revoked_at.tzinfo or timezone.utc)
        == PROCESS_NOW
        for token in user.refresh_tokens
    )


def _track_committed_steps(db_session, job):
    committed_steps = []

    def capture_completed_steps(_session):
        if job.firebase_tokens_revoked_at is not None:
            committed_steps.append("firebase_tokens")
        if job.firestore_anonymized_at is not None:
            committed_steps.append("firestore")
        if job.media_deleted_at is not None:
            committed_steps.append("media")
        if job.firebase_auth_deleted_at is not None:
            committed_steps.append("firebase_auth")

    event.listen(db_session, "after_commit", capture_completed_steps)
    return committed_steps, capture_completed_steps


def test_account_deletion_processes_ordered_steps_and_anonymizes_postgres(db_session):
    from app.account_deletion import (
        process_account_deletion,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    committed_steps, listener = _track_committed_steps(db_session, job)
    gateway = FakeAccountGateway(committed_steps=committed_steps)
    storage = FakeDeletionStorage(committed_steps=committed_steps)
    try:
        result = process_account_deletion(
            db_session,
            job.id,
            gateway,
            storage,
            Settings(),
            PROCESS_NOW,
        )
    finally:
        event.remove(db_session, "after_commit", listener)

    assert gateway.calls == ["firebase_tokens", "firestore", "firebase_auth"]
    assert storage.deleted == ["owned-media-key"]
    assert result.status is AccountDeletionStatus.COMPLETED
    assert result.attempt_count == 1
    assert result.completed_at == PROCESS_NOW
    assert user.phone_number.startswith("deleted-")
    assert user.firebase_uid is None
    assert user.profile.full_name is None
    assert user.profile.province is None
    assert user.profile.district is None
    assert user.profile.notifications_enabled is False
    device_tokens = db_session.query(DeviceToken).filter_by(user_id=user.id).all()
    assert all(token.active is False for token in device_tokens)
    assert user.account_status is AccountStatus.ANONYMIZED
    assert result.postgres_anonymized_at == PROCESS_NOW


@pytest.mark.parametrize(
    ("failure_step", "error_code", "completed_before_failure"),
    [
        ("firebase_tokens", "FIREBASE_AUTH_UNAVAILABLE", []),
        ("firestore", "FIRESTORE_UNAVAILABLE", ["firebase_tokens"]),
        (
            "media",
            "MEDIA_STORAGE_UNAVAILABLE",
            ["firebase_tokens", "firestore"],
        ),
        (
            "firebase_auth",
            "FIREBASE_AUTH_UNAVAILABLE",
            ["firebase_tokens", "firestore", "media"],
        ),
    ],
)
def test_account_deletion_failure_is_retryable_and_skips_committed_steps(
    db_session, failure_step, error_code, completed_before_failure
):
    from app.account_deletion import (
        process_account_deletion,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    committed_steps, listener = _track_committed_steps(db_session, job)
    failing_gateway = FakeAccountGateway(
        fail_on=failure_step, committed_steps=committed_steps
    )
    failing_storage = FakeDeletionStorage(
        fail=failure_step == "media", committed_steps=committed_steps
    )
    try:
        failed = process_account_deletion(
            db_session,
            job.id,
            failing_gateway,
            failing_storage,
            Settings(),
            PROCESS_NOW,
        )

        assert failed.status is AccountDeletionStatus.RETRY_REQUIRED
        assert failed.attempt_count == 1
        assert failed.last_error_code == error_code
        assert failed.next_retry_at == PROCESS_NOW + timedelta(minutes=15)
        assert user.account_status is AccountStatus.DELETION_PENDING
        assert "secret" not in failed.last_error_code.lower()
        assert [
            step
            for step, timestamp in [
                ("firebase_tokens", failed.firebase_tokens_revoked_at),
                ("firestore", failed.firestore_anonymized_at),
                ("media", failed.media_deleted_at),
            ]
            if timestamp is not None
        ] == completed_before_failure

        retry_gateway = FakeAccountGateway(committed_steps=committed_steps)
        retry_storage = FakeDeletionStorage(committed_steps=committed_steps)
        retried = process_account_deletion(
            db_session,
            job.id,
            retry_gateway,
            retry_storage,
            Settings(),
            PROCESS_NOW + timedelta(minutes=15),
        )
    finally:
        event.remove(db_session, "after_commit", listener)

    expected_gateway_calls = {
        "firebase_tokens": ["firebase_tokens", "firestore", "firebase_auth"],
        "firestore": ["firestore", "firebase_auth"],
        "media": ["firebase_auth"],
        "firebase_auth": ["firebase_auth"],
    }
    assert retry_gateway.calls == expected_gateway_calls[failure_step]
    assert retry_storage.deleted == (
        [] if failure_step == "firebase_auth" else ["owned-media-key"]
    )
    assert retried.status is AccountDeletionStatus.COMPLETED
    assert retried.attempt_count == 2


def test_missing_media_is_idempotent_success_and_metadata_remains(db_session):
    from app.account_deletion import (
        process_account_deletion,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    gateway = FakeAccountGateway()
    storage = FakeDeletionStorage(missing=True)

    result = process_account_deletion(
        db_session, job.id, gateway, storage, Settings(), PROCESS_NOW
    )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert db_session.query(MediaAsset).filter_by(owner_id=user.id).count() == 1


def test_completed_deletion_job_is_a_noop_on_rerun(db_session):
    from app.account_deletion import (
        process_account_deletion,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    completed = process_account_deletion(
        db_session,
        job.id,
        FakeAccountGateway(),
        FakeDeletionStorage(),
        Settings(),
        PROCESS_NOW,
    )
    gateway = FakeAccountGateway()
    storage = FakeDeletionStorage()

    rerun = process_account_deletion(
        db_session,
        job.id,
        gateway,
        storage,
        Settings(),
        PROCESS_NOW + timedelta(days=1),
    )

    assert rerun.status is AccountDeletionStatus.COMPLETED
    assert rerun.attempt_count == completed.attempt_count == 1
    assert gateway.calls == []
    assert storage.deleted == []


@pytest.mark.parametrize(
    "dependency_name",
    ["get_current_user", "get_account_deletion_request_user"],
)
def test_anonymized_firebase_identity_is_forbidden_in_every_auth_dependency(
    db_session, monkeypatch, dependency_name
):
    from app import dependencies
    from app.account_deletion import (
        process_account_deletion,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    original_phone = user.phone_number
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    process_account_deletion(
        db_session,
        job.id,
        FakeAccountGateway(),
        FakeDeletionStorage(),
        Settings(),
        PROCESS_NOW,
    )
    monkeypatch.setattr(
        dependencies,
        "verify_firebase_id_token",
        lambda _token: FirebaseIdentity("firebase-user-secret", original_phone),
    )

    with pytest.raises(HTTPException) as error:
        getattr(dependencies, dependency_name)(
            HTTPAuthorizationCredentials(scheme="Bearer", credentials="token"),
            db_session,
            Settings(firebase_auth_enabled=True),
        )

    assert error.value.status_code == 403
    assert db_session.query(User).count() == 1


def add_retry_job(
    db_session,
    *,
    status=AccountDeletionStatus.PENDING,
    attempt_count=0,
    next_retry_at=None,
):
    user = User(
        phone_number=f"test-retry-{uuid4().hex}",
        account_status=(
            AccountStatus.ANONYMIZED
            if status is AccountDeletionStatus.COMPLETED
            else AccountStatus.DELETION_PENDING
        ),
        anonymized_subject_id=f"anon-{uuid4().hex}",
    )
    db_session.add(user)
    db_session.flush()
    job = AccountDeletionJob(
        user_id=user.id,
        status=status,
        attempt_count=attempt_count,
        next_retry_at=next_retry_at,
        completed_at=PROCESS_NOW if status is AccountDeletionStatus.COMPLETED else None,
    )
    db_session.add(job)
    db_session.commit()
    return job


def test_automatic_retry_selection_is_due_pending_and_attempt_bounded(db_session):
    from app.account_deletion import eligible_account_deletion_jobs

    pending = add_retry_job(db_session)
    due = add_retry_job(
        db_session,
        status=AccountDeletionStatus.RETRY_REQUIRED,
        attempt_count=4,
        next_retry_at=PROCESS_NOW,
    )
    add_retry_job(
        db_session,
        status=AccountDeletionStatus.RETRY_REQUIRED,
        attempt_count=4,
        next_retry_at=PROCESS_NOW + timedelta(seconds=1),
    )
    add_retry_job(
        db_session,
        status=AccountDeletionStatus.RETRY_REQUIRED,
        attempt_count=5,
        next_retry_at=PROCESS_NOW,
    )
    add_retry_job(db_session, status=AccountDeletionStatus.COMPLETED)

    jobs = eligible_account_deletion_jobs(
        db_session, Settings(), PROCESS_NOW, automatic=True
    )

    assert {job.id for job in jobs} == {pending.id, due.id}


def test_startup_selection_filters_leases_and_applies_batch_limit_in_sql(
    db_session,
):
    from app.account_deletion import eligible_account_deletion_jobs

    pending_jobs = [add_retry_job(db_session) for _ in range(3)]
    expired = add_retry_job(
        db_session,
        status=AccountDeletionStatus.PROCESSING,
        attempt_count=1,
    )
    expired.lease_until = PROCESS_NOW - timedelta(seconds=1)
    expired.processing_owner_token = "expired-worker"
    live = add_retry_job(
        db_session,
        status=AccountDeletionStatus.PROCESSING,
        attempt_count=1,
    )
    live.lease_until = PROCESS_NOW + timedelta(minutes=1)
    live.processing_owner_token = "live-worker"
    db_session.commit()

    all_eligible = eligible_account_deletion_jobs(
        db_session,
        Settings(),
        PROCESS_NOW,
        automatic=True,
        limit=10,
    )

    assert expired.id in {job.id for job in all_eligible}
    assert live.id not in {job.id for job in all_eligible}
    assert {job.id for job in pending_jobs}.issubset(
        {job.id for job in all_eligible}
    )

    statements = []

    def capture_sql(_connection, _cursor, statement, _parameters, _context, _many):
        statements.append(" ".join(statement.lower().split()))

    bind = db_session.get_bind()
    event.listen(bind, "before_cursor_execute", capture_sql)
    try:
        limited = eligible_account_deletion_jobs(
            db_session,
            Settings(),
            PROCESS_NOW,
            automatic=True,
            limit=2,
        )
    finally:
        event.remove(bind, "before_cursor_execute", capture_sql)

    selection_sql = next(
        statement
        for statement in statements
        if "from account_deletion_jobs" in statement
    )
    assert len(limited) == 2
    assert "account_deletion_jobs.attempt_count" in selection_sql
    assert "account_deletion_jobs.status" in selection_sql
    assert "account_deletion_jobs.next_retry_at" in selection_sql
    assert "account_deletion_jobs.lease_until" in selection_sql
    assert " limit " in selection_sql


def test_explicit_retry_selection_can_override_schedule_and_attempt_limit(db_session):
    from app.account_deletion import eligible_account_deletion_jobs

    exhausted = add_retry_job(
        db_session,
        status=AccountDeletionStatus.RETRY_REQUIRED,
        attempt_count=5,
        next_retry_at=PROCESS_NOW + timedelta(days=1),
    )

    jobs = eligible_account_deletion_jobs(
        db_session,
        Settings(),
        PROCESS_NOW,
        automatic=False,
        job_id=exhausted.id,
    )

    assert [job.id for job in jobs] == [exhausted.id]


def test_retry_runner_continues_after_unexpected_error_without_logging_detail(
    db_session, caplog
):
    from app.account_deletion import run_account_deletion_retries

    first = add_retry_job(db_session)
    second = add_retry_job(db_session)
    processed = []

    def processor(job_id):
        processed.append(job_id)
        if job_id == first.id:
            raise RuntimeError("provider detail and user PII")

    run_account_deletion_retries(
        db_session,
        settings=Settings(),
        now=PROCESS_NOW,
        processor=processor,
        automatic=True,
    )

    assert set(processed) == {first.id, second.id}
    assert "ACCOUNT_DELETION_UNEXPECTED" in caplog.text
    assert str(first.id) in caplog.text
    assert "provider detail" not in caplog.text
    assert "user PII" not in caplog.text


def test_safe_processor_records_unexpected_failure_in_fresh_session(
    deletion_session_factory, caplog
):
    from app.account_deletion import (
        process_account_deletion_safely,
        request_account_deletion,
    )

    with deletion_session_factory() as setup:
        user, _raw_token = add_deletion_subject(setup)
        job = request_account_deletion(setup, user, PROCESS_NOW)
        job_id = job.id

    class UnexpectedGateway(FakeAccountGateway):
        def revoke_tokens(self, uid):
            raise RuntimeError("provider context with user PII and credential path")

    result = process_account_deletion_safely(
        job_id,
        session_factory=deletion_session_factory,
        gateway_factory=lambda _settings: UnexpectedGateway(),
        storage_factory=lambda _settings: FakeDeletionStorage(),
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result is not None
    assert result.status is AccountDeletionStatus.RETRY_REQUIRED
    assert result.attempt_count == 1
    assert result.last_error_code == "ACCOUNT_DELETION_UNEXPECTED"
    assert result.processing_started_at is None
    assert result.lease_until is None
    assert result.processing_owner_token is None
    assert "provider context" not in caplog.text
    assert "user PII" not in caplog.text
    assert "credential path" not in caplog.text


def test_safe_processor_does_not_overwrite_a_different_live_owner(
    deletion_session_factory, monkeypatch, caplog
):
    import app.account_deletion as deletion_module

    with deletion_session_factory() as setup:
        user, _raw_token = add_deletion_subject(setup)
        job = deletion_module.request_account_deletion(setup, user, PROCESS_NOW)
        job_id = job.id

    def lose_lease_then_fail(claimed_job_id, **_kwargs):
        with deletion_session_factory() as competing_worker:
            competing_job = competing_worker.get(
                AccountDeletionJob, claimed_job_id
            )
            competing_job.status = AccountDeletionStatus.PROCESSING
            competing_job.attempt_count = 2
            competing_job.processing_started_at = PROCESS_NOW
            competing_job.lease_until = PROCESS_NOW + timedelta(minutes=10)
            competing_job.processing_owner_token = "new-live-owner"
            competing_worker.commit()
        raise RuntimeError("provider detail and user PII")

    monkeypatch.setattr(
        deletion_module,
        "process_account_deletion_by_id",
        lose_lease_then_fail,
    )

    result = deletion_module.process_account_deletion_safely(
        job_id,
        session_factory=deletion_session_factory,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result is not None
    assert result.status is AccountDeletionStatus.PROCESSING
    assert result.attempt_count == 2
    assert result.processing_owner_token == "new-live-owner"
    assert result.last_error_code is None
    assert "provider detail" not in caplog.text
    assert "user PII" not in caplog.text


def test_process_by_id_records_safe_gateway_initialization_failure(db_session):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)

    def unavailable_gateway(_settings):
        raise AccountDeletionProviderError("FIREBASE_AUTH_UNAVAILABLE")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=unavailable_gateway,
        storage_factory=lambda _settings: FakeDeletionStorage(),
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result is job
    assert result.status is AccountDeletionStatus.RETRY_REQUIRED
    assert result.attempt_count == 1
    assert result.last_error_code == "FIREBASE_AUTH_UNAVAILABLE"


def test_process_by_id_records_fixed_storage_configuration_failure(db_session):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)

    def unavailable_storage(_settings):
        raise ValueError("storage provider, user UID, and secret details")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=lambda _settings: FakeAccountGateway(),
        storage_factory=unavailable_storage,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.RETRY_REQUIRED
    assert result.attempt_count == 1
    assert result.last_error_code == "MEDIA_STORAGE_UNAVAILABLE"
    assert "secret" not in result.last_error_code.lower()


def test_process_by_id_skips_firebase_initialization_for_legacy_account(db_session):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    user = User(phone_number="+905551234571", role=UserRole.FARMER)
    db_session.add(user)
    db_session.commit()
    job = request_account_deletion(db_session, user, PROCESS_NOW)

    def forbidden_gateway_initialization(_settings):
        raise AssertionError("Firebase gateway must not be created without a UID")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=forbidden_gateway_initialization,
        storage_factory=lambda _settings: FakeDeletionStorage(),
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert user.account_status is AccountStatus.ANONYMIZED


def test_completed_firebase_steps_do_not_initialize_an_unavailable_gateway(
    db_session,
):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    user, _raw_token = add_deletion_subject(db_session)
    job = request_account_deletion(db_session, user, PROCESS_NOW)
    job.firebase_tokens_revoked_at = PROCESS_NOW
    job.firestore_anonymized_at = PROCESS_NOW
    job.firebase_auth_deleted_at = PROCESS_NOW
    db_session.commit()
    storage = FakeDeletionStorage()

    def unavailable_gateway(_settings):
        raise AssertionError("completed Firebase steps must not create the gateway")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=unavailable_gateway,
        storage_factory=lambda _settings: storage,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert storage.deleted == ["owned-media-key"]


def test_empty_media_step_completes_without_initializing_storage(db_session):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    user = User(phone_number="+905551234568", role=UserRole.FARMER)
    db_session.add(user)
    db_session.commit()
    job = request_account_deletion(db_session, user, PROCESS_NOW)

    def unavailable_storage(_settings):
        raise AssertionError("empty media ownership must not create storage")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=lambda _settings: FakeAccountGateway(),
        storage_factory=unavailable_storage,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert result.media_deleted_at is not None


@pytest.mark.parametrize(
    ("attempt_count", "next_retry_at"),
    [
        (5, PROCESS_NOW),
        (4, PROCESS_NOW + timedelta(seconds=1)),
    ],
    ids=["attempt-limit", "not-due"],
)
def test_process_by_id_automatic_retry_does_not_bypass_retry_bounds(
    db_session, attempt_count, next_retry_at
):
    from app.account_deletion import process_account_deletion_by_id

    job = add_retry_job(
        db_session,
        status=AccountDeletionStatus.RETRY_REQUIRED,
        attempt_count=attempt_count,
        next_retry_at=next_retry_at,
    )

    def forbidden_factory(_settings):
        raise AssertionError("ineligible automatic retry advanced")

    result = process_account_deletion_by_id(
        job.id,
        session_factory=lambda: nullcontext(db_session),
        gateway_factory=forbidden_factory,
        storage_factory=forbidden_factory,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.RETRY_REQUIRED
    assert result.attempt_count == attempt_count
    assert result.next_retry_at == next_retry_at


def test_application_lifespan_invokes_startup_deletion_retry_off_event_loop(
    monkeypatch,
):
    import app.main as main_module

    app = main_module.app
    lifespan = main_module.lifespan

    calls = []
    thread_calls = []
    missing = object()
    state_names = (
        "redis",
        "otp_store",
        "weather_provider",
        "push_provider",
        "ai_chat_provider",
        "media_storage",
    )
    previous_state = {
        name: getattr(app.state, name, missing) for name in state_names
    }

    class FakeRedis:
        def close(self):
            pass

    monkeypatch.setattr("app.main.Redis.from_url", lambda *args, **kwargs: FakeRedis())
    monkeypatch.setattr("app.main.create_weather_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_push_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_ai_chat_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_media_storage", lambda _settings: object())
    def startup_retry():
        calls.append("startup-retry")

    async def to_thread(function, *args, **kwargs):
        thread_calls.append(function)
        return function(*args, **kwargs)

    monkeypatch.setattr(main_module, "run_startup_account_deletion_retries", startup_retry)

    class AsyncioStub:
        pass

    AsyncioStub.to_thread = staticmethod(to_thread)
    monkeypatch.setattr(main_module, "asyncio", AsyncioStub, raising=False)

    async def enter_lifespan():
        async with lifespan(app):
            assert calls == ["startup-retry"]
            assert thread_calls == [startup_retry]

    try:
        asyncio.run(enter_lifespan())
    finally:
        for name, previous in previous_state.items():
            if previous is missing:
                delattr(app.state, name)
            else:
                setattr(app.state, name, previous)


def test_application_lifespan_contains_startup_retry_failure(monkeypatch, caplog):
    import app.main as main_module

    app = main_module.app
    lifespan = main_module.lifespan
    missing = object()
    state_names = (
        "redis",
        "otp_store",
        "weather_provider",
        "push_provider",
        "ai_chat_provider",
        "media_storage",
    )
    previous_state = {
        name: getattr(app.state, name, missing) for name in state_names
    }

    class FakeRedis:
        def close(self):
            pass

    monkeypatch.setattr("app.main.Redis.from_url", lambda *args, **kwargs: FakeRedis())
    monkeypatch.setattr("app.main.create_weather_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_push_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_ai_chat_provider", lambda _settings: object())
    monkeypatch.setattr("app.main.create_media_storage", lambda _settings: object())

    def failing_startup_retry():
        raise RuntimeError("provider details and user PII")

    async def to_thread(function, *args, **kwargs):
        return function(*args, **kwargs)

    monkeypatch.setattr(
        main_module,
        "run_startup_account_deletion_retries",
        failing_startup_retry,
    )

    class AsyncioStub:
        pass

    AsyncioStub.to_thread = staticmethod(to_thread)
    monkeypatch.setattr(main_module, "asyncio", AsyncioStub, raising=False)
    entered = []

    async def enter_lifespan():
        async with lifespan(app):
            entered.append(True)

    try:
        asyncio.run(enter_lifespan())
    finally:
        for name, previous in previous_state.items():
            if previous is missing:
                delattr(app.state, name)
            else:
                setattr(app.state, name, previous)

    assert entered == [True]
    assert "ACCOUNT_DELETION_UNEXPECTED" in caplog.text
    assert "provider details" not in caplog.text
    assert "user PII" not in caplog.text


def test_expired_processing_lease_is_reclaimed_and_completed(
    deletion_session_factory,
):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    with deletion_session_factory() as setup:
        user, _raw_token = add_deletion_subject(setup)
        job = request_account_deletion(setup, user, PROCESS_NOW)
        job.status = AccountDeletionStatus.PROCESSING
        job.attempt_count = 1
        job.processing_started_at = PROCESS_NOW - timedelta(minutes=20)
        job.lease_until = PROCESS_NOW - timedelta(minutes=10)
        job.processing_owner_token = "abandoned-worker"
        setup.commit()
        job_id = job.id

    gateway = FakeAccountGateway()
    storage = FakeDeletionStorage()
    result = process_account_deletion_by_id(
        job_id,
        session_factory=deletion_session_factory,
        gateway_factory=lambda _settings: gateway,
        storage_factory=lambda _settings: storage,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
    )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert result.attempt_count == 2
    assert gateway.calls == ["firebase_tokens", "firestore", "firebase_auth"]
    assert storage.deleted == ["owned-media-key"]


def test_explicit_retry_does_not_steal_a_live_processing_lease(
    deletion_session_factory,
):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    with deletion_session_factory() as setup:
        user, _raw_token = add_deletion_subject(setup)
        job = request_account_deletion(setup, user, PROCESS_NOW)
        job.status = AccountDeletionStatus.PROCESSING
        job.attempt_count = 1
        job.processing_started_at = PROCESS_NOW
        job.lease_until = PROCESS_NOW + timedelta(minutes=10)
        job.processing_owner_token = "live-worker"
        setup.commit()
        job_id = job.id

    factory_calls = []

    def gateway_factory(_settings):
        factory_calls.append("gateway")
        return FakeAccountGateway()

    def storage_factory(_settings):
        factory_calls.append("storage")
        return FakeDeletionStorage()

    result = process_account_deletion_by_id(
        job_id,
        session_factory=deletion_session_factory,
        gateway_factory=gateway_factory,
        storage_factory=storage_factory,
        settings=Settings(),
        now_factory=lambda: PROCESS_NOW,
        force=True,
    )

    assert result.status is AccountDeletionStatus.PROCESSING
    assert result.attempt_count == 1
    assert factory_calls == []
    assert result.processing_owner_token == "live-worker"


def test_stale_second_session_cannot_repeat_completed_external_steps(
    deletion_session_factory,
):
    from app.account_deletion import (
        process_account_deletion_by_id,
        request_account_deletion,
    )

    with deletion_session_factory() as setup:
        user, _raw_token = add_deletion_subject(setup)
        job = request_account_deletion(setup, user, PROCESS_NOW)
        job_id = job.id

    stale_session = deletion_session_factory()
    stale_job = stale_session.get(AccountDeletionJob, job_id)
    assert stale_job.status is AccountDeletionStatus.PENDING
    first_gateway = FakeAccountGateway()
    first_storage = FakeDeletionStorage()
    second_gateway = FakeAccountGateway()
    second_storage = FakeDeletionStorage()
    try:
        first = process_account_deletion_by_id(
            job_id,
            session_factory=deletion_session_factory,
            gateway_factory=lambda _settings: first_gateway,
            storage_factory=lambda _settings: first_storage,
            settings=Settings(),
            now_factory=lambda: PROCESS_NOW,
            force=True,
        )
        second = process_account_deletion_by_id(
            job_id,
            session_factory=lambda: nullcontext(stale_session),
            gateway_factory=lambda _settings: second_gateway,
            storage_factory=lambda _settings: second_storage,
            settings=Settings(),
            now_factory=lambda: PROCESS_NOW + timedelta(seconds=1),
            force=True,
        )
    finally:
        stale_session.close()

    assert first.status is AccountDeletionStatus.COMPLETED
    assert second.status is AccountDeletionStatus.COMPLETED
    assert first_gateway.calls == ["firebase_tokens", "firestore", "firebase_auth"]
    assert first_storage.deleted == ["owned-media-key"]
    assert second_gateway.calls == []
    assert second_storage.deleted == []


def _commit_deletion_lock(session_factory, user_id):
    from app.account_deletion import request_account_deletion

    with session_factory() as deleter:
        user = deleter.get(User, user_id)
        request_account_deletion(deleter, user, PROCESS_NOW)


def test_legacy_session_issue_rechecks_active_after_competing_deletion(
    deletion_session_factory,
):
    from app.routers.auth import issue_session

    with deletion_session_factory() as setup:
        user = User(phone_number="+905551230001", role=UserRole.FARMER)
        setup.add(user)
        setup.commit()
        user_id = user.id

    writer = deletion_session_factory()
    stale_user = writer.get(User, user_id)
    _commit_deletion_lock(deletion_session_factory, user_id)
    try:
        with pytest.raises(HTTPException) as error:
            issue_session(stale_user, writer, Settings())
    finally:
        writer.rollback()
        writer.close()

    assert error.value.status_code == 403
    with deletion_session_factory() as check:
        assert check.query(User).filter_by(id=user_id).one().refresh_tokens == []


def test_refresh_rotation_rechecks_active_after_competing_deletion(
    deletion_session_factory, monkeypatch
):
    from app.routers.auth import refresh_session

    with deletion_session_factory() as setup:
        user = User(phone_number="+905551230002", role=UserRole.FARMER)
        setup.add(user)
        setup.flush()
        raw_token, token = create_refresh_token(user.id, Settings())
        setup.add(token)
        setup.commit()
        user_id = user.id
        token_id = token.id

    writer = deletion_session_factory()
    stale_user = writer.get(User, user_id)
    stale_token = writer.get(type(token), token_id)
    assert stale_user.account_status is AccountStatus.ACTIVE
    assert stale_token.revoked_at is None
    original_scalar = writer.scalar
    deletion_committed = False

    def scalar_then_commit_deletion(statement, *args, **kwargs):
        nonlocal deletion_committed
        result = original_scalar(statement, *args, **kwargs)
        if not deletion_committed:
            deletion_committed = True
            _commit_deletion_lock(deletion_session_factory, user_id)
        return result

    monkeypatch.setattr(writer, "scalar", scalar_then_commit_deletion)
    try:
        with pytest.raises(HTTPException) as error:
            refresh_session(RefreshTokenRequest(refresh_token=raw_token), writer, Settings())
    finally:
        writer.rollback()
        writer.close()

    assert deletion_committed is True
    assert error.value.status_code == 403
    with deletion_session_factory() as check:
        assert all(item.revoked_at is not None for item in check.get(User, user_id).refresh_tokens)


def test_media_uploaded_before_competing_deletion_lock_is_removed_on_denial(
    deletion_session_factory,
):
    from app.routers.media import upload_media

    with deletion_session_factory() as setup:
        user = User(phone_number="+905551230003", role=UserRole.FARMER)
        setup.add(user)
        setup.commit()
        user_id = user.id

    writer = deletion_session_factory()
    stale_user = writer.get(User, user_id)
    storage = FakeRaceStorage(
        on_save=lambda: _commit_deletion_lock(deletion_session_factory, user_id)
    )
    try:
        with pytest.raises(HTTPException) as error:
            asyncio.run(
                upload_media(
                    FakeUploadFile(),
                    stale_user,
                    writer,
                    Settings(),
                    storage,
                )
            )
    finally:
        writer.rollback()
        writer.close()

    assert error.value.status_code == 403
    assert storage.saved == {}
    assert len(storage.deleted) == 1
    with deletion_session_factory() as check:
        assert check.query(MediaAsset).filter_by(owner_id=user_id).count() == 0


def test_media_committed_before_deletion_is_seen_by_the_deletion_sweep(
    deletion_session_factory,
):
    from app.account_deletion import process_account_deletion, request_account_deletion
    from app.routers.media import upload_media

    with deletion_session_factory() as setup:
        user = User(phone_number="+905551230004", role=UserRole.FARMER)
        setup.add(user)
        setup.commit()
        user_id = user.id

    storage = FakeRaceStorage()
    with deletion_session_factory() as writer:
        writer_user = writer.get(User, user_id)
        asset = asyncio.run(
            upload_media(
                FakeUploadFile(),
                writer_user,
                writer,
                Settings(),
                storage,
            )
        )
        storage_key = asset.storage_key

    with deletion_session_factory() as deleter:
        user = deleter.get(User, user_id)
        job = request_account_deletion(deleter, user, PROCESS_NOW)
        result = process_account_deletion(
            deleter,
            job.id,
            FakeAccountGateway(),
            storage,
            Settings(),
            PROCESS_NOW,
        )

    assert result.status is AccountDeletionStatus.COMPLETED
    assert storage_key in storage.deleted


def test_device_registration_rechecks_active_after_competing_deletion(
    deletion_session_factory,
):
    from app.routers.notifications import register_device

    with deletion_session_factory() as setup:
        user = User(phone_number="+905551230005", role=UserRole.FARMER)
        setup.add(user)
        setup.commit()
        user_id = user.id

    writer = deletion_session_factory()
    stale_user = writer.get(User, user_id)
    _commit_deletion_lock(deletion_session_factory, user_id)
    try:
        with pytest.raises(HTTPException) as error:
            register_device(
                DeviceTokenRegister(
                    token="race-device-token",
                    platform=DevicePlatform.ANDROID,
                ),
                stale_user,
                writer,
                object(),
            )
    finally:
        writer.rollback()
        writer.close()

    assert error.value.status_code == 403
    with deletion_session_factory() as check:
        assert check.query(DeviceToken).filter_by(user_id=user_id).count() == 0

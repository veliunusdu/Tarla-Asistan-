import os
from pathlib import Path
import subprocess
import sys
from datetime import timedelta

import pytest
from firebase_admin import auth, exceptions as firebase_exceptions
from google.api_core import exceptions as google_exceptions
from pydantic import ValidationError
from sqlalchemy.exc import IntegrityError

from app.config import Settings
from app.firebase_account import (
    AccountDeletionProviderError,
    FirebaseAdminAccountGateway,
)
from app.models import (
    AccountDeletionJob,
    AccountDeletionStatus,
    AccountStatus,
    FirebaseLinkApproval,
    User,
    UserRole,
    utcnow,
)


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

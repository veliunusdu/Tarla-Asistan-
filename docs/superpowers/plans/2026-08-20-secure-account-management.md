# Secure Account Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent unapproved Firebase linking for agronomists and add a retryable account-deletion flow that removes identity data while preserving anonymized agricultural and audit records.

**Architecture:** PostgreSQL remains the workflow source of truth through one-time Firebase-link approvals and persistent deletion jobs. A focused Firebase Admin gateway performs token revocation, named-database Firestore anonymization, and Auth deletion; a service coordinates idempotent steps and records completion timestamps. FastAPI exposes only the user deletion request, while operator-only actions remain CLI commands; Flutter provides explicit confirmation and clears local personal data after `202 Accepted`.

**Tech Stack:** Python 3.12, FastAPI, SQLAlchemy 2, Alembic, PostgreSQL/SQLite tests, Firebase Admin SDK, Enterprise Firestore Native (`tarla-asistani`), Flutter/Dart, Firebase Auth, sqflite, pytest.

**Spec:** `docs/superpowers/specs/2026-08-21-secure-account-management-design.md`

## Global Constraints

- Firebase project is `demo2-c4265`; the named Enterprise Firestore database is exactly `tarla-asistani`.
- Agronomist Firebase UID linking requires an unexpired, unconsumed operator approval; farmer phone linking keeps its existing automatic behavior.
- Account deletion immediately changes the local account to `DELETION_PENDING`; failed external work never reactivates it.
- Personal data and media objects are removed; agricultural and audit rows remain attached to an anonymized PostgreSQL subject.
- Provider exception messages, bearer tokens, service-account contents, phone numbers, and Firebase UIDs must not be written to deletion error fields or logs.
- External steps are idempotent and store individual completion timestamps before a retry advances.
- No operator approval HTTP endpoint, admin UI, 30-day waiting period, generic `Idempotency-Key`, staging deploy, or unrelated refactor is part of this plan.

---

## File Structure

- `backend/app/models.py`: account-state, approval, and deletion-job persistence models.
- `backend/migrations/versions/20260820_0008_secure_account_management.py`: reversible PostgreSQL schema migration.
- `backend/app/firebase_mapping.py`: role-aware, transaction-safe Firebase-to-local-account mapping.
- `backend/app/firebase_account.py`: narrow Firebase Auth and named Firestore Admin gateway.
- `backend/app/account_deletion.py`: deletion request creation and stepwise retry coordinator.
- `backend/app/manage.py`: operator CLI for link approval, dry-run inspection, and deletion retry.
- `backend/app/routers/users.py`: authenticated deletion-request endpoint only.
- `backend/app/dependencies.py`: delegates identity mapping and rejects non-active accounts.
- `backend/app/routers/auth.py`: blocks refresh-token renewal for non-active accounts.
- `backend/app/config.py`: exact Firestore database ID and retry settings.
- `backend/app/main.py`: bounded startup retry and application dependencies.
- `backend/app/schemas.py`: deletion request/response DTOs and account status output.
- `backend/tests/test_firebase_mapping.py`: approval and race behavior.
- `backend/tests/test_account_deletion.py`: request, authorization, external-step, retry, and anonymization behavior.
- `backend/tests/test_manage.py`: operator command validation and dry-run behavior.
- `mobile/lib/services/account_deletion_service.dart`: API request plus local cleanup orchestration.
- `mobile/lib/screens/hesap_ayarlari_ekrani.dart`: destructive action and typed confirmation UI.
- `mobile/lib/services/database_helper.dart`: transactional removal of local farms, activities, and queued operations.
- `mobile/lib/screens/ozet_ekrani.dart`, `mobile/lib/main.dart`: account-settings navigation and dependency wiring.
- `mobile/test/account_deletion_service_test.dart`, `mobile/test/hesap_ayarlari_ekrani_test.dart`: mobile flow tests.
- `docs/OPENAPI.md`, `docs/istenilenDosyalar/API_DOCUMENTATION.md`, `docs/istenilenDosyalar/BACKEND_DURUM_RAPORU.md`, `docs/istenilenDosyalar/EKSIKLER_VE_AKSIYONLAR.md`: exact delivered contract and updated status.

---

### Task 1: Persist Account State, Link Approvals, and Deletion Jobs

**Files:**
- Modify: `backend/app/models.py`
- Create: `backend/migrations/versions/20260820_0008_secure_account_management.py`
- Test: `backend/tests/test_account_deletion.py`

**Interfaces:**
- Produces: `AccountStatus`, `FirebaseLinkApproval`, `AccountDeletionStatus`, `AccountDeletionJob`.
- Produces: `User.account_status`, `User.deleted_at`, `User.anonymized_subject_id`.
- Consumes: existing `User`, `Profile`, `RefreshToken`, `DeviceToken`, and `MediaAsset` models.

- [ ] **Step 1: Write failing model tests**

```python
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
```

- [ ] **Step 2: Run the model test and verify RED**

Run: `cd backend && python -m pytest tests/test_account_deletion.py::test_secure_account_models_have_safe_defaults -q`

Expected: FAIL because the new enums and models do not exist.

- [ ] **Step 3: Add exact enums, fields, relationships, and constraints**

```python
class AccountStatus(str, enum.Enum):
    ACTIVE = "ACTIVE"
    DELETION_PENDING = "DELETION_PENDING"
    ANONYMIZED = "ANONYMIZED"

class AccountDeletionStatus(str, enum.Enum):
    PENDING = "PENDING"
    PROCESSING = "PROCESSING"
    RETRY_REQUIRED = "RETRY_REQUIRED"
    COMPLETED = "COMPLETED"

class FirebaseLinkApproval(Base):
    __tablename__ = "firebase_link_approvals"
    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id"), index=True)
    firebase_uid: Mapped[str] = mapped_column(String(128), unique=True)
    approved_by: Mapped[str] = mapped_column(String(120))
    approved_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), index=True)
    consumed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)

class AccountDeletionJob(Base):
    __tablename__ = "account_deletion_jobs"
    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("users.id"), unique=True)
    firebase_uid_snapshot: Mapped[str | None] = mapped_column(String(128))
    status: Mapped[AccountDeletionStatus] = mapped_column(
        Enum(AccountDeletionStatus, name="account_deletion_status"),
        default=AccountDeletionStatus.PENDING,
    )
    attempt_count: Mapped[int] = mapped_column(Integer, default=0)
    last_error_code: Mapped[str | None] = mapped_column(String(80))
    next_retry_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    firebase_tokens_revoked_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    firestore_anonymized_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    media_deleted_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    firebase_auth_deleted_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    postgres_anonymized_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=utcnow, onupdate=utcnow)
    completed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
```

Add `account_status`, `deleted_at`, and `anonymized_subject_id` to `User`; make `Profile.full_name`, `province`, and `district` nullable in both model annotations and migration. Increase `users.phone_number` to 64 characters so `deleted-<uuid>` fits. Add a partial unique index that permits only one unconsumed approval per `user_id`; the CLI will mark an expired approval consumed before replacing it.

- [ ] **Step 4: Implement and inspect migration upgrade/downgrade**

The migration must create PostgreSQL enums with SQLAlchemy, add the user columns with server default `ACTIVE`, alter profile columns to nullable and phone length to 64, create both tables and indexes, then remove the temporary server default. Downgrade must drop tables/indexes/columns before dropping enum types.

Run: `cd backend && alembic upgrade head && alembic downgrade 20260820_0007 && alembic upgrade head`

Expected: all three commands exit 0 against the configured development database.

- [ ] **Step 5: Run focused tests and commit**

Run: `cd backend && python -m pytest tests/test_account_deletion.py -q`

Expected: model tests PASS.

Commit: `feat: add secure account lifecycle models`

---

### Task 2: Require One-Time Approval for Agronomist Firebase Linking

**Files:**
- Create: `backend/app/firebase_mapping.py`
- Create: `backend/app/manage.py`
- Modify: `backend/app/dependencies.py`
- Test: `backend/tests/test_firebase_mapping.py`
- Test: `backend/tests/test_manage.py`

**Interfaces:**
- Consumes: `FirebaseIdentity`, `UserRole`, `AccountStatus`, `FirebaseLinkApproval`.
- Produces: `resolve_firebase_user(db: Session, identity: FirebaseIdentity) -> User`.
- Produces: `approve_firebase_link(db: Session, *, user_id: UUID, firebase_uid: str, operator: str, now: datetime, dry_run: bool = False) -> FirebaseLinkApproval | None`.

- [ ] **Step 1: Write failing role-aware mapping tests**

```python
def test_unapproved_agronomist_link_is_forbidden(db_session):
    user = User(phone_number="+905551234581", role=UserRole.AGRONOMIST)
    db_session.add(user)
    db_session.commit()
    with pytest.raises(HTTPException) as error:
        resolve_firebase_user(
            db_session,
            FirebaseIdentity(uid="expert-uid", phone_number=user.phone_number),
        )
    assert error.value.status_code == 403
    db_session.refresh(user)
    assert user.firebase_uid is None

def test_approved_agronomist_link_consumes_approval(db_session):
    user, approval = add_approved_expert(db_session, uid="expert-uid")
    resolved = resolve_firebase_user(
        db_session,
        FirebaseIdentity(uid="expert-uid", phone_number=user.phone_number),
    )
    db_session.refresh(approval)
    assert resolved.id == user.id
    assert approval.consumed_at is not None
```

Also test expired approval, consumed approval, wrong UID, farmer auto-link, same-link idempotency, conflicting UID, and independent-session unique races.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `cd backend && python -m pytest tests/test_firebase_mapping.py -q`

Expected: FAIL because `firebase_mapping.py` does not exist.

- [ ] **Step 3: Extract mapping into one transaction-safe service**

```python
def resolve_firebase_user(db: Session, identity: FirebaseIdentity) -> User:
    existing_uid = db.scalar(select(User).where(User.firebase_uid == identity.uid))
    if existing_uid is not None:
        return require_active(existing_uid)
    if not identity.phone_number:
        raise unauthorized()
    user = db.scalar(select(User).where(User.phone_number == identity.phone_number))
    if user is None:
        return create_farmer_with_uid(db, identity)
    require_active(user)
    if user.firebase_uid is not None:
        raise mapping_conflict()
    if user.role is UserRole.AGRONOMIST:
        return consume_approval_and_link(db, user=user, uid=identity.uid)
    return link_farmer(db, user=user, uid=identity.uid)
```

`consume_approval_and_link` must conditionally update both the user (`firebase_uid IS NULL`) and approval (`consumed_at IS NULL`, `expires_at > now`) before one commit. A missing approval raises a generic `403` without phone/UID. Integrity errors roll back and re-read; a different winner returns `409`.

- [ ] **Step 4: Delegate Firebase mapping from the auth dependency**

Replace the inline mapping branch in `get_current_user` with:

```python
identity = verify_firebase_id_token(credentials.credentials)
return resolve_firebase_user(db, identity)
```

Keep Firebase token/unavailable exception translation in `dependencies.py`. Ensure the returned user is `ACTIVE` for both Firebase and legacy JWT paths.

- [ ] **Step 5: Add the operator-only approval command with dry-run**

```python
def approve_firebase_link(..., dry_run: bool = False):
    user = db.get(User, user_id)
    if user is None or user.role is not UserRole.AGRONOMIST:
        raise ManagementCommandError("Hedef kullanıcı uygun bir uzman hesabı değil.")
    if db.scalar(select(User).where(User.firebase_uid == firebase_uid)) is not None:
        raise ManagementCommandError("Firebase UID zaten bağlı.")
    if dry_run:
        return None
    # consume expired open approvals, add a 24-hour approval, commit
```

Expose:

```text
python -m app.manage approve-firebase-link --user-id <uuid> --firebase-uid <uid> --operator <label> [--dry-run]
```

CLI output may contain the local user UUID and expiry, but never phone number, Firebase UID, or token.

- [ ] **Step 6: Run mapping, CLI, and regression tests; commit**

Run: `cd backend && python -m pytest tests/test_firebase_mapping.py tests/test_manage.py tests/test_firebase_auth.py tests/test_auth.py -q`

Expected: all tests PASS, including existing farmer and legacy JWT cases.

Commit: `feat: require approval for expert firebase linking`

---

### Task 3: Build the Firebase Account Administration Gateway

**Files:**
- Modify: `backend/app/config.py`
- Modify: `backend/app/firebase_auth.py`
- Create: `backend/app/firebase_account.py`
- Test: `backend/tests/test_account_deletion.py`

**Interfaces:**
- Consumes: named Firebase Admin app from `get_firebase_auth_app()`.
- Produces: `FirebaseAccountGateway` protocol with `revoke_tokens(uid)`, `anonymize_firestore(uid, anonymous_subject)`, `delete_auth_user(uid)`.
- Produces: `FirebaseAdminAccountGateway` using database ID from `Settings.firestore_database_id`.
- Produces: stable `AccountDeletionProviderError(code: str)` without provider detail exposure.

- [ ] **Step 1: Write failing gateway tests with fake Admin modules**

```python
def test_gateway_uses_named_database_and_anonymizes_owned_farms():
    fake = FakeFirestore(farms=[{"id": "farm-1", "ownerId": "uid-1"}])
    gateway = FirebaseAdminAccountGateway(
        app=object(), auth_module=FakeAuth(), firestore_factory=fake.client
    )
    gateway.anonymize_firestore("uid-1", "anon-123")
    assert fake.database_id == "tarla-asistani"
    assert fake.query == ("ownerId", "==", "uid-1")
    assert fake.updated["farm-1"] == {
        "ownerId": "anon-123",
        "anonymousOwnerId": "anon-123",
    }
    assert fake.deleted_documents == ["users/uid-1"]
```

Test missing Auth user as success, transient Firestore failure mapped to `FIRESTORE_UNAVAILABLE`, batches above 500 documents, and no raw provider text in exception string.

- [ ] **Step 2: Run gateway tests and verify RED**

Run: `cd backend && python -m pytest tests/test_account_deletion.py -k gateway -q`

Expected: FAIL because the gateway is absent.

- [ ] **Step 3: Add explicit configuration and Admin gateway**

Add:

```python
firestore_database_id: str = "tarla-asistani"
account_deletion_retry_minutes: int = Field(default=15, ge=1, le=1440)
account_deletion_max_automatic_attempts: int = Field(default=5, ge=1, le=20)
```

The gateway must call `firebase_admin.firestore.client(app=app, database_id=settings.firestore_database_id)`, query farms by exact `ownerId`, update in chunks no larger than 450, and delete `users/{uid}`. `auth.UserNotFoundError` during revoke/delete is an idempotent success. Other Firebase errors are translated to fixed codes only.

- [ ] **Step 4: Run focused tests and commit**

Run: `cd backend && python -m pytest tests/test_account_deletion.py -k gateway -q`

Expected: gateway tests PASS.

Commit: `feat: add firebase account administration gateway`

---

### Task 4: Implement Retryable Deletion Service, Endpoint, and Access Lock

**Files:**
- Create: `backend/app/account_deletion.py`
- Modify: `backend/app/schemas.py`
- Modify: `backend/app/routers/users.py`
- Modify: `backend/app/routers/auth.py`
- Modify: `backend/app/dependencies.py`
- Modify: `backend/app/main.py`
- Modify: `backend/app/manage.py`
- Test: `backend/tests/test_account_deletion.py`
- Test: `backend/tests/test_auth.py`
- Test: `backend/tests/test_manage.py`

**Interfaces:**
- Consumes: `FirebaseAccountGateway`, `MediaStorage`, `SessionLocal`, Task 1 models.
- Produces: `request_account_deletion(db, user, now) -> AccountDeletionJob`.
- Produces: `process_account_deletion(db, job_id, gateway, storage, settings, now) -> AccountDeletionJob`.
- Produces: `POST /api/v1/users/me/deletion-request` with exact `202` response.

- [ ] **Step 1: Write failing request and authorization tests**

```python
def test_deletion_request_locks_account_and_is_idempotent(client, owner_headers, db_session):
    first = client.post(
        "/api/v1/users/me/deletion-request",
        headers=owner_headers,
        json={"confirmation": "HESABIMI SIL"},
    )
    second = client.post(
        "/api/v1/users/me/deletion-request",
        headers=owner_headers,
        json={"confirmation": "HESABIMI SIL"},
    )
    assert first.status_code == second.status_code == 202
    assert first.json() == second.json()
    job = db_session.get(AccountDeletionJob, UUID(first.json()["request_id"]))
    assert job.user.account_status is AccountStatus.DELETION_PENDING
    assert all(token.revoked_at is not None for token in job.user.refresh_tokens)
```

Also test exact confirmation validation, unauthenticated `401`, pending user `403` on normal endpoints, and legacy refresh denial after locking.

- [ ] **Step 2: Write failing step/retry/anonymization tests**

Use fake gateway and storage to prove each completion timestamp is skipped on rerun. Force failure at each external step and assert `RETRY_REQUIRED`, fixed `last_error_code`, incremented attempts, future `next_retry_at`, and unchanged account lock. On success assert:

```python
assert user.phone_number.startswith("deleted-")
assert user.firebase_uid is None
assert user.profile.full_name is None
assert user.profile.province is None
assert user.profile.district is None
assert user.profile.notifications_enabled is False
assert all(token.active is False for token in device_tokens)
assert job.status is AccountDeletionStatus.COMPLETED
assert user.account_status is AccountStatus.ANONYMIZED
```

- [ ] **Step 3: Run service/API tests and verify RED**

Run: `cd backend && python -m pytest tests/test_account_deletion.py tests/test_auth.py -q`

Expected: FAIL because the deletion API and service are absent.

- [ ] **Step 4: Implement request DTOs and transaction**

```python
class AccountDeletionRequest(BaseModel):
    confirmation: str

    @field_validator("confirmation")
    @classmethod
    def exact_confirmation(cls, value: str) -> str:
        if value != "HESABIMI SIL":
            raise ValueError("Hesap silme onay metni eşleşmiyor.")
        return value

class AccountDeletionResponse(BaseModel):
    request_id: uuid.UUID
    status: Literal["PENDING", "PROCESSING", "RETRY_REQUIRED", "COMPLETED"]
```

`request_account_deletion` locks the user, revokes all open `RefreshToken` rows, generates one `anonymized_subject_id`, and creates or returns the user's unique job in one commit. The route returns `202` and schedules processing only after the transaction succeeds.

- [ ] **Step 5: Implement ordered, idempotent processing**

Process only timestamps still `None` in this order:

1. Firebase token revocation.
2. Named Firestore farm ownership anonymization and user document deletion.
3. Delete every owned media object through `MediaStorage`; `MediaStorageMissing` counts as success and metadata rows remain.
4. Firebase Auth user deletion.
5. PostgreSQL anonymization and device-token deactivation.

Commit each completed external step timestamp before starting the next step. On a fixed-code provider/storage error, set `RETRY_REQUIRED`, calculate `next_retry_at`, commit, and return without raising provider detail. The final PostgreSQL transaction sets `ANONYMIZED`, `deleted_at`, `postgres_anonymized_at`, `COMPLETED`, and `completed_at` together.

- [ ] **Step 6: Enforce account lock everywhere sessions can enter**

Create one helper:

```python
def require_active_account(user: User) -> User:
    if user.account_status is not AccountStatus.ACTIVE:
        raise HTTPException(status_code=403, detail="Hesap aktif değil.")
    return user
```

Call it for existing Firebase UID rows, mapped/new Firebase users, legacy JWT users, and `refresh_session` before rotating a token. The deletion endpoint obtains an `ACTIVE` user once, then subsequent normal access is denied.

- [ ] **Step 7: Add bounded background and operator retries**

The route uses `BackgroundTasks.add_task(process_account_deletion_by_id, job.id)`. During lifespan startup, process only jobs whose `status` is `PENDING` or eligible `RETRY_REQUIRED`, limited to the configured automatic-attempt threshold; catch/log only job ID plus stable error code. Add:

```text
python -m app.manage retry-account-deletions [--job-id <uuid>] [--dry-run]
```

Dry-run lists eligible job UUIDs/statuses without external calls. Automatic processing never advances a job at or above the maximum attempt count; the explicit `--job-id` command may retry it.

- [ ] **Step 8: Run backend regression and commit**

Run: `cd backend && python -m pytest -q`

Expected: all backend tests PASS.

Run: `cd backend && python -m ruff check app tests`

Expected: exit 0.

Commit: `feat: add retryable account deletion workflow`

---

### Task 5: Add the Mobile Account Deletion Flow and Local Cleanup

**Files:**
- Create: `mobile/lib/services/account_deletion_service.dart`
- Create: `mobile/lib/screens/hesap_ayarlari_ekrani.dart`
- Modify: `mobile/lib/services/database_helper.dart`
- Modify: `mobile/lib/screens/ozet_ekrani.dart`
- Modify: `mobile/lib/main.dart`
- Create: `mobile/test/account_deletion_service_test.dart`
- Create: `mobile/test/hesap_ayarlari_ekrani_test.dart`
- Modify: `mobile/test/app_accessibility_test.dart`

**Interfaces:**
- Consumes: `ApiClient.postJson`, `FirebaseAuthService.signOut`, `DatabaseHelper`.
- Produces: `AccountDeletionService.requestDeletion(String confirmation) -> Future<String>` returning request ID.
- Produces: `DatabaseHelper.clearPersonalData() -> Future<void>`.
- Produces: `HesapAyarlariEkrani` with typed destructive confirmation.

- [ ] **Step 1: Write failing service tests**

```dart
test('202 cleanup clears local data before signing out', () async {
  final events = <String>[];
  final service = AccountDeletionService(
    api: FakeDeletionApi(events),
    localData: FakeLocalData(events),
    signOut: () async => events.add('signOut'),
  );
  final requestId = await service.requestDeletion('HESABIMI SIL');
  expect(requestId, 'job-1');
  expect(events, ['api', 'clearLocalData', 'signOut']);
});
```

Test API failure leaves local session/data intact, malformed response is safe, and the service sends exactly `POST /users/me/deletion-request` with the confirmation field.

- [ ] **Step 2: Write failing widget tests**

Verify the delete button opens a second screen/dialog, the submit action remains disabled until the exact text `HESABIMI SIL`, progress prevents double submission, success shows the request ID/accepted explanation, and retryable error preserves the confirmation screen.

- [ ] **Step 3: Run mobile tests and verify RED**

Run: `cd mobile && flutter test test/account_deletion_service_test.dart test/hesap_ayarlari_ekrani_test.dart`

Expected: FAIL because the service and screen do not exist.

- [ ] **Step 4: Implement transactional local cleanup**

```dart
Future<void> clearPersonalData() async {
  final db = await database;
  await db.transaction((txn) async {
    await txn.delete('sync_operations');
    await txn.delete('faaliyetler');
    await txn.delete('tarlalar');
  });
}
```

Remove `notification_device_id`, legacy `access_token`, and `refresh_token` from `SharedPreferences`; retain only non-personal onboarding state. Cleanup runs only after a valid successful API response.

- [ ] **Step 5: Implement the account screen and wire navigation**

Add an account/settings icon to `OzetEkrani`. Pass an `AccountDeletionService` from `main.dart`; the success path clears `_postLoginUid`/`_postLoginFuture` after sign-out. The UI text must state that access closes immediately and retained agricultural/audit records are anonymized.

- [ ] **Step 6: Run Flutter regression and commit**

Run: `cd mobile && flutter test`

Expected: all Flutter tests PASS.

Run: `cd mobile && flutter analyze --no-pub`

Expected: exit 0 on a healthy Flutter analysis environment. If the known Windows LSP JSON truncation recurs before diagnostics, save the exact command/output as an environment blocker and do not claim static analysis passed.

Commit: `feat: add mobile account deletion flow`

---

### Task 6: Publish the Contract and Complete Security Verification

**Files:**
- Modify: `docs/OPENAPI.md`
- Modify: `docs/istenilenDosyalar/API_DOCUMENTATION.md`
- Modify: `docs/istenilenDosyalar/BACKEND_DURUM_RAPORU.md`
- Modify: `docs/istenilenDosyalar/EKSIKLER_VE_AKSIYONLAR.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: final request/response/error behavior from Tasks 2–5.
- Produces: exact mobile/backend handoff contract and operator commands without secrets.

- [ ] **Step 1: Add exact API and operator documentation**

Document:

```http
POST /api/v1/users/me/deletion-request
Authorization: Bearer <Firebase ID token>
Content-Type: application/json

{"confirmation":"HESABIMI SIL"}

HTTP/1.1 202 Accepted
{"request_id":"<uuid>","status":"PENDING"}
```

List `400`, `401`, `403`, `409`, and `503` meanings; state timestamps are ISO-8601 UTC when present. Explain that repeated requests return the same job, agronomist linking requires the CLI approval, and approval/retry command output excludes PII and Firebase UIDs.

- [ ] **Step 2: Update status/gap documents truthfully**

Move account deletion and expert-link approval from “missing” to “implemented locally, deployment pending.” Keep staging URL, migration application, real-device Firebase Phone Auth, R2/FCM device verification, and production deployment explicitly unresolved unless fresh evidence exists.

- [ ] **Step 3: Run final backend, mobile, migration, and secret checks**

Run:

```text
cd backend && python -m pytest -q
cd backend && python -m ruff check app tests
cd mobile && flutter test
firebase emulators:exec --only firestore --project demo2-c4265 "npm --prefix firebase-rules-test test"
git diff --check
git status --short
```

Expected: backend, Ruff, Flutter, Firestore Rules, and diff checks PASS. Confirm `.env`, `backend/secrets/`, `google-services.json`, bearer tokens, and real phone numbers are not staged.

- [ ] **Step 4: Request a security-focused code review**

Reviewer must specifically check:

- no agronomist auto-link path remains;
- approval consumption and UID assignment are one transaction;
- pending/anonymized users cannot authenticate or refresh;
- deletion step timestamps make retries safe;
- media contents are removed while metadata remains;
- Firestore uses exactly `tarla-asistani` and old UID access fails;
- no PII/provider details enter logs or error bodies.

Fix every Critical/Important finding and rerun the focused plus full suites.

- [ ] **Step 5: Commit and push only scoped files**

Commit: `docs: publish secure account management contract`

Push the current `codex/sprint-5-veli` branch only after fresh verification. Do not stage `.agents/`, `.vscode/`, `backend/secrets/`, `mobile/firebase.json`, `skills-lock.json`, or any unrelated user files.

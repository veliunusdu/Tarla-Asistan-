# Firebase Authentication and Firestore Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move mobile sign-in to Firebase Phone Authentication and farm/activity data to Firestore, while FastAPI retains AI, R2, FCM delivery, and domain APIs.

**Architecture:** Flutter authenticates and reads/writes farm data directly through Firebase. It sends a short-lived Firebase ID token to FastAPI; FastAPI verifies that token and maps the Firebase UID to a local user. This release starts Firestore empty and does not migrate PostgreSQL records.

**Tech Stack:** Flutter, firebase_core, firebase_auth, cloud_firestore, Firebase Admin SDK, FastAPI, Alembic, pytest.

**Spec:** `docs/superpowers/specs/2026-08-19-firebase-auth-firestore-migration-design.md`

## Global Constraints

- Firebase project: `demo2-c4265`; Android package: `com.tarlaasistani.pilot`.
- Android Firebase app ID: `1:167065176851:android:b4fbe23246580cac2ca8e6`.
- Phone authentication is the only new sign-in flow.
- Firestore starts empty; existing PostgreSQL records remain untouched.
- Roles are server-managed; a mobile client may create only a `FARMER` profile.
- Client-written Firestore timestamps use `FieldValue.serverTimestamp()`.
- Firestore rules permit a user to access only farms whose `ownerId` matches their Firebase UID.
- Do not commit `.env`, `google-services.json`, service-account JSON, tokens, or API keys.

---

## File Structure

- `firestore.rules`: owner-only Firestore access policy.
- `mobile/lib/firebase_options.dart`: generated Firebase options.
- `mobile/lib/services/firebase_auth_service.dart`: phone verification and ID-token access.
- `mobile/lib/services/firestore_farm_repository.dart`: farm/activity streams and writes.
- `backend/app/firebase_auth.py`: Firebase Admin initialization and token verification.
- `backend/app/dependencies.py`, `models.py`, Alembic revision: local-user mapping.
- `backend/tests/test_firebase_auth.py` and mobile service tests: contracts.

### Task 1: Provision Firebase and Android configuration

**Files:**
- Create: `firestore.rules`, `mobile/lib/firebase_options.dart`
- Create locally: `mobile/android/app/google-services.json`
- Modify: `mobile/.gitignore`, `mobile/pubspec.yaml`, Android Gradle files

**Interfaces:**
- Produces the Firebase project configuration required by every later task.

- [ ] **Step 1: Verify the CLI and select the existing project**

Run: `npx -y firebase-tools@latest --version`

Run: `npx -y firebase-tools@latest login --no-localhost`

Run: `npx -y firebase-tools@latest use demo2-c4265`

- [ ] **Step 2: Discover or create Firestore**

Run: `npx -y firebase-tools@latest firestore:databases:list --project demo2-c4265`

If no `(default)` database exists, run `npx -y firebase-tools@latest firestore:locations`, obtain the owner's approved region, then run:

`npx -y firebase-tools@latest firestore:databases:create (default) --edition=standard --location=<approved-region> --project demo2-c4265`

- [ ] **Step 3: Enable Phone Authentication and register fingerprints**

In Firebase Console enable Authentication > Sign-in method > Phone. Add SHA-1 and SHA-256 values for both debug and release signing keys. Keep keystore passwords private.

- [ ] **Step 4: Fetch safe Android config and add packages**

Run: `npx -y firebase-tools@latest apps:sdkconfig ANDROID 1:167065176851:android:b4fbe23246580cac2ca8e6 --project demo2-c4265`

Place the downloaded `google-services.json` in `mobile/android/app/` and ignore it. Run `cd mobile; flutter pub add firebase_core firebase_auth cloud_firestore`. Run FlutterFire configuration for `demo2-c4265`, creating `mobile/lib/firebase_options.dart`.

- [ ] **Step 5: Write rules, test in emulator, then deploy**

```rules
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    function signedIn() { return request.auth != null; }
    function ownsFarm(id) {
      return signedIn() && get(/databases/$(database)/documents/farms/$(id)).data.ownerId == request.auth.uid;
    }
    match /users/{uid} {
      allow read: if signedIn() && request.auth.uid == uid;
      allow create: if signedIn() && request.auth.uid == uid && request.resource.data.role == 'FARMER';
      allow update: if signedIn() && request.auth.uid == uid && request.resource.data.role == resource.data.role;
      allow delete: if false;
    }
    match /farms/{id} {
      allow create: if signedIn() && request.resource.data.ownerId == request.auth.uid;
      allow read, delete: if ownsFarm(id);
      allow update: if ownsFarm(id) && request.resource.data.ownerId == resource.data.ownerId;
      match /activities/{activityId} { allow read, write: if ownsFarm(id); }
    }
  }
}
```

Run after emulator verification: `npx -y firebase-tools@latest deploy --only firestore:rules --project demo2-c4265`

- [ ] **Step 6: Verify and commit safe files**

Run: `cd mobile; flutter analyze`

Expected: no Gradle or Firebase configuration errors.

```bash
git add firestore.rules mobile/.gitignore mobile/pubspec.yaml mobile/pubspec.lock mobile/android/build.gradle.kts mobile/android/app/build.gradle.kts mobile/lib/firebase_options.dart
git commit -m "chore: configure Firebase Android and Firestore"
```

### Task 2: Implement Firebase Phone Authentication in Flutter

**Files:**
- Create: `mobile/lib/services/firebase_auth_service.dart`
- Modify: `mobile/lib/main.dart`, `mobile/lib/screens/giris_ekrani.dart`, `mobile/lib/services/api_client.dart`
- Test: `mobile/test/firebase_auth_service_test.dart`

**Interfaces:**
- Produces `Future<void> sendCode(String phone)` and `Future<String> confirmCode(String verificationId, String smsCode)`.
- Produces `Future<String?> currentIdToken()` for FastAPI.

- [ ] **Step 1: Write failing service tests**

```dart
test('sends the normalized phone number', () async {
  await service.sendCode('+905551112233');
  expect(fake.phoneNumber, '+905551112233');
});
test('returns an ID token after code confirmation', () async {
  expect(await service.confirmCode('id', '123456'), 'firebase-id-token');
});
```

- [ ] **Step 2: Run and observe failure**

Run: `cd mobile; flutter test test/firebase_auth_service_test.dart`

Expected: FAIL because `FirebaseAuthService` is absent.

- [ ] **Step 3: Add the auth service and initialization**

```dart
await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
```

Use `FirebaseAuth.instance.verifyPhoneNumber` in the production gateway. `confirmCode` signs in with a `PhoneAuthProvider.credential` and returns `user.getIdToken()`. Firebase owns session restoration; do not save its token in SharedPreferences.

- [ ] **Step 4: Replace login routing and API authorization**

Use `FirebaseAuth.instance.authStateChanges()` to select login or dashboard. In `ApiClient`, obtain a fresh `currentUser?.getIdToken()` for each authenticated call:

```dart
headers['Authorization'] = 'Bearer $idToken';
```

Remove the mobile screen's calls to old `/auth/otp/*` endpoints.

- [ ] **Step 5: Verify and commit**

Run: `cd mobile; flutter test test/firebase_auth_service_test.dart; flutter analyze`

Expected: PASS and no analysis errors.

```bash
git add mobile/lib/main.dart mobile/lib/screens/giris_ekrani.dart mobile/lib/services/firebase_auth_service.dart mobile/lib/services/api_client.dart mobile/test/firebase_auth_service_test.dart
git commit -m "feat: sign in with Firebase phone authentication"
```

### Task 3: Make Firestore the farm and activity source

**Files:**
- Create: `mobile/lib/services/firestore_farm_repository.dart`
- Modify: farm/activity screens and `mobile/lib/services/sync_service.dart`
- Test: `mobile/test/firestore_farm_repository_test.dart`

**Interfaces:**
- Produces `Stream<List<Farm>> watchFarms(String uid)`.
- Produces `Future<void> createFarm(Farm farm)` and `Future<void> createActivity(String farmId, Activity activity)`.

- [ ] **Step 1: Write failing repository tests**

```dart
test('sets the Firebase user as farm owner', () async {
  await repository.createFarm(Farm(id: 'f1', name: 'Deneme'));
  expect(fake.document('farms/f1')['ownerId'], 'uid-1');
});
test('stores activity under the parent farm', () async {
  await repository.createActivity('f1', Activity(id: 'a1', description: 'Sulama'));
  expect(fake.exists('farms/f1/activities/a1'), isTrue);
});
```

- [ ] **Step 2: Run and observe failure**

Run: `cd mobile; flutter test test/firestore_farm_repository_test.dart`

Expected: FAIL because `FirestoreFarmRepository` is absent.

- [ ] **Step 3: Implement the stable document contract**

```dart
await firestore.collection('farms').doc(farm.id).set({
  'ownerId': uid, 'name': farm.name, 'latitude': farm.latitude,
  'longitude': farm.longitude, 'sizeInHectares': farm.sizeInHectares,
  'cropType': farm.cropType, 'irrigationMethod': farm.irrigationMethod,
  'plantedAt': farm.plantedAt, 'createdAt': FieldValue.serverTimestamp(),
  'updatedAt': FieldValue.serverTimestamp(),
});
```

Store activities at `farms/{farmId}/activities/{activityId}` using `activityType`, `description`, `occurredAt`, `inputMethod`, and `createdAt`.

- [ ] **Step 4: Switch the screens and offline behavior**

`TarlaListesiEkrani` listens to `watchFarms(uid)`. Add-farm and add-activity screens call the repository. Keep current validation and display permission errors in Turkish. Do not queue new farm/activity work in SQLite; Firestore offline persistence handles new writes. Do not delete legacy SQLite data.

- [ ] **Step 5: Verify and commit**

Run: `cd mobile; flutter test test/firestore_farm_repository_test.dart test/sync_service_test.dart; flutter analyze`

Expected: PASS and no analysis errors.

```bash
git add mobile/lib/services/firestore_farm_repository.dart mobile/lib/services/sync_service.dart mobile/lib/screens mobile/test/firestore_farm_repository_test.dart
git commit -m "feat: store farms and activities in Firestore"
```

### Task 4: Verify Firebase tokens in FastAPI

**Files:**
- Create: `backend/app/firebase_auth.py`, Alembic revision, `backend/tests/test_firebase_auth.py`
- Modify: `backend/app/config.py`, `backend/app/dependencies.py`, `backend/app/models.py`, `backend/tests/conftest.py`

**Interfaces:**
- Produces `verify_firebase_id_token(token: str) -> FirebaseIdentity`.
- Produces a Firebase-aware `get_current_user` dependency.

- [ ] **Step 1: Write failing tests**

```python
def test_identity_maps_to_local_user(client, firebase_token):
    response = client.get('/api/v1/auth/me', headers={'Authorization': f'Bearer {firebase_token}'})
    assert response.status_code == 200
    assert response.json()['firebase_uid'] == 'firebase-uid-1'

def test_invalid_token_is_unauthorized(client):
    response = client.get('/api/v1/auth/me', headers={'Authorization': 'Bearer invalid'})
    assert response.status_code == 401
```

- [ ] **Step 2: Run and observe failure**

Run: `cd backend; pytest tests/test_firebase_auth.py -v`

Expected: FAIL because verification is absent.

- [ ] **Step 3: Add schema and verifier**

Add nullable, unique, indexed `firebase_uid` to `User` and an Alembic revision. Add `firebase_auth_enabled: bool = False` to settings. `firebase_auth.py` creates/reuses its own Firebase Admin app and calls `firebase_admin.auth.verify_id_token(token, check_revoked=True)`. It returns `FirebaseIdentity(uid: str, phone_number: str | None)`.

- [ ] **Step 4: Map a verified identity**

When `firebase_auth_enabled` is true, `get_current_user` verifies the bearer token. Find by UID; at first sign-in create a local `FARMER` using the verified phone number and save UID. Convert expired, revoked, malformed, and wrong-project tokens to the current generic HTTP 401 response. Never assign a role from a request or Firebase custom claim in this phase.

- [ ] **Step 5: Verify and commit**

Run: `cd backend; pytest tests/test_firebase_auth.py tests/test_auth.py -v; ruff check app tests`

Expected: PASS and no Ruff violations.

```bash
git add backend/app/firebase_auth.py backend/app/config.py backend/app/dependencies.py backend/app/models.py backend/alembic backend/tests/test_firebase_auth.py backend/tests/conftest.py
git commit -m "feat: verify Firebase identities in FastAPI"
```

### Task 5: Verify end-to-end flow and document staging setup

**Files:**
- Modify: `README.md`, `backend/.env.example`, `docs/OPENAPI.md`
- Test: complete backend and mobile suites

**Interfaces:**
- Consumes all previous contracts and produces a deployable staging procedure.

- [ ] **Step 1: Document non-secret staging values**

Document `FIREBASE_AUTH_ENABLED=true`, `FIREBASE_PROJECT_ID=demo2-c4265`, `FIREBASE_SERVICE_ACCOUNT_PATH=secrets/firebase-service-account.json`, Phone Auth enablement, Android SHA fingerprints, and Firestore rules deployment. Do not place secret values in docs.

- [ ] **Step 2: Perform a physical-device acceptance test**

Sign in with a test number; create one farm and one activity; enable airplane mode and create one activity; reconnect and confirm it appears exactly once. Confirm `/health/ready` remains healthy and an AI request accepts the Firebase bearer token.

- [ ] **Step 3: Run complete verification**

Run: `cd backend; pytest -q; ruff check app tests`

Run: `cd mobile; flutter test; flutter analyze`

Expected: every command exits 0.

- [ ] **Step 4: Deploy staging and commit docs**

Run: `npx -y firebase-tools@latest deploy --only firestore:rules --project demo2-c4265`

Set the documented non-secret staging configuration, restart the backend service, call `/health/ready`, and sign in from Android against staging.

```bash
git add README.md backend/.env.example docs/OPENAPI.md
git commit -m "docs: describe Firebase mobile setup"
```

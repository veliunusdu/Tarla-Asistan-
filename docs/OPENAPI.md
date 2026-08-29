# Firebase API integration and staging verification

## Authentication contract

When the backend setting `FIREBASE_AUTH_ENABLED=true` is active, protected
ASP.NET Core API routes accept a Firebase Authentication ID token:

```http
Authorization: Bearer <Firebase ID token>
```

The backend verifies the token with Firebase Admin, rejects invalid, expired,
revoked, or wrong-project tokens with `401`, and creates or reuses a local
`FARMER` record from the verified Firebase UID. The mobile application requests
the current valid Firebase token for each protected API call; Firebase may
reuse its cached token until it needs renewal. Firebase verification service or
configuration failures return the backend's generic `503`. During first-time
local account mapping, a verified-phone collision can return the safe `409`
response.

The .NET backend retains the OTP endpoints for compatibility, while the new
mobile application uses Firebase Phone Authentication. Protected-route Bearer
tokens are validated by the configured authentication service.

`POST /api/v1/ai/chat` is a protected route and is the staging check for a
Firebase bearer token. `GET /health/ready` is public and verifies PostgreSQL
and Redis only; it does not validate Firebase connectivity or credentials.

## Firebase staging configuration

The target is Firebase project `demo2-c4265` and the named Enterprise Firestore
database `tarla-asistani`. Configure a backend process started from `backend/`
with these non-secret values:

```env
FIREBASE_AUTH_ENABLED=true
FIREBASE_PROJECT_ID=demo2-c4265
FIREBASE_SERVICE_ACCOUNT_PATH=secrets/firebase-service-account.json
```

The service-account JSON must be provisioned outside version control. In Docker
Compose, mount it read-only and use its container path
`/run/secrets/firebase-service-account.json` instead. Never add service-account
JSON, environment files, Firebase client configuration files, bearer tokens,
API keys, or real telephone numbers to documentation or Git.

`compose.staging.yaml` is an override, not a standalone Compose project. An
authorized operator supplies an untracked secure environment file and merges
the base file first. Use `up` with forced recreation rather than `restart`,
because a restart does not apply changed container environment values:

```powershell
docker compose --env-file <secure-staging-env> -f compose.yaml -f compose.staging.yaml up -d --build --force-recreate backend
```

An administrator must enable Firebase Phone Authentication and add SHA-1 and
SHA-256 fingerprints for both debug and release Android signing keys for
`com.tarlaasistani.pilot`. The Android client configuration is provisioned
separately and remains untracked.

## Rules and index release gate

The repository configures the Enterprise database name and location in
`firebase.json`; `firestore.rules` and `firestore.indexes.json` are the release
artifacts. Validate rules locally first:

```powershell
cd firebase-emulator-tests
npm run test:emulator
```

Only a project-authorized operator may publish both remote rules and indexes:

```powershell
npx -y firebase-tools@latest deploy --only firestore:rules,firestore:indexes --project demo2-c4265
```

## Staging acceptance gate

1. Set the non-secret values above, provision the service account securely, and
   recreate the backend with the merged Compose command above.
2. Confirm `GET /health/ready` is healthy.
3. On a physical Android device, complete Firebase Phone Authentication with an
   approved test account.
4. Create one farm and one activity. Switch the device offline, create one more
   activity, reconnect, and confirm it appears exactly once.
5. Send a protected AI request with the current Firebase bearer token and
   confirm the backend accepts it.

This procedure intentionally does not include real credentials, device actions,
remote deployment, database migration, or production publishing.

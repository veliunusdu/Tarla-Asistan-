# Tarla Asistanı Mobile

Flutter farmer application for the Sprint 5 pilot. It supports OTP login,
server-backed farms, offline activity capture, automatic retry, FCM token
registration and task/case/weather notification routing.

## Local run

The Android emulator reaches the local API through `10.0.2.2`:

```powershell
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8000/api/v1
```

Debug builds intentionally allow cleartext traffic. Pilot/release builds must use
an HTTPS API URL.

## Firebase Cloud Messaging

Create an Android Firebase app with package name `com.tarlaasistani.pilot`, then
pass its public client configuration as compile-time values:

```powershell
flutter run `
  --dart-define=API_BASE_URL=https://staging-api.example.com/api/v1 `
  --dart-define=FIREBASE_API_KEY=... `
  --dart-define=FIREBASE_APP_ID=... `
  --dart-define=FIREBASE_MESSAGING_SENDER_ID=... `
  --dart-define=FIREBASE_PROJECT_ID=... `
  --dart-define=FIREBASE_STORAGE_BUCKET=...
```

When configuration is absent, the app remains usable and push is reported as
unavailable. Notification permission denial does not block login. Authorized
tokens are registered with `POST /api/v1/notifications/devices`; token refreshes
are registered again. Taps accept only the `tarla-asistani://` scheme and open the
matching task, case or weather screen.

## Offline and low-connection behavior

Activities are committed to SQLite together with one UUID-based sync operation.
The queue retries in creation order when connectivity returns. Timeouts, 429 and
5xx responses remain queued; the backend's `client_operation_id` makes a replay
idempotent. Connectivity state is only a retry trigger—every network request still
handles timeouts and transport failures.

Run the automated weak-connection, deep-link and large-text tests with:

```powershell
flutter test
```

## Signed pilot package

1. Copy `android/key.properties.example` to `android/key.properties` and point it
   to a securely stored pilot keystore. Never commit the keystore or passwords.
2. Set `API_BASE_URL` and the Firebase variables in the shell.
3. From the repository root run `./scripts/build-pilot.ps1`.

The signed bundle is created at `mobile/build/app/outputs/bundle/release/app-release.aab`.
Use a dedicated internal-testing track and retain the mapping between build number,
commit and pilot cohort.

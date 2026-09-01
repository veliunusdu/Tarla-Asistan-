# Mobile Production Flow Design

## Goal

Make the Flutter application a production-ready farmer flow where every
connected service has an understandable user interface, and where .NET API
and Supabase are the only source of persistent application data.

## Decisions

- Firebase Authentication remains the identity provider for registration and
  sign-in.
- The .NET API owns application users, profiles, farms, activities, weather,
  AI conversations, media metadata, and notification-device registration.
- Supabase PostgreSQL remains the API's production database. The mobile app
  never connects to Supabase directly.
- Firestore is removed from mandatory sign-in and farm workflows. Existing
  Firestore helper code may remain temporarily while it has no production
  call path.
- Firebase Cloud Messaging continues to provide device tokens and permission
  handling. A token-registration failure must never block a signed-in user.
- R2 media and AI chat remain backend-mediated; the mobile app does not hold
  provider credentials.

## User Flow

1. A user registers or signs in through Firebase Auth.
2. The app exchanges the Firebase ID token with `POST /api/v1/auth/firebase`.
3. The backend creates or resolves the farmer account in Supabase and returns
   backend access and refresh tokens.
4. The app starts local sync and notification registration as best-effort
   tasks. Their failure is visible in settings but does not prevent entry to
   the application.
5. The signed-in farmer sees the home dashboard, farm list, journal, AI
   assistant, and a persistent profile/settings entry point.

## Profile And Session UI

The profile/settings screen is reachable from the application shell. It
shows the authenticated user's name, email or phone, role, notification
permission state, and backend connectivity state.

- Update profile uses `PUT /api/v1/users/me`.
- Sign out first asks the backend to revoke the refresh token, removes local
  tokens, deactivates the registered notification device when possible, then
  signs out Firebase.
- Account deletion remains an explicit, confirmed request through
  `POST /api/v1/users/me/deletion-request`.
- A failed profile refresh or notification registration is displayed as a
  recoverable status, never as a full-screen login failure.

## Farm And Location UI

All farm screens use the backend repository stack rather than defaulting to
SQLite repositories.

- The farm list reads `GET /api/v1/farms`, opens details, and refreshes after
  any mutation.
- Add farm creates through `POST /api/v1/farms`.
- Detail exposes edit and archive actions. Archive is a confirmation-gated
  soft delete through `DELETE /api/v1/farms/{id}`.
- Edit uses `PATCH /api/v1/farms/{id}` and preserves fields not changed by the
  user.
- Add and edit place the location section directly after the farm name. It
  offers device location, OpenStreetMap selection, coordinate summary,
  change, and remove actions.
- A farmer may save a locationless farm. Locationless farms show a clear
  `Konum ekle` action wherever weather is requested.

## Weather UI

The dashboard weather card requests weather for a selected farm through the
backend. It shows the farm name with the reading.

- When no farm exists, it offers `Tarla Ekle`.
- When a farm exists but has no coordinates, it explains that weather requires
  a farm location and opens that farm's location editor.
- Provider, network, and authorization failures retain a retry action and
  show a service-specific message instead of a generic connection error.

## Service-To-UI Mapping

| Service | Mobile UI responsibility |
| --- | --- |
| Firebase Auth | Registration, sign-in, identity and sign-out |
| .NET API + Supabase | Profile, farms, activities, weather, AI and durable data |
| OpenStreetMap + device location | Farm location selection and display |
| Weather provider through API | Farm-scoped dashboard forecast and risks |
| DeepSeek through API | AI assistant replies and image analysis |
| Cloudflare R2 through API | Media upload and rendered media references |
| Firebase Cloud Messaging | Notification permission state and device registration |

## Error Handling

- Authentication exchange failure keeps the user on the login screen with the
  backend error message when safe to display.
- Noncritical post-login failures are collected as settings statuses and do
  not block access to the application.
- Farm, location, and weather errors identify the failed action and include a
  retry path.
- Destructive farm archival and account deletion require confirmation.

## Testing And Acceptance

- Each repository and service change is developed test-first.
- Widget tests cover profile access, sign-out, farm edit/archive confirmation,
  location-required weather guidance, and retry states.
- Repository tests verify the exact backend HTTP method, endpoint, and JSON
  payload for profile and farm mutations.
- A manual device pass verifies: register, sign in, view profile, sign out,
  add a farm with device/map location, see weather, edit a farm, archive a
  farm, send an AI message, and accept or decline notifications.

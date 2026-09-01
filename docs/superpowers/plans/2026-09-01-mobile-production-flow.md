# Mobile Production Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a mobile farmer flow where Firebase identity, .NET API, Supabase-backed data, location, weather, AI, R2 media, and notifications are all represented by working UI.

**Architecture:** Firebase Auth produces identity tokens only. `AuthService` exchanges them for backend tokens and all durable mobile data is read and mutated through `ApiClient` and the .NET API. Post-login sync and FCM registration become best-effort work, while settings exposes their current state. Farm screens receive a shared backend repository instead of constructing local SQLite defaults.

**Tech Stack:** Flutter, Firebase Auth, Firebase Cloud Messaging, .NET Minimal API, Supabase PostgreSQL, OpenStreetMap, geolocator, Cloudflare R2, DeepSeek.

**Spec:** `docs/superpowers/specs/2026-09-01-mobile-production-flow-design.md`

## Global Constraints

- Work only in `feature/mobile-location-web-readiness`; do not modify `main`.
- Firebase Auth is identity only; Firestore cannot block sign-in or persist production farm data.
- The mobile client connects only to the .NET API for application data; Supabase credentials never enter the app.
- All behavioral changes use Flutter test-first development and record a red test run before implementation.
- Archive and account deletion require explicit user confirmation.
- Never send `0.0, 0.0` as a missing farm location.

---

### Task 1: Make Post-Login Initialization Nonblocking

**Files:**
- Modify: `mobile/lib/services/post_login_initializer.dart`
- Modify: `mobile/lib/main.dart`
- Modify: `mobile/test/post_login_initializer_test.dart`
- Modify: `mobile/test/main_test.dart`

**Interfaces:**
- Consumes: `UserProfileProvisioner.ensureProfile`, `SyncService.initialize`, `NotificationService.initializeAfterLogin`.
- Produces: `PostLoginInitializationStatus` with independent profile, sync, and notification failures, plus `Future<void> initialize(...)` that only fails when backend authentication fails.

- [ ] **Step 1: Write the failing initializer test**

```dart
test('continues to sync when the optional profile mirror fails', () async {
  final events = <String>[];
  final initializer = PostLoginInitializer(
    profileProvisioner: _ThrowingProfileProvisioner(),
    initializeSync: () async => events.add('sync'),
    initializeNotifications: () async => events.add('notifications'),
  );

  await initializer.initialize(uid: 'uid-1', phoneNumber: null, email: 'a@b.com');

  expect(events, ['sync', 'notifications']);
  expect(initializer.status.profileFailure, isNotNull);
});
```

- [ ] **Step 2: Run the test to verify RED**

Run: `flutter test test/post_login_initializer_test.dart --name "optional profile mirror fails"`

Expected: FAIL because `initialize` propagates the profile failure and no status API exists.

- [ ] **Step 3: Implement the minimal status model and best-effort profile call**

```dart
class PostLoginInitializationStatus {
  const PostLoginInitializationStatus({this.profileFailure, this.syncFailure, this.notificationFailure});
  final Object? profileFailure;
  final Object? syncFailure;
  final Object? notificationFailure;
}
```

Record profile failures and continue with sync/notifications. In `main.dart`, only the Firebase-to-backend token exchange stays a fatal sign-in prerequisite.

- [ ] **Step 4: Add the application-level regression test**

```dart
testWidgets('shows the authenticated shell when optional post-login setup fails', (tester) async {
  await tester.pumpWidget(testAppWithAuthenticatedUser());
  await tester.pumpAndSettle();
  expect(find.byType(AnaEkran), findsOneWidget);
});
```

- [ ] **Step 5: Verify GREEN**

Run: `flutter test test/post_login_initializer_test.dart test/main_test.dart`

Expected: PASS; a valid Firebase/backend session reaches the shell even if Firestore or FCM setup fails.

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/services/post_login_initializer.dart mobile/lib/main.dart mobile/test/post_login_initializer_test.dart mobile/test/main_test.dart
git commit -m "fix: keep mobile login independent of firestore"
```

### Task 2: Add Backend User Profile And Session Client

**Files:**
- Create: `mobile/lib/features/profile/data/backend_profile_repository.dart`
- Create: `mobile/lib/features/profile/domain/user_profile.dart`
- Modify: `mobile/lib/services/auth_service.dart`
- Modify: `mobile/test/services/auth_service_test.dart`
- Create: `mobile/test/features/profile/data/backend_profile_repository_test.dart`

**Interfaces:**
- Consumes: `ApiClient.getJson`, `ApiClient.putJson`, and stored refresh tokens.
- Produces: `BackendProfileRepository.getCurrentProfile()`, `updateProfile(UserProfileUpdate)`, `requestDeletion(String confirmation)`, and `AuthService.logout()`.

- [ ] **Step 1: Write the failing repository test**

```dart
test('loads the backend profile from auth/me', () async {
  final client = RecordingApiClient.get('/auth/me', response: _userJson);
  final profile = await BackendProfileRepository(client).getCurrentProfile();
  expect(profile.fullName, 'Ayşe Demir');
  expect(client.requestedPath, '/auth/me');
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/features/profile/data/backend_profile_repository_test.dart --name "loads the backend profile"`

Expected: FAIL because `BackendProfileRepository` does not exist.

- [ ] **Step 3: Implement profile DTO mapping and authenticated mutations**

```dart
Future<UserProfile> getCurrentProfile() async =>
    UserProfile.fromJson(await _client.getJson('/auth/me'));

Future<UserProfile> updateProfile(UserProfileUpdate update) async =>
    UserProfile.fromJson(await _client.putJson('/users/me', update.toJson()));
```

- [ ] **Step 4: Write and run logout RED test**

```dart
test('revokes the backend refresh token before clearing the local session', () async {
  final service = AuthService(client: RecordingClient.noContent('/auth/logout'));
  await service.saveTestSession(accessToken: 'access', refreshToken: 'refresh');
  await service.logout();
  expect(service.client.requestBody['refresh_token'], 'refresh');
  expect(await service.currentAccessToken(), isNull);
});
```

Run: `flutter test test/services/auth_service_test.dart --name "revokes the backend refresh token"`

Expected: FAIL because `logout` does not exist.

- [ ] **Step 5: Implement logout and verify GREEN**

Run: `flutter test test/services/auth_service_test.dart test/features/profile/data/backend_profile_repository_test.dart`

Expected: PASS; logout calls `/auth/logout`, clears local session on success or safe failure, and never logs tokens.

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/features/profile mobile/lib/services/auth_service.dart mobile/test/services/auth_service_test.dart mobile/test/features/profile
git commit -m "feat: add backend profile and logout client"
```

### Task 3: Build Profile, Settings, And Logout UI

**Files:**
- Create: `mobile/lib/screens/profil_ekrani.dart`
- Modify: `mobile/lib/screens/ana_ekran.dart`
- Modify: `mobile/lib/main.dart`
- Modify: `mobile/lib/services/notification_service.dart`
- Create: `mobile/test/screens/profil_ekrani_test.dart`
- Modify: `mobile/test/screens/ana_ekran_test.dart`

**Interfaces:**
- Consumes: `BackendProfileRepository`, `AuthService.logout`, `FirebaseAuthService.signOut`, and `NotificationService.state`.
- Produces: profile UI reachable from the main shell with edit, notification status, logout, and deletion request confirmation.

- [ ] **Step 1: Write the failing shell navigation test**

```dart
testWidgets('opens Profile from the main shell', (tester) async {
  await tester.pumpWidget(buildAnaEkran());
  await tester.tap(find.byTooltip('Profil ve ayarlar'));
  await tester.pumpAndSettle();
  expect(find.text('Profil ve Ayarlar'), findsOneWidget);
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/screens/ana_ekran_test.dart --name "opens Profile"`

Expected: FAIL because the shell has no profile entry point.

- [ ] **Step 3: Implement the profile screen and account action coordinator**

```dart
Future<void> _logout() async {
  await _authService.logout();
  await _notificationService.deactivateCurrentDevice();
  await _firebaseAuthService.signOut();
}
```

Render loading, recoverable profile load failure, role, identity, notification status, editable profile fields, confirmed deletion request, and confirmed logout.

- [ ] **Step 4: Write profile behavior tests**

```dart
testWidgets('asks before submitting an account deletion request', (tester) async {
  await tester.pumpWidget(buildProfileScreen());
  await tester.tap(find.text('Hesap silme talebi'));
  await tester.pumpAndSettle();
  expect(find.text('Bu işlem geri alınamaz.'), findsOneWidget);
});
```

- [ ] **Step 5: Verify GREEN**

Run: `flutter test test/screens/profil_ekrani_test.dart test/screens/ana_ekran_test.dart`

Expected: PASS; users can see service status and safely end their session.

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/screens/profil_ekrani.dart mobile/lib/screens/ana_ekran.dart mobile/lib/main.dart mobile/lib/services/notification_service.dart mobile/test/screens/profil_ekrani_test.dart mobile/test/screens/ana_ekran_test.dart
git commit -m "feat: add mobile profile and session controls"
```

### Task 4: Complete Backend Farm Repository Mutations

**Files:**
- Modify: `mobile/lib/features/fields/data/tarla_repository.dart`
- Modify: `mobile/lib/features/fields/data/backend_tarla_repository.dart`
- Modify: `mobile/lib/features/fields/data/local_tarla_repository.dart`
- Modify: `mobile/test/features/fields/data/backend_tarla_repository_test.dart`
- Modify: `mobile/test/tarla_ekleme_ekrani_test.dart`

**Interfaces:**
- Consumes: `FarmRemoteRepository.updateFarm(String, FarmUpdateRequestDto)` and `archiveFarm(String)`.
- Produces: `TarlaRepository.updateTarla(Tarla)` and `TarlaRepository.archiveTarla(String)`.

- [ ] **Step 1: Write the failing backend mutation test**

```dart
test('archives a farm through the backend remote repository', () async {
  final remote = RecordingFarmRemoteRepository();
  await BackendTarlaRepository(remote: remote).archiveTarla('farm-1');
  expect(remote.archivedFarmId, 'farm-1');
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/features/fields/data/backend_tarla_repository_test.dart --name "archives a farm"`

Expected: FAIL because `archiveTarla` is not part of `TarlaRepository`.

- [ ] **Step 3: Implement update/archive in both repositories**

```dart
abstract interface class TarlaRepository implements TarlaReadRepository {
  Future<void> addTarla(Tarla tarla);
  Future<void> updateTarla(Tarla tarla);
  Future<void> archiveTarla(String id);
}
```

For backend updates, send a sparse `FarmUpdateRequestDto`. For local data, preserve test compatibility by updating or removing only the local row.

- [ ] **Step 4: Verify GREEN**

Run: `flutter test test/features/fields/data/backend_tarla_repository_test.dart test/tarla_ekleme_ekrani_test.dart`

Expected: PASS; create, edit, location update, and archive use the repository contract.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/fields/data mobile/test/features/fields/data/backend_tarla_repository_test.dart mobile/test/tarla_ekleme_ekrani_test.dart
git commit -m "feat: support mobile farm update and archive"
```

### Task 5: Wire The Backend Farm Repository Through The Mobile Shell

**Files:**
- Modify: `mobile/lib/main.dart`
- Modify: `mobile/lib/screens/ana_ekran.dart`
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_listesi_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_gunlugu_ekrani.dart`
- Modify: `mobile/test/main_test.dart`
- Modify: `mobile/test/screens/ana_ekran_test.dart`

**Interfaces:**
- Consumes: one `BackendFarmRepository(apiClient: _apiClient)` and one `BackendTarlaRepository(remote: ...)` per authenticated app shell.
- Produces: every farm list, add form, location editor, and dashboard receives the same authenticated `TarlaRepository` instance.

- [ ] **Step 1: Write the failing shell injection test**

```dart
testWidgets('passes the authenticated farm repository to the farm tab', (tester) async {
  final repository = RecordingTarlaRepository();
  await tester.pumpWidget(buildAnaEkran(tarlaRepository: repository));
  await tester.tap(find.text('Tarlalarım'));
  await tester.pumpAndSettle();
  expect(repository.reads, 1);
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/screens/ana_ekran_test.dart --name "authenticated farm repository"`

Expected: FAIL because pushed screens construct `LocalTarlaRepository` defaults.

- [ ] **Step 3: Pass repositories through all constructors and navigation**

```dart
return AnaEkran(
  tarlaRepository: BackendTarlaRepository(
    remote: BackendFarmRepository(apiClient: _apiClient),
  ),
  weatherRepository: BackendWeatherRepository(apiClient: _apiClient),
  aiRepository: BackendAiAssistantRepository(apiClient: _apiClient),
);
```

- [ ] **Step 4: Verify GREEN**

Run: `flutter test test/main_test.dart test/screens/ana_ekran_test.dart test/screens/tarla_listesi_ekrani_test.dart`

Expected: PASS; authenticated screens no longer silently fall back to SQLite.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/main.dart mobile/lib/screens/ana_ekran.dart mobile/lib/screens/ana_sayfa_ekrani.dart mobile/lib/screens/tarla_listesi_ekrani.dart mobile/lib/screens/tarla_gunlugu_ekrani.dart mobile/test/main_test.dart mobile/test/screens
git commit -m "fix: use backend farms throughout mobile shell"
```

### Task 6: Add Farm Edit, Archive, And Visible Location Controls

**Files:**
- Modify: `mobile/lib/screens/tarla_ekleme_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_detay_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_listesi_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_konum_duzenleme_ekrani.dart`
- Modify: `mobile/test/screens/tarla_detay_ekrani_test.dart`
- Modify: `mobile/test/screens/tarla_listesi_ekrani_test.dart`
- Modify: `mobile/test/screens/tarla_ekleme_ekrani_test.dart`

**Interfaces:**
- Consumes: `TarlaRepository.updateTarla`, `archiveTarla`, `TarlaLocationRepository.updateTarlaLocation`, and `FieldLocationPickerScreen`.
- Produces: edit/archival actions from farm detail and list, plus location actions in add and edit forms.

- [ ] **Step 1: Write the failing archive confirmation test**

```dart
testWidgets('archives only after the user confirms', (tester) async {
  final repository = RecordingTarlaRepository();
  await tester.pumpWidget(buildTarlaDetay(repository: repository));
  await tester.tap(find.byTooltip('Tarlayı arşivle'));
  await tester.pumpAndSettle();
  expect(find.text('Tarlayı arşivle?'), findsOneWidget);
  await tester.tap(find.text('Arşivle'));
  await tester.pumpAndSettle();
  expect(repository.archivedIds, ['farm-1']);
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/screens/tarla_detay_ekrani_test.dart --name "archives only"`

Expected: FAIL because no farm archive UI exists.

- [ ] **Step 3: Implement farm action UI**

Use an app-bar overflow menu for `Düzenle` and `Tarlayı arşivle`. Open the existing form in edit mode, prefilled from the selected farm. Send updates through the injected repository and return `true` so list and dashboard reload.

- [ ] **Step 4: Write the visible location control test**

```dart
testWidgets('shows location controls after the farm name in edit mode', (tester) async {
  await tester.pumpWidget(buildFarmForm(editing: _farmWithoutLocation));
  expect(find.text('Tarla konumu'), findsOneWidget);
  expect(find.text('Konumumu kullan'), findsOneWidget);
  expect(find.text('Haritada seç'), findsOneWidget);
});
```

- [ ] **Step 5: Verify GREEN**

Run: `flutter test test/screens/tarla_detay_ekrani_test.dart test/screens/tarla_listesi_ekrani_test.dart test/screens/tarla_ekleme_ekrani_test.dart test/screens/tarla_konum_duzenleme_ekrani_test.dart`

Expected: PASS; users can edit, archive with confirmation, set, change, or remove coordinates.

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/screens/tarla_ekleme_ekrani.dart mobile/lib/screens/tarla_detay_ekrani.dart mobile/lib/screens/tarla_listesi_ekrani.dart mobile/lib/screens/tarla_konum_duzenleme_ekrani.dart mobile/test/screens
git commit -m "feat: complete mobile farm management controls"
```

### Task 7: Make Weather Farm-Scoped And Location-Guided

**Files:**
- Modify: `mobile/lib/features/weather/data/backend_weather_repository.dart`
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Modify: `mobile/test/features/weather/data/backend_weather_repository_test.dart`
- Modify: `mobile/test/screens/ana_sayfa_ekrani_test.dart`

**Interfaces:**
- Consumes: `TarlaRepository.getTarlalar`, `BackendWeatherRepository.getWeather`, and `TarlaKonumDuzenlemeEkrani`.
- Produces: weather card state for no farms, locationless farms, successful farm forecast, and retryable provider/API failure.

- [ ] **Step 1: Write the failing no-location widget test**

```dart
testWidgets('opens the selected locationless farm when weather needs coordinates', (tester) async {
  await tester.pumpWidget(buildHome(tarlalar: [_farmWithoutLocation]));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Konum ekle'));
  await tester.pumpAndSettle();
  expect(find.text('Deneme Tarlası'), findsOneWidget);
  expect(find.text('Konumumu kullan'), findsOneWidget);
});
```

- [ ] **Step 2: Run RED**

Run: `flutter test test/screens/ana_sayfa_ekrani_test.dart --name "weather needs coordinates"`

Expected: FAIL if the weather error remains generic or routes to add-farm.

- [ ] **Step 3: Implement weather selection and service-specific errors**

```dart
on WeatherLocationRequiredException {
  return WeatherLocationRequiredCard(
    farm: firstFarmWithoutLocation,
    onAddLocation: () => _konumEkle(firstFarmWithoutLocation),
  );
}
```

Keep the selected farm name beside weather data. Preserve safe backend provider error messages for retryable failures.

- [ ] **Step 4: Verify GREEN**

Run: `flutter test test/features/weather/data/backend_weather_repository_test.dart test/screens/ana_sayfa_ekrani_test.dart`

Expected: PASS; a farmer can always see the exact next action needed to obtain weather.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/weather/data/backend_weather_repository.dart mobile/lib/screens/ana_sayfa_ekrani.dart mobile/test/features/weather/data/backend_weather_repository_test.dart mobile/test/screens/ana_sayfa_ekrani_test.dart
git commit -m "fix: guide weather users through farm location"
```

### Task 8: Verify Services On A Physical Android Device

**Files:**
- Modify: `README.md`
- Modify: `mobile/README.md` if it exists

**Interfaces:**
- Consumes: deployed Render API, Firebase project `demo2-c4265`, Supabase production database, R2, DeepSeek, weather provider, and a connected Android device.
- Produces: reproducible build/install/test instructions and evidence for each production service.

- [ ] **Step 1: Add the manual acceptance checklist**

```markdown
1. Register a new email/password farmer account.
2. Confirm mobile opens the dashboard even if notifications are declined.
3. Open Profile, edit profile, sign out, and sign in again.
4. Add a farm using device location and confirm its weather card loads.
5. Edit the farm, change map coordinates, and confirm weather refreshes.
6. Archive the farm and confirm it disappears from active farms.
7. Send a text and photo to the AI assistant.
8. Confirm media upload, AI response, and notification-device status.
```

- [ ] **Step 2: Run the full automated suite**

Run: `flutter test`

Expected: PASS with no failing test.

- [ ] **Step 3: Build and install the debug APK**

Run: `flutter build apk --debug`

Run: `C:\\Users\\veliu\\AppData\\Local\\Android\\Sdk\\platform-tools\\adb.exe install -r build\\app\\outputs\\flutter-apk\\app-debug.apk`

Expected: installation succeeds on the connected device.

- [ ] **Step 4: Execute the manual checklist and record failures by service**

Expected: every failure identifies Firebase Auth, API/Supabase, location, weather, AI/R2, or FCM rather than a generic connection message.

- [ ] **Step 5: Commit documentation**

```bash
git add README.md mobile/README.md
git commit -m "docs: add mobile production test checklist"
```


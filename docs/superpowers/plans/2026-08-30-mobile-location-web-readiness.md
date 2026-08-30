# Mobile Location and Web Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all add-field routes persist location-ready backend fields and make web registration pilot-safe.

**Architecture:** `TarlaEklemeEkrani` remains the one location capture screen, receiving its repository from every navigation source. Web remains an agronomist console; farmer registration is a Firebase account-creation bridge to mobile, not a farmer dashboard.

**Tech Stack:** Flutter/Dart, `geolocator`, `flutter_map`, .NET 8, Firebase Auth, Next.js 16, TypeScript.

**Spec:** `docs/superpowers/specs/2026-08-30-mobile-location-web-readiness-design.md`

## Global Constraints

- Keep foreground-only location permissions and OpenStreetMap attribution.
- Do not send `0.0, 0.0` for missing locations.
- Preserve `AGRONOMIST` authorization on `/dashboard`.
- Do not create a full farmer web portal in this release.
- Use a failing test before each behavior change.

---

### Task 1: Propagate the Field Repository Through Every Mobile Route

**Files:**
- Modify: `mobile/lib/screens/tarla_gunlugu_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_listesi_ekrani.dart`
- Test: `mobile/test/screens/tarla_gunlugu_ekrani_test.dart`
- Test: `mobile/test/screens/tarla_listesi_ekrani_test.dart`

**Interfaces:**
- Consumes: the parent screen's `TarlaRepository`.
- Produces: every `TarlaEklemeEkrani` gets `repository: widget._tarlaRepo` or `repository: widget._repository`.

- [ ] **Step 1: Write the failing journal-empty-state test.**

```dart
testWidgets('Günlüğümden eklenen tarla supplied repositoryye kaydolur', (tester) async {
  final farms = RecordingTarlaRepository();
  await tester.pumpWidget(MaterialApp(home: TarlaGunluguEkrani(tarlaRepository: farms)));
  await tester.tap(find.text('Tarla Ekle'));
  await completeFarmForm(tester);
  expect(farms.added, hasLength(1));
});
```

- [ ] **Step 2: Run `flutter test test/screens/tarla_gunlugu_ekrani_test.dart --name "Günlüğümden eklenen tarla"`.**

Expected: FAIL because `TarlaGunluguEkrani` opens `const TarlaEklemeEkrani()` and uses its local default.

- [ ] **Step 3: Replace the journal route with the injected repository.**

```dart
builder: (_) => TarlaEklemeEkrani(repository: widget._tarlaRepo),
```

- [ ] **Step 4: Add matching tests for the field-list empty state and floating action button.**

```dart
await completeFarmForm(tester);
expect(farms.added.single.name, 'Kuzey Tarla');
```

- [ ] **Step 5: Run `flutter test test/screens/tarla_gunlugu_ekrani_test.dart test/screens/tarla_listesi_ekrani_test.dart`.**

Expected: PASS.

- [ ] **Step 6: Commit with `fix: persist farms from every mobile route`.**

### Task 2: Make Location Capture Visible Before Save

**Files:**
- Modify: `mobile/lib/screens/tarla_ekleme_ekrani.dart`
- Test: `mobile/test/screens/tarla_ekleme_ekrani_test.dart`
- Test: `mobile/test/features/location/presentation/field_location_picker_screen_test.dart`

**Interfaces:**
- Consumes: `LocationService.getCurrentLocation()` and `FieldLocationPickerScreen`.
- Produces: a `Tarla konumu` section immediately after the field-name input.

- [ ] **Step 1: Write the failing location visibility test.**

```dart
expect(find.text('Tarla konumu'), findsOneWidget);
expect(find.text('Konumumu kullan'), findsOneWidget);
expect(find.text('Haritada seç'), findsOneWidget);
expect(find.text('Konumsuz devam et'), findsOneWidget);
```

- [ ] **Step 2: Run `flutter test test/screens/tarla_ekleme_ekrani_test.dart --name "Tarla konumu"`.**

Expected: FAIL because the current controls are an unlabeled block near the bottom of the scroll view.

- [ ] **Step 3: Replace the block with a labelled `InputDecorator`.**

```dart
InputDecorator(
  decoration: const InputDecoration(labelText: 'Tarla konumu'),
  child: Column(children: [Text(locationLabel), Wrap(children: locationActions)]),
)
```

Place this section after the name field and before the size, crop and date fields.

- [ ] **Step 4: Keep missing location explicit but valid.**

```dart
await completeFarmForm(tester);
await tester.tap(find.text('Konumsuz devam et'));
expect(repository.added.single.latitude, isNull);
expect(repository.added.single.longitude, isNull);
```

`Konumsuz devam et` must use the same required-field validation as Save; it cannot bypass name, area, crop or date validation.

- [ ] **Step 5: Run `flutter test test/screens/tarla_ekleme_ekrani_test.dart test/features/location/presentation/field_location_picker_screen_test.dart`.**

Expected: PASS.

- [ ] **Step 6: Commit with `feat: make farm location capture discoverable`.**

### Task 3: Let Weather Update an Existing Locationless Field

**Files:**
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Create: `mobile/lib/screens/tarla_konum_duzenleme_ekrani.dart`
- Modify: `mobile/lib/features/fields/data/tarla_repository.dart`
- Modify: `mobile/lib/features/fields/data/backend_tarla_repository.dart`
- Modify: `mobile/lib/features/fields/data/local_tarla_repository.dart`
- Test: `mobile/test/screens/ana_sayfa_ekrani_test.dart`
- Test: `mobile/test/screens/tarla_konum_duzenleme_ekrani_test.dart`

**Interfaces:**
- Consumes: `WeatherLocationRequiredException`, `Tarla`, and `TarlaLocation`.
- Produces: `Future<void> updateTarlaLocation(String id, TarlaLocation location)`.

- [ ] **Step 1: Write the failing weather-state test.**

```dart
expect(find.text('Hava durumu için tarla konumu ekleyin'), findsOneWidget);
await tester.tap(find.text('Konum ekle'));
expect(find.byType(TarlaKonumDuzenlemeEkrani), findsOneWidget);
```

- [ ] **Step 2: Run `flutter test test/screens/ana_sayfa_ekrani_test.dart --name "Konum ekle"`.**

Expected: FAIL because the current action is `Tarla Ekle`, which can create a duplicate.

- [ ] **Step 3: Add a focused location-update interface and backend PATCH mapping.**

```dart
abstract interface class TarlaLocationRepository {
  Future<void> updateTarlaLocation(String id, TarlaLocation location);
}
```

`BackendTarlaRepository` must PATCH both coordinates to `/farms/{id}`. `LocalTarlaRepository` must update the matching record for offline parity.

- [ ] **Step 4: Implement `TarlaKonumDuzenlemeEkrani` with the existing GPS and map picker services.**

```dart
class TarlaKonumDuzenlemeEkrani extends StatefulWidget {
  const TarlaKonumDuzenlemeEkrani({required this.tarla, required this.repository, super.key});
}
```

- [ ] **Step 5: After a successful update, return true and refresh the weather future.**

Run: `flutter test test/screens/ana_sayfa_ekrani_test.dart test/screens/tarla_konum_duzenleme_ekrani_test.dart`

Expected: PASS; the original field ID changes location rather than another field being created.

- [ ] **Step 6: Commit with `feat: guide weather users to add a farm location`.**

### Task 4: Harden Web Registration and Role Communication

**Files:**
- Modify: `web/app/register/page.tsx`
- Modify: `web/app/login/page.tsx`
- Modify: `web/app/farmer/page.tsx`
- Modify: `web/.env.example`
- Modify: `web/package.json`
- Create: `web/vitest.config.ts`
- Create: `docs/release/agronomist-provisioning.md`
- Test: `web/app/register/page.test.tsx`
- Test: `web/lib/firebase.test.ts`

**Interfaces:**
- Consumes: Firebase `createUserWithEmailAndPassword`, `updateProfile`, `getIdToken(true)` and `loginWithFirebase`.
- Produces: a profile-safe `FARMER` registration and role-accurate copy.

- [ ] **Step 1: Write a failing registration test.**

```tsx
expect(mockGetIdToken).toHaveBeenCalledWith(true);
expect(mockLoginWithFirebase).toHaveBeenCalledWith('fresh-id-token');
```

- [ ] **Step 2: Add a web test runner and run the test.**

Add `vitest`, `jsdom`, `@testing-library/react`, and `@testing-library/jest-dom`; add `"test": "vitest run"` to `package.json`; configure the `@/*` alias in `vitest.config.ts`.

Run: `pnpm test -- register`

Expected: FAIL because registration currently calls `getIdToken()` without `true`.

- [ ] **Step 3: Force-refresh after updating the display name.**

```ts
if (name.trim()) await updateProfile(credential.user, { displayName: name.trim() });
const session = await loginWithFirebase(await credential.user.getIdToken(true));
```

- [ ] **Step 4: Change login and farmer copy.**

```tsx
Çiftçi hesabı oluşturmak ve mobil uygulamada kullanmak için kayıt olun.
```

The farmer page must explicitly be a mobile handoff, never a dashboard claim.

- [ ] **Step 5: Document agronomist provisioning without granting roles in the browser.**

```markdown
1. Create or locate the intended agronomist user record.
2. Obtain that person's Firebase UID after their Firebase sign-in.
3. Use the protected operator approval process to create an unexpired Firebase-link approval.
4. Have the agronomist sign in once; verify the approval is consumed and `/dashboard` opens.
```

The runbook must state that a normal `/register` account cannot become agronomist by changing browser data or API request parameters.

- [ ] **Step 6: Remove empty Firebase assignments from `.env.example`.**

```dotenv
# Leave Firebase variables unset to use lib/firebase.ts development defaults.
```

- [ ] **Step 7: Run `pnpm test && pnpm lint && pnpm build`.**

Expected: PASS.

- [ ] **Step 8: Commit with `fix: harden web farmer registration`.**

### Task 5: Run and Record Pilot Acceptance

**Files:**
- Create: `docs/release/field-location-and-web-registration-acceptance.md`

**Interfaces:**
- Consumes: deployed Render URLs, a new Firebase email account, and Android location permissions.
- Produces: a dated acceptance record.

- [ ] **Step 1: Record preconditions.**

```markdown
- Render web and API are Live for the same main commit.
- Firebase Email/Password is enabled.
- Android location is enabled and the test app is freshly installed.
```

- [ ] **Step 2: Run registration and mobile sign-in.**

```markdown
1. Create a unique account at `/register`.
2. Verify the farmer handoff screen.
3. Sign in to Android with the same credentials.
```

- [ ] **Step 3: Run field and weather checks.**

```markdown
1. Add fields from Home, Fields and Journal empty states using separate test accounts.
2. Use GPS and map selection; save; restart; confirm persistence.
3. Confirm weather returns for the coordinate-backed field.
4. Create a locationless field and confirm the location action appears.
```

- [ ] **Step 4: Run regressions.**

```markdown
1. Text AI returns a provider response.
2. An agronomist can enter `/dashboard`.
3. A farmer cannot enter `/dashboard`.
4. Agronomist provisioning consumes an approved Firebase link exactly once.
5. No secret appears in app logs, browser console, screenshots or Git status.
```

- [ ] **Step 5: Run `flutter test`, `dotnet test --no-restore`, and `pnpm test && pnpm lint && pnpm build`; add their results to the document.**

- [ ] **Step 6: Commit with `docs: add pilot acceptance checklist`.**

## Coverage Review

- Task 1 fixes the local-only field creation path.
- Task 2 makes the existing location features discoverable.
- Task 3 lets weather users repair locationless fields without duplicates.
- Task 4 covers registration correctness, local configuration and role language.
- Task 5 proves the deployed web, mobile, Firebase, API, weather and AI flows.

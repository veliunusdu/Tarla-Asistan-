# Mobile Case Listing & Agronomist Two-Way Messaging Implementation Plan

> **Goal:** Enable farmers to list their field problem cases, inspect case status/history, and engage in two-way interactive messaging (chat) with agronomists (sending replies and photos).
> **Spec Reference:** `docs/superpowers/specs/2026-09-03-mobile-case-messaging-and-list-design.md`

---

## Proposed Changes & File Layout

### Domain (`mobile/lib/features/cases/domain/models/`)
- `case_status.dart`: Enum for case status (`open`, `inReview`, `waitingFarmer`, `answered`, `closed`) with display name and backend mapping.
- `case_summary.dart`: Case list model (`id`, `farmId`, `farmName`, `category`, `status`, `title`, `messageCount`, `mediaCount`, `createdAt`, `updatedAt`).
- `case_message.dart`: Chat message model with sender info, `isFromExpert`, `isCurrentUser`, text body, attached media URLs.
- `case_detail.dart`: Complete case view model with header, initial media, and chronological message list.

### Data (`mobile/lib/features/cases/data/`)
- `case_repository.dart`: Extend interface with `getCases({farmId, status})`, `getCaseById(caseId)`, and `sendMessage(caseId, ...)`.
- `backend_case_repository.dart`: Implement calls to `/api/v1/cases`, `/api/v1/cases/{id}`, `/api/v1/cases/{id}/messages`, and multipart media upload.

### Presentation (`mobile/lib/features/cases/presentation/`)
- `vaka_listesi_ekrani.dart`: Screen displaying filter tabs (All, Active, Closed), status badges, case summary cards, and quick create FAB.
- `vaka_detay_ekrani.dart`: Interactive chat screen with expert/farmer speech bubbles, collapsible case complaint header, bottom text input + photo attachment bar, and closed-case lock banner.

### Integrations (`mobile/lib/screens/`)
- `tarla_detay_ekrani.dart`: Add "Bildirimleri Görüntüle" button in field details.
- `profil_ekrani.dart`: Add "Sorun Bildirimlerim & Uzman Sohbetleri" menu action.
- `notification_target_screen.dart`: Deep-link handler to immediately navigate to `VakaDetayEkrani` on case update push notifications.
- `ana_ekran.dart`: Forward `caseRepository` to profile and navigation targets.

---

## Tasks

### Task 1: Domain Models (`CaseStatus`, `CaseSummary`, `CaseMessage`, `CaseDetail`)

**Files:**
- Create: `mobile/lib/features/cases/domain/models/case_status.dart`
- Create: `mobile/lib/features/cases/domain/models/case_summary.dart`
- Create: `mobile/lib/features/cases/domain/models/case_message.dart`
- Create: `mobile/lib/features/cases/domain/models/case_detail.dart`
- Test: `mobile/test/features/cases/domain/models/case_models_test.dart`

#### Step 1: Write failing test
Create `mobile/test/features/cases/domain/models/case_models_test.dart`:
```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';

void main() {
  group('CaseStatus', () {
    test('parses from backend string and provides display name', () {
      expect(CaseStatus.fromString('Open'), CaseStatus.open);
      expect(CaseStatus.fromString('InReview'), CaseStatus.inReview);
      expect(CaseStatus.fromString('WaitingFarmer'), CaseStatus.waitingFarmer);
      expect(CaseStatus.fromString('Answered'), CaseStatus.answered);
      expect(CaseStatus.fromString('Closed'), CaseStatus.closed);

      expect(CaseStatus.waitingFarmer.displayName, 'Bilgi Bekliyor');
      expect(CaseStatus.waitingFarmer.backendValue, 'WaitingFarmer');
    });
  });

  group('CaseMessage', () {
    test('correctly identifies expert vs farmer messages', () {
      final expertMsg = CaseMessage(
        id: 'msg-1',
        caseId: 'case-1',
        senderId: 'expert-1',
        senderName: 'Dr. Ayşe',
        messageType: CaseMessageType.expertResponse,
        body: 'İlaçlama tavsiyem ektedir.',
        createdAt: DateTime.now(),
      );

      final farmerMsg = CaseMessage(
        id: 'msg-2',
        caseId: 'case-1',
        senderId: 'farmer-1',
        senderName: 'Mehmet Çiftçi',
        messageType: CaseMessageType.comment,
        body: 'Teşekkürler, uyguladım.',
        createdAt: DateTime.now(),
        isCurrentUser: true,
      );

      expect(expertMsg.isFromExpert, isTrue);
      expect(expertMsg.isCurrentUser, isFalse);
      expect(farmerMsg.isFromExpert, isFalse);
      expect(farmerMsg.isCurrentUser, isTrue);
    });
  });

  group('CaseSummary and CaseDetail', () {
    test('instantiates with expected properties', () {
      final now = DateTime.now();
      final summary = CaseSummary(
        id: 'c-1',
        farmId: 'f-1',
        farmName: 'Zeytinlik',
        category: CaseCategory.pest,
        status: CaseStatus.waitingFarmer,
        title: 'Zeytin Sineği',
        createdAt: now,
        updatedAt: now,
        messageCount: 3,
        mediaCount: 1,
      );

      expect(summary.title, 'Zeytin Sineği');
      expect(summary.messageCount, 3);
      expect(summary.status, CaseStatus.waitingFarmer);

      final detail = CaseDetail(
        id: 'c-1',
        farmId: 'f-1',
        farmName: 'Zeytinlik',
        category: CaseCategory.pest,
        status: CaseStatus.waitingFarmer,
        title: 'Zeytin Sineği',
        description: 'Tuzaklarda sinek sayısı arttı.',
        createdAt: now,
        initialMediaUrls: ['https://example.com/sinegi.jpg'],
        messages: [],
      );

      expect(detail.description, 'Tuzaklarda sinek sayısı arttı.');
      expect(detail.initialMediaUrls.length, 1);
    });
  });
}
```

#### Step 2: Run test to verify failure
`cd mobile && flutter test test/features/cases/domain/models/case_models_test.dart`

#### Step 3: Implement domain models
Create `case_status.dart`, `case_summary.dart`, `case_message.dart`, and `case_detail.dart` as defined in the design spec.

#### Step 4: Run test to verify pass
`cd mobile && flutter test test/features/cases/domain/models/case_models_test.dart`

#### Step 5: Commit
`git commit -m "feat(cases): add CaseStatus, CaseSummary, CaseMessage, and CaseDetail domain models"`

---

### Task 2: Data Layer Updates in `CaseRepository` and `BackendCaseRepository`

**Files:**
- Modify: `mobile/lib/features/cases/data/case_repository.dart`
- Modify: `mobile/lib/features/cases/data/backend_case_repository.dart`
- Create: `mobile/test/features/cases/data/backend_case_repository_list_and_chat_test.dart`

#### Step 1: Write failing test
Create `mobile/test/features/cases/data/backend_case_repository_list_and_chat_test.dart` asserting:
1. `getCases()` returns parsed list of `CaseSummary`.
2. `getCases(farmId: '...')` appends `farmId` query param.
3. `getCaseById(caseId)` returns `CaseDetail` with messages.
4. `sendMessage(caseId, ...)` posts to `/cases/{id}/messages` and uploads image to `/media` if provided.

#### Step 2: Run test to verify failure
`cd mobile && flutter test test/features/cases/data/backend_case_repository_list_and_chat_test.dart`

#### Step 3: Implement repository methods
In `case_repository.dart` and `backend_case_repository.dart`:
- Add `getCases`: calls `_api.getJson('/cases', queryParameters: ...)`, maps response `items` to `CaseSummary`.
- Add `getCaseById`: calls `_api.getJson('/cases/$caseId')`, maps response to `CaseDetail`.
- Add `sendMessage`: uploads media if present, calls `_api.postJson('/cases/$caseId/messages', ...)`, maps response to `CaseMessage`.

#### Step 4: Run test to verify pass
`cd mobile && flutter test test/features/cases/data/backend_case_repository_list_and_chat_test.dart`

#### Step 5: Commit
`git commit -m "feat(cases): implement getCases, getCaseById, and sendMessage in BackendCaseRepository"`

---

### Task 3: Presentation - Case List Screen (`VakaListesiEkrani`)

**Files:**
- Create: `mobile/lib/features/cases/presentation/vaka_listesi_ekrani.dart`
- Create: `mobile/test/features/cases/presentation/vaka_listesi_ekrani_test.dart`

#### Step 1: Write failing widget test
Create `mobile/test/features/cases/presentation/vaka_listesi_ekrani_test.dart`:
- Verifies loading state and empty state.
- Verifies rendering of case list cards with status badge colors and farm names.
- Verifies filter chips (Tümü, Aktifler, Çözülenler).
- Verifies clicking a card navigates to `VakaDetayEkrani`.
- Verifies FAB opens `SorunBildirEkrani`.

#### Step 2: Run test to verify failure
`cd mobile && flutter test test/features/cases/presentation/vaka_listesi_ekrani_test.dart`

#### Step 3: Implement `vaka_listesi_ekrani.dart`
- Add `VakaListesiEkrani` with `farmId` filter option, pull-to-refresh, status badges, and FAB.

#### Step 4: Run test to verify pass
`cd mobile && flutter test test/features/cases/presentation/vaka_listesi_ekrani_test.dart`

#### Step 5: Commit
`git commit -m "feat(cases): implement VakaListesiEkrani"`

---

### Task 4: Presentation - Interactive Chat Screen (`VakaDetayEkrani`)

**Files:**
- Create: `mobile/lib/features/cases/presentation/vaka_detay_ekrani.dart`
- Create: `mobile/test/features/cases/presentation/vaka_detay_ekrani_test.dart`

#### Step 1: Write failing widget test
Create `mobile/test/features/cases/presentation/vaka_detay_ekrani_test.dart`:
- Renders case summary banner (title, status, initial description).
- Renders expert bubble on the left with badge and agronomist name.
- Renders farmer bubble on the right.
- Enters text in input field, taps send, verifies `repository.sendMessage` was called and new message appears.
- Verifies input bar is locked with informative message when case status is `closed`.

#### Step 2: Run test to verify failure
`cd mobile && flutter test test/features/cases/presentation/vaka_detay_ekrani_test.dart`

#### Step 3: Implement `vaka_detay_ekrani.dart`
- Add `VakaDetayEkrani` with chat timeline, auto-scroll, image picker bottom sheet, sending indicator, and closed case disabled state.

#### Step 4: Run test to verify pass
`cd mobile && flutter test test/features/cases/presentation/vaka_detay_ekrani_test.dart`

#### Step 5: Commit
`git commit -m "feat(cases): implement VakaDetayEkrani interactive chat screen"`

---

### Task 5: Entry Points, Deep Linking, and Navigation Integration

**Files:**
- Modify: `mobile/lib/screens/tarla_detay_ekrani.dart`
- Modify: `mobile/lib/screens/profil_ekrani.dart`
- Modify: `mobile/lib/screens/notification_target_screen.dart`
- Modify: `mobile/lib/screens/ana_ekran.dart`
- Create: `mobile/test/screens/case_navigation_entry_test.dart`

#### Step 1: Write failing test
Create `mobile/test/screens/case_navigation_entry_test.dart`:
- Tests "Vakalar" button on `TarlaDetayEkrani` opens `VakaListesiEkrani` scoped to that farm.
- Tests `NotificationTargetScreen` forwards to `VakaDetayEkrani` when target is a case.
- Tests `ProfilEkrani` contains "Sorun Bildirimlerim" leading to `VakaListesiEkrani`.

#### Step 2: Run test to verify failure
`cd mobile && flutter test test/screens/case_navigation_entry_test.dart`

#### Step 3: Integrate entry points
- In `TarlaDetayEkrani`: add "Bildirimleri Gör" button next to "Sorun Bildir".
- In `ProfilEkrani`: add list tile for "Sorun Bildirimlerim & Uzman Sohbetleri".
- In `NotificationTargetScreen`: route to `VakaDetayEkrani` for case deep-links.
- In `AnaEkran`: pass `caseRepository` to `ProfilEkrani`.

#### Step 4: Run test to verify pass
`cd mobile && flutter test test/screens/case_navigation_entry_test.dart`
`cd mobile && flutter test test/features/cases/`

#### Step 5: Commit
`git commit -m "feat(cases): wire case listing and chat navigation into TarlaDetay, Profil, and NotificationTargetScreen"`

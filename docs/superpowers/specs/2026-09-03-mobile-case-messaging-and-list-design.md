# Mobile Case Listing & Agronomist Two-Way Messaging Design Specification

- **Date:** 2026-09-03
- **Feature:** Mobile Case Listing & Two-Way Agronomist Messaging ("Vakalarım & Uzman Sohbeti")
- **Author:** Pair Programming Session (Antigravity & User)
- **Status:** Approved

---

## 1. Problem Statement & Context

In Step 1, mobile case creation was implemented, allowing farmers to report field problems with title, description, category, and photos directly to the backend (`POST /api/v1/cases`).

However, two critical capabilities remain missing on mobile:
1. **Case Listing:** Farmers cannot browse their open or resolved cases, track the status of investigations, or see case histories.
2. **Two-Way Messaging / Chat:** When an agronomist responds with recommendations or requests additional information (`CaseMessageType.AdditionalInfoRequest`), the mobile app has only a read-only screen (`NotificationTargetScreen`) and lacks the ability to reply, ask follow-up questions, or send supplemental photos.

This specification defines the architecture, data models, interactive conversation UI, and navigation entry points for Case Listing and Two-Way Messaging.

---

## 2. Architecture & Data Contracts

### 2.1 Backend Contract Review

The backend exposes the following existing endpoints:
- `GET /api/v1/cases`
  - Query parameters: `farmId` (UUID optional), `status` (string optional), `limit`, `offset`
  - Returns: `{ "items": [ CaseSummaryDto ], "total": int }`
  - CaseSummaryDto fields: `id`, `farm_id`, `farm_name`, `category`, `priority`, `status`, `title`, `created_at_utc`, `updated_at_utc`, `message_count`, `media_count`.
- `GET /api/v1/cases/{id}`
  - Returns `CaseDetailDto`:
    - `id`, `farm_id`, `farm_name`, `category`, `priority`, `status`, `title`, `description`, `media` (list of `{ id, url, original_name }`), `messages` (list of `CaseMessageDto`), `created_at_utc`.
  - CaseMessageDto fields:
    - `id`, `case_id`, `sender_id`, `sender_name`, `message_type` (`Comment` | `AdditionalInfoRequest` | `ExpertResponse`), `body`, `media` (list of `{ id, url }`), `created_at_utc`.
- `POST /api/v1/cases/{id}/messages`
  - JSON Body: `{ "body": "<string>", "message_type": "Comment", "media_ids": ["<uuid>"], "client_operation_id": "<uuid>" }`
  - Returns: `CaseMessageDto`
  - **Backend Side-Effect:** If case status was `WaitingFarmer`, receiving a message from the farmer automatically transitions the case status back to `InReview` and updates `updated_at_utc`.

---

### 2.2 Mobile Domain Models (`mobile/lib/features/cases/domain/models/`)

#### 1. `CaseStatus`
```dart
enum CaseStatus {
  open,
  inReview,
  waitingFarmer,
  answered,
  closed;

  String get displayName => switch (this) {
    CaseStatus.open => 'Yeni',
    CaseStatus.inReview => 'İnceleniyor',
    CaseStatus.waitingFarmer => 'Bilgi Bekliyor',
    CaseStatus.answered => 'Yanıtlandı',
    CaseStatus.closed => 'Çözüldü / Kapalı',
  };

  String get backendValue => switch (this) {
    CaseStatus.open => 'Open',
    CaseStatus.inReview => 'InReview',
    CaseStatus.waitingFarmer => 'WaitingFarmer',
    CaseStatus.answered => 'Answered',
    CaseStatus.closed => 'Closed',
  };

  static CaseStatus fromString(String? value) => switch (value?.toLowerCase()) {
    'open' => CaseStatus.open,
    'inreview' => CaseStatus.inReview,
    'waitingfarmer' => CaseStatus.waitingFarmer,
    'answered' => CaseStatus.answered,
    'closed' => CaseStatus.closed,
    _ => CaseStatus.open,
  };
}
```

#### 2. `CaseSummary`
```dart
class CaseSummary {
  const CaseSummary({
    required this.id,
    required this.farmId,
    required this.farmName,
    required this.category,
    required this.status,
    required this.title,
    required this.createdAt,
    required this.updatedAt,
    required this.messageCount,
    required this.mediaCount,
  });

  final String id;
  final String farmId;
  final String farmName;
  final CaseCategory category;
  final CaseStatus status;
  final String title;
  final DateTime createdAt;
  final DateTime updatedAt;
  final int messageCount;
  final int mediaCount;
}
```

#### 3. `CaseMessage`
```dart
enum CaseMessageType {
  comment,
  additionalInfoRequest,
  expertResponse;

  static CaseMessageType fromString(String? val) => switch (val?.toLowerCase()) {
    'additionalinforequest' => CaseMessageType.additionalInfoRequest,
    'expertresponse' => CaseMessageType.expertResponse,
    _ => CaseMessageType.comment,
  };
}

class CaseMessage {
  const CaseMessage({
    required this.id,
    required this.caseId,
    required this.senderId,
    required this.senderName,
    required this.messageType,
    required this.body,
    this.mediaUrls = const [],
    required this.createdAt,
    this.isCurrentUser = false,
  });

  final String id;
  final String caseId;
  final String senderId;
  final String senderName;
  final CaseMessageType messageType;
  final String body;
  final List<String> mediaUrls;
  final DateTime createdAt;
  final bool isCurrentUser;

  bool get isFromExpert =>
      messageType == CaseMessageType.expertResponse ||
      messageType == CaseMessageType.additionalInfoRequest;
}
```

#### 4. `CaseDetail`
```dart
class CaseDetail {
  const CaseDetail({
    required this.id,
    required this.farmId,
    required this.farmName,
    required this.category,
    required this.status,
    required this.title,
    required this.description,
    this.initialMediaUrls = const [],
    this.messages = const [],
    required this.createdAt,
  });

  final String id;
  final String farmId;
  final String farmName;
  final CaseCategory category;
  final CaseStatus status;
  final String title;
  final String description;
  final List<String> initialMediaUrls;
  final List<CaseMessage> messages;
  final DateTime createdAt;
}
```

---

### 2.3 Data Layer Updates (`mobile/lib/features/cases/data/`)

Update `CaseRepository` interface:
```dart
abstract interface class CaseRepository {
  Future<String> createCase(CreateCaseInput input);
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status});
  Future<CaseDetail> getCaseById(String caseId);
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  });
}
```

Implementation in `BackendCaseRepository`:
- `getCases({farmId, status})`: Calls `GET /cases` with query parameters. Parses and returns `List<CaseSummary>`.
- `getCaseById(caseId)`: Calls `GET /cases/{caseId}`. Sets `isCurrentUser` based on sender name / ID comparison. Parses `CaseDetail` and its messages.
- `sendMessage(caseId, ...)`:
  - If `imageBytes != null`: uploads via `postMultipart('/media')`, verifies non-empty `mediaId`.
  - Submits `POST /cases/{caseId}/messages` with payload `{ "body": body, "message_type": "Comment", "media_ids": mediaIds, "client_operation_id": uuid.v4() }`.
  - Returns created `CaseMessage`.

---

## 3. User Interface & Screen Architecture

### 3.1 `VakaListesiEkrani` (`mobile/lib/features/cases/presentation/vaka_listesi_ekrani.dart`)
- **Header:**
  - If opened from field: `"[Tarla Adı] Bildirimleri"`
  - If opened globally: `"Sorun Bildirimlerim"`
- **Filter Tabs:**
  - `Tümü`, `Aktifler` (Open, InReview, WaitingFarmer, Answered), `Çözülenler` (Closed).
- **List Items (Card):**
  - Category icon & title.
  - Farm name chip.
  - Status badge with semantic colors:
    - `WaitingFarmer`: Red/Error ("⚠️ Yanıt Bekleniyor")
    - `InReview`: Orange/Warning ("İnceleniyor")
    - `Open`: Blue/Primary ("Yeni")
    - `Answered`: Green/Success ("Uzman Yanıtladı")
    - `Closed`: Grey ("Kapalı")
  - Last updated relative date & message counter badge.
  - On tap: Navigates to `VakaDetayEkrani`.
- **Floating Action Button:**
  - `+ Sorun Bildir` -> navigates to `SorunBildirEkrani`.

---

### 3.2 `VakaDetayEkrani` (`mobile/lib/features/cases/presentation/vaka_detay_ekrani.dart`)
- **Header Section (Collapsible / Banner):**
  - Farm name, Case Title, Status badge.
  - Initial complaint description and initial photo thumbnail(s) preview.
- **Interactive Conversation Timeline (ListView):**
  - Chronological message bubbles:
    - **Agronomist (Left Bubble):**
      - Accent background (`AppColors.primaryLight`).
      - Sender name ("Ziraat Mühendisi").
      - Special badge for `AdditionalInfoRequest` ("⚠️ Ek Bilgi Talebi") or `ExpertResponse` ("✅ Ziraat Mühendisi Tavsiyesi").
      - Message text and optional attached media.
    - **Farmer (Right Bubble):**
      - Surface background (`Colors.white` / card shadow).
      - Text body and attached photo thumbnail.
- **Bottom Input Bar:**
  - When status != `closed`:
    - Photo picker button (`Icons.camera_alt`). Shows small thumbnail preview above input when selected.
    - `TextField`: "Uzmana yanıt yazın...".
    - Send button: Submits message, appends directly to list, and scrolls to bottom.
  - When status == `closed`:
    - Disabled banner: *"🔒 Bu sorun bildiriminiz çözülerek kapatılmıştır."*

---

## 4. Entry Points & Deep Linking

1. **`TarlaDetayEkrani`:**
   - Add button: `"Bildirimleri Görüntüle"` alongside `"Sorun Bildir"` in `_TarlaBilgiKarti`.
2. **`ProfilEkrani`:**
   - Add menu entry: `"Sorun Bildirimlerim & Uzman Sohbetleri"`.
3. **`AnaSayfaEkrani`:**
   - Action item or banner when cases need attention.
4. **`NotificationTargetScreen`:**
   - When a push notification arrives with `type == 'case'` or `case_update`, immediately route or display `VakaDetayEkrani` so the farmer can reply on the spot.

---

## 5. Error Handling & Edge Cases

- **Offline / Network Failures:**
  - If `sendMessage` fails, preserve the text in the `TextField`, show a friendly `SnackBar` ("Mesaj iletilemedi. Lütfen bağlantınızı kontrol edip tekrar deneyin.").
- **Media Upload Failures:**
  - Throw before submitting `/messages` to avoid sending a half-empty message.
- **Closed Cases:**
  - Prevent user from submitting new comments to a closed case.

---

## 6. Testing Strategy

1. **Domain Tests:**
   - `test/features/cases/domain/models/case_status_test.dart`
   - `test/features/cases/domain/models/case_summary_test.dart`
   - `test/features/cases/domain/models/case_message_test.dart`
   - `test/features/cases/domain/models/case_detail_test.dart`
2. **Data Layer Tests:**
   - `test/features/cases/data/backend_case_repository_list_and_chat_test.dart`
   - Test `getCases()` without and with `farmId` filter.
   - Test `getCaseById()` correctly mapping expert vs farmer messages.
   - Test `sendMessage()` with and without media upload.
3. **Widget Tests:**
   - `test/features/cases/presentation/vaka_listesi_ekrani_test.dart` (filtering, card rendering, navigation).
   - `test/features/cases/presentation/vaka_detay_ekrani_test.dart` (chat rendering, sending message, closed state lock).
   - `test/screens/tarla_detay_case_list_entry_test.dart` (entry point tests).

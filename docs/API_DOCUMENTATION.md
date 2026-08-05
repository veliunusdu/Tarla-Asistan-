# API DOCUMENTATION

## 1. General Information
- **Base URL:** `https://api.tarla-asistani.com/api/v1` (for MVP)
- **Content-Type:** `application/json`
- **Authentication:** Bearer Token (JWT)

## 2. Authentication (Auth)

### 2.1 Request OTP (Farmer)
`POST /auth/request-otp`
```json
// Request
{
  "phone_number": "+905551234567"
}

// Response (200 OK)
{
  "message": "OTP code has been sent."
}
```

### 2.2 Verify OTP
`POST /auth/verify-otp`
```json
// Request
{
  "phone_number": "+905551234567",
  "otp_code": "123456"
}

// Response (200 OK)
{
  "access_token": "eyJhbGciOiJIUzI1NiIsIn...",
  "refresh_token": "opaque-random-token",
  "token_type": "bearer",
  "expires_in": 3600,
  "user": {
    "id": "uuid",
    "role": "FARMER"
  }
}
```

### 2.3 Refresh Session
`POST /auth/refresh`
```json
{
  "refresh_token": "opaque-random-token"
}
```
- Rotates the refresh token. A used, expired, or revoked token returns `401`.

### 2.4 Logout
`POST /auth/logout`
```json
{
  "refresh_token": "opaque-random-token"
}
```
- Revokes the refresh session and returns `204 No Content`.

### 2.5 Current User
`GET /auth/me`
- **Auth:** `Bearer Token`

### 2.6 Complete Profile
`PUT /users/me`
```json
{
  "full_name": "Veli Ünüşdü",
  "province": "Konya",
  "district": "Selçuklu",
  "terms_accepted": true,
  "notifications_enabled": true
}
```

## 3. Farm Management (Farms)

### 3.1 List Farms
`GET /farms?include_archived=false&limit=50&offset=0`
- **Auth:** `Bearer Token`
- **Response:** Kullanıcıya ait tarlaları `items`, `total`, `limit` ve `offset`
  alanlarıyla döndürür.

Ownership is checked server-side. A farm that does not belong to the active user
is returned as `404` so its existence is not disclosed.

### 3.2 Add Farm
`POST /farms`
```json
{
  "name": "Kuzey Tarlası",
  "latitude": 38.7312,
  "longitude": 35.4787,
  "size_in_hectares": 12.5,
  "irrigation_method": "DRIP",
  "soil_type": "Killi tın",
  "note": "Kuzey girişini kullan.",
  "crop_type": "WHEAT",
  "variety": "Bezostaja",
  "planted_at": "2026-03-10"
}
```
- Konum, ürün ve geçmişte veya bugünde olan ekim tarihi zorunludur.
- Aynı ada sahip aktif tarla engellenmez; `warnings` alanında kullanıcıya
  kontrol uyarısı verilir.

### 3.3 Get and Update Farm
- `GET /farms/{farm_id}`
- `PATCH /farms/{farm_id}`

Her iki uç da yalnızca aktif kullanıcının kendi tarlasına erişmesine izin verir.

### 3.4 Archive Farm
`DELETE /farms/{farm_id}`

Kaydı silmez; `archived_at` alanını doldurur ve `204` döndürür. Arşivlenmiş
tarlalar varsayılan listede ve tekil görüntülemede yer almaz.

### 3.5 Production Periods
- `GET /farms/{farm_id}/production-periods`
- `POST /farms/{farm_id}/production-periods`
- `POST /farms/{farm_id}/production-periods/{period_id}/close`

Desteklenen ürünler: `WHEAT`, `BARLEY`, `CORN`, `SUNFLOWER`, `TOMATO`.
Bir tarlada aynı anda yalnızca bir aktif dönem olabilir. Yeni dönem sırasında
aktif ürün varsa API `409` döndürür; kullanıcı açıkça `close_existing=true`
gönderirse önceki dönem kapanır ve geçmişte korunur.

### 3.6 Farm Weather and Risks
`GET /farms/{farm_id}/weather`

Saatlik sıcaklık, yağış olasılığı, yağış miktarı ve rüzgâr hızını; son güncelleme,
sağlayıcı ve risk listesiyle birlikte döndürür. Sağlayıcı kesintisinde son
başarılı kayıt varsa `is_stale=true` olarak gösterilir, yoksa `503` döner.
Risk önerileri bilgilendirme amaçlıdır ve saha kontrolünün yerini almaz.

## 4. Tasks (Tasks)

### 4.1 Get Daily Tasks
`GET /farms/{farm_id}/tasks?date=YYYY-MM-DD`
- **Auth:** Tarla sahibi
- Gün için en fazla üç öncelikli işi `items` içinde döndürür.
- Kritik hava görevleri üç iş sınırına dahil edilmez ve
  `critical_weather_alerts` içinde ayrıca döndürülür.
- Açık durumdaki geçmiş tarihli işler `overdue` içinde gösterilir.
- Günlük kural ve hava görevleri aynı tarla/tarih için tekrar üretilmez.

### 4.2 Create Expert Task
`POST /farms/{farm_id}/tasks`
```json
{
  "title": "Yaprakları kontrol edin",
  "description": "Alt yapraklarda lekelenme olup olmadığını kontrol edin.",
  "reason": "Uzman saha takibi için önerdi.",
  "priority": "HIGH",
  "confidence": "HIGH",
  "due_date": "2026-07-20",
  "crop_period_id": "uuid"
}
```
- **Auth:** Yalnızca `AGRONOMIST`
- Aynı tarla, tarih ve içerikteki ikinci görev `409` döndürür.
- `crop_period_id` verilmezse tarlanın aktif üretim dönemi kullanılır.

### 4.3 Get and Update Task
- `GET /tasks/{task_id}`
- `PATCH /tasks/{task_id}/status`

Çiftçi `VIEWED`, `PLANNED`, `COMPLETED` veya `NOT_APPLIED` durumuna
geçebilir. `NOT_APPLIED` için `not_applied_reason` zorunludur. Uzman yalnızca
kendi rolüne uygun olarak görevi `CANCELLED` durumuna getirebilir.

### 4.4 Complete Task
`POST /tasks/{task_id}/complete`
- İsteğe bağlı `note` ve `photo_url` kabul eder.
- Görev tamamlanır ve tarla günlüğünde tek kayıt olacak şekilde saklanır.
- Aynı tamamlama isteğinin tekrarı yeni faaliyet üretmez.

## 5. Activities and Farm Journal

### 5.1 Create and List Activities
- `POST /farms/{farm_id}/activities`
- `GET /farms/{farm_id}/activities?include_drafts=true&include_archived=false`

Desteklenen faaliyetler: `IRRIGATION`, `FERTILIZATION`, `SPRAYING`,
`PRUNING`, `FIELD_CHECK`, `HARVEST`, `OTHER`.

Manuel kayıtlar doğrudan `CONFIRMED` olur. `input_method=VOICE` ile gönderilen
kayıt, ses adresi veya döküm içermeli ve kullanıcı onaylayana kadar `DRAFT`
olarak kalmalıdır.

### 5.2 Confirm and Maintain Activities
- `POST /activities/{activity_id}/confirm`
- `PATCH /activities/{activity_id}`
- `GET /activities/{activity_id}/revisions`
- `DELETE /activities/{activity_id}`
- `POST /activities/{activity_id}/restore`

Güncellemeden önce değişen alanların eski değerleri revizyon geçmişine yazılır.
Silme işlemi kalıcı silme yerine arşivlemedir.

### 5.3 Farm Journal
`GET /farms/{farm_id}/journal?limit=50&offset=0`

Doğrulanmış faaliyetleri ve sonlandırılmış görevleri tek bir ters kronolojik
akışta döndürür. Sesli taslaklar kullanıcı onayına kadar günlüğe girmez.

## 6. Media and Cases

### 6.1 Upload Media
`POST /media`

- **Content-Type:** `multipart/form-data`
- `file` alanında JPG, PNG, WEBP veya desteklenen ses dosyası gönderilir.
- Varsayılan üst sınır 15 MB'dır.
- Dönen `/api/v1/media/{media_id}/content` adresi yalnızca yetkili kullanıcıların
  Bearer token ile erişebildiği güvenli bir adrestir.

### 6.2 Create Case
`POST /cases`
```json
{
  "client_operation_id": "11111111-1111-1111-1111-111111111111",
  "farm_id": "00000000-0000-0000-0000-000000000000",
  "category": "DISEASE",
  "title": "Yapraklarda sararma",
  "description": "Alt yapraklarda hızla yayılan sarı lekeler var.",
  "media_ids": ["00000000-0000-0000-0000-000000000001"]
}
```

`client_operation_id`, çevrimdışı kuyruğun aynı isteği yeniden göndermesi
durumunda ikinci bir kayıt oluşmasını engeller. Aynı kimlik farklı içerikle
kullanılırsa `409` döner. Bu destek faaliyet oluşturma ve vaka mesajlarında da
geçerlidir.

### 6.3 List and Detail

- `GET /cases?status=OPEN&priority=HIGH&farm_id={farm_id}&limit=50&offset=0`
- `GET /cases/{case_id}`

Çiftçi yalnızca kendi tarlalarındaki vakaları; uzman ise öncelik, durum, çiftçi
ve tarla bağlamıyla tüm vakaları görür.

### 6.4 Status and Priority
`PATCH /cases/{case_id}/status`
```json
{
  "status": "IN_REVIEW",
  "priority": "CRITICAL",
  "assign_to_me": true
}
```

### 6.5 Messages and Additional Information
`POST /cases/{case_id}/messages`
```json
{
  "client_operation_id": "22222222-2222-2222-2222-222222222222",
  "message_type": "ADDITIONAL_INFO_REQUEST",
  "body": "Yaprağın alt yüzünün fotoğrafını ekler misiniz?",
  "media_ids": []
}
```

Mesaj türleri: `COMMENT`, `ADDITIONAL_INFO_REQUEST`, `EXPERT_RESPONSE`.
Ek bilgi isteği vakayı `WAITING_FARMER` durumuna getirir; çiftçinin cevabı
vakayı yeniden `IN_REVIEW` yapar.

### 6.6 Expert Response and Close
`POST /cases/{case_id}/expert-response`
```json
{
  "client_operation_id": "33333333-3333-3333-3333-333333333333",
  "body": "Saha kontrolü öneriyorum; yaprakları bu süre içinde kuru tutun.",
  "media_ids": [],
  "close_case": true
}
```

`close_case=false` vakayı `ANSWERED`, `true` ise `CLOSED` durumuna getirir.

## 7. Notifications

Uzman görevi, kritik hava görevi ve uzman cevabı olayları uygulama içi bildirim
oluşturur. Etkin cihaz varsa push gönderilir; cihaz daha sonra kaydolursa bekleyen
bildirim gönderilir. Her olay `dedupe_key` ile yalnızca bir kez kaydedilir.

### 7.1 Register Device
`POST /notifications/devices`
```json
{
  "token": "device-provider-token-at-least-16-characters",
  "platform": "ANDROID"
}
```

Response `201`:
```json
{
  "id": "uuid",
  "user_id": "uuid",
  "platform": "ANDROID",
  "active": true,
  "last_seen_at": "2026-08-05T10:00:00Z",
  "created_at": "2026-08-05T10:00:00Z"
}
```

### 7.2 Notification Inbox

- `GET /notifications?unread_only=false&limit=50&offset=0`
- `POST /notifications/{notification_id}/read`
- `DELETE /notifications/devices/{device_id}`

Liste response örneği:
```json
{
  "items": [{
    "id": "uuid",
    "user_id": "uuid",
    "notification_type": "TASK_ASSIGNED",
    "title": "Yeni uzman göreviniz var",
    "body": "Yaprakları kontrol edin",
    "deep_link": "tarla-asistani://farms/uuid/tasks/uuid",
    "data": {"farm_id": "uuid", "task_id": "uuid"},
    "status": "SENT",
    "attempt_count": 1,
    "last_error": null,
    "sent_at": "2026-08-05T10:00:00Z",
    "read_at": null,
    "created_at": "2026-08-05T10:00:00Z"
  }],
  "total": 1,
  "unread": 1,
  "limit": 50,
  "offset": 0
}
```

## 8. Pilot Feedback and Metrics

### 8.1 Create Feedback
`POST /pilot/feedback`
```json
{
  "feedback_type": "WEEKLY_CHECKIN",
  "rating": 4,
  "comment": "Görevler anlaşılır ve zamanında geldi.",
  "related_task_id": null,
  "related_case_id": null
}
```

`FALSE_ALERT` için hava kaynağından üretilmiş `related_task_id`; `WEEKLY_CHECKIN`
için 1–5 arası `rating` zorunludur.

### 8.2 Review Feedback (Agronomist)

- `GET /pilot/feedback?status=OPEN&limit=50&offset=0`
- `PATCH /pilot/feedback/{feedback_id}` body: `{"status":"REVIEWED"}`

### 8.3 Pilot Metrics (Agronomist)
`GET /pilot/metrics?window_days=7`
```json
{
  "window_days": 7,
  "active_farmers": 38,
  "tasks_created": 120,
  "tasks_completed": 91,
  "task_completion_rate": 75.83,
  "critical_weather_alerts": 12,
  "false_alerts": 1,
  "false_alert_rate": 8.33,
  "cases_created": 16,
  "cases_answered": 14,
  "average_expert_response_minutes": 42.5,
  "notifications_created": 148,
  "notifications_sent": 143,
  "notification_delivery_rate": 96.62,
  "feedback_count": 31,
  "average_feedback_rating": 4.2
}
```

## 9. Operations

- `GET /health/live`: process liveness
- `GET /health/ready`: database and Redis readiness
- `GET /metrics`: Prometheus text metrics (OpenAPI schema dışında)
- Her response `X-Request-ID` döndürür; beklenmeyen hatalarda aynı kimlik hata
  response'una eklenir.

## 10. Error Responses
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid parameters provided.",
    "details": ["phone_number format is incorrect"]
  }
}
```

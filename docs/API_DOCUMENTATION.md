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
- **Response:** Priority 3 task list on the relevant date (or today).

### 4.2 Complete Task
`POST /tasks/{task_id}/complete`
- **Response:** Task is marked as completed and recorded as an activity.

## 5. Cases (Cases)

### 5.1 Report Case (Problem)
`POST /cases`
```json
// Request
{
  "farm_id": "uuid",
  "category": "DISEASE",
  "description": "There is yellowing on the leaves.",
  "media_urls": ["https://s3.aws.com/.../photo.jpg"]
}
```

## 6. Error Responses
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid parameters provided.",
    "details": ["phone_number format is incorrect"]
  }
}
```

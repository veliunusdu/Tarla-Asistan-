# Tarla Asistanı API Dokümantasyonu

> Bu dosya `scripts/generate_api_docs.py` ile backend kodundaki gerçek FastAPI OpenAPI şemasından üretilir. Elle düzenlemek yerine üretim aracını çalıştırın.

## Genel Bakış

- **API sürümü:** `0.1.0`
- **Yerel Docker adresi:** `http://localhost:8100`
- **API öneki:** `/api/v1`
- **İçerik tipi:** `application/json`; medya yükleme için `multipart/form-data`
- **Kapsam:** 48 işlem, 39 yol, 74 şema
- **Makine tarafından okunabilir tanım:** [`openapi.json`](./openapi.json)
- **Paylaşılabilir görsel doküman:** [`api-docs.html`](./api-docs.html)

## Kimlik Doğrulama ve Oturum

Korunan uçlarda `Authorization: Bearer <access_token>` başlığı kullanılır. Access token varsayılan olarak 60 dakika geçerlidir. `POST /api/v1/auth/refresh` refresh tokenı döndürür; eski refresh token tekrar kullanılamaz. OTP kodu varsayılan olarak 180 saniye geçerlidir ve istek/deneme sınırları uygulanır.

Roller: `FARMER` kendi tarlaları, faaliyetleri ve vakaları üzerinde çalışır. `AGRONOMIST` uzman görevleri, vaka değerlendirmeleri ve pilot metrikleri yönetir. Kaynak sahipliği sunucu tarafında kontrol edilir; yetkisiz kaynağın varlığını açıklamamak için birçok uç `404` döndürür.

## Ortak Kurallar

- Tarihler ISO 8601 biçimindedir: `YYYY-MM-DD`; zamanlar UTC `date-time` olarak gönderilir.
- Kimlikler UUID biçimindedir.
- Liste uçları çoğunlukla `limit` ve `offset` kullanır; yanıt `items` ve `total` içerir.
- `client_operation_id`, çevrimdışı tekrar gönderimlerini tekilleştirir. Aynı kimlik farklı içerikle kullanılırsa `409 Conflict` döner.
- Arşivleme uçları fiziksel silme yapmaz; kayıtlar geçmiş ve denetim amacıyla korunur.
- Her yanıtta `X-Request-ID` bulunur. Sunucu hatalarında aynı değer JSON gövdesindeki `request_id` alanında da döner.

## Endpoint Özeti

| Grup | Metot | Yol | Açıklama | Erişim |
|---|---|---|---|---|
| Sistem | `GET` | `/health` | Health | Herkese açık |
| Sistem | `GET` | `/health/live` | Liveness | Herkese açık |
| Sistem | `GET` | `/health/ready` | Readiness | Herkese açık |
| Kimlik doğrulama | `POST` | `/api/v1/auth/request-otp` | Telefon numarasına tek kullanımlık kod gönder | Herkese açık |
| Kimlik doğrulama | `POST` | `/api/v1/auth/verify-otp` | Tek kullanımlık kodu doğrula ve JWT üret | Herkese açık |
| Kimlik doğrulama | `POST` | `/api/v1/auth/refresh` | Refresh tokenı döndürerek oturumu yenile | Herkese açık |
| Kimlik doğrulama | `POST` | `/api/v1/auth/logout` | Refresh oturumunu iptal et | Herkese açık |
| Kimlik doğrulama | `GET` | `/api/v1/auth/me` | Aktif oturumu getir | Bearer token |
| Kullanıcılar | `PUT` | `/api/v1/users/me` | Zorunlu profili tamamla | Bearer token |
| Tarlalar | `GET` | `/api/v1/farms` | Kullanıcının tarlalarını listele | Bearer; sahiplik ve rol denetimi |
| Tarlalar | `POST` | `/api/v1/farms` | Tarla ve ilk üretim dönemini oluştur | Bearer; sahiplik ve rol denetimi |
| Tarlalar | `GET` | `/api/v1/farms/{farm_id}` | Sahibi olunan tarlayı getir | Bearer; sahiplik ve rol denetimi |
| Tarlalar | `PATCH` | `/api/v1/farms/{farm_id}` | Sahibi olunan tarlayı güncelle | Bearer; sahiplik ve rol denetimi |
| Tarlalar | `DELETE` | `/api/v1/farms/{farm_id}` | Sahibi olunan tarlayı arşivle | Bearer; sahiplik ve rol denetimi |
| Üretim Dönemleri | `GET` | `/api/v1/farms/{farm_id}/production-periods` | Tarlanın üretim dönemi geçmişini listele | Bearer; sahiplik ve rol denetimi |
| Üretim Dönemleri | `POST` | `/api/v1/farms/{farm_id}/production-periods` | Yeni üretim dönemi başlat | Bearer; sahiplik ve rol denetimi |
| Üretim Dönemleri | `POST` | `/api/v1/farms/{farm_id}/production-periods/{period_id}/close` | Aktif üretim dönemini kapat | Bearer; sahiplik ve rol denetimi |
| Hava Durumu | `GET` | `/api/v1/farms/{farm_id}/weather` | Tarla için hava tahmini ve tarımsal riskleri getir | Bearer; sahiplik ve rol denetimi |
| Günlük Görevler | `GET` | `/api/v1/farms/{farm_id}/tasks` | Günlük öncelikli görevleri listele | Bearer; sahiplik ve rol denetimi |
| Günlük Görevler | `POST` | `/api/v1/farms/{farm_id}/tasks` | Uzman görevi oluştur | Yalnızca AGRONOMIST |
| Günlük Görevler | `GET` | `/api/v1/tasks/{task_id}` | Görev detayını getir | Bearer; sahiplik ve rol denetimi |
| Günlük Görevler | `PATCH` | `/api/v1/tasks/{task_id}/status` | Görev durumunu güncelle | Bearer; sahiplik ve rol denetimi |
| Günlük Görevler | `POST` | `/api/v1/tasks/{task_id}/complete` | Görevi tamamla ve tarla günlüğüne kaydet | Bearer; sahiplik ve rol denetimi |
| Faaliyetler ve Tarla Günlüğü | `GET` | `/api/v1/farms/{farm_id}/activities` | Tarla faaliyetlerini listele | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Faaliyetler ve Tarla Günlüğü | `POST` | `/api/v1/farms/{farm_id}/activities` | Faaliyet veya sesli faaliyet taslağı oluştur | Yalnızca FARMER |
| Faaliyetler ve Tarla Günlüğü | `PATCH` | `/api/v1/activities/{activity_id}` | Faaliyeti güncelle ve önceki değerleri koru | Yalnızca FARMER |
| Faaliyetler ve Tarla Günlüğü | `DELETE` | `/api/v1/activities/{activity_id}` | Faaliyeti arşivle | Yalnızca FARMER |
| Faaliyetler ve Tarla Günlüğü | `POST` | `/api/v1/activities/{activity_id}/confirm` | Sesli faaliyet taslağını doğrula | Yalnızca FARMER |
| Faaliyetler ve Tarla Günlüğü | `GET` | `/api/v1/activities/{activity_id}/revisions` | Faaliyet değişiklik geçmişini getir | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Faaliyetler ve Tarla Günlüğü | `POST` | `/api/v1/activities/{activity_id}/restore` | Arşivlenmiş faaliyeti geri yükle | Yalnızca FARMER |
| Faaliyetler ve Tarla Günlüğü | `GET` | `/api/v1/farms/{farm_id}/journal` | Tarla günlüğünü kronolojik olarak getir | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Medya | `POST` | `/api/v1/media` | Fotoğraf veya ses dosyası yükle | Bearer token |
| Medya | `GET` | `/api/v1/media/{media_id}/content` | Yetkili medya içeriğini getir | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Sorun Bildirme ve Vakalar | `GET` | `/api/v1/cases` | Rol bazlı vaka listesini getir | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Sorun Bildirme ve Vakalar | `POST` | `/api/v1/cases` | Fotoğraf, ses veya yazı ile vaka oluştur | Yalnızca FARMER |
| Sorun Bildirme ve Vakalar | `GET` | `/api/v1/cases/{case_id}` | Vaka, tarla bağlamı ve mesajlarını getir | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Sorun Bildirme ve Vakalar | `PATCH` | `/api/v1/cases/{case_id}/status` | Uzman vaka durumunu ve önceliğini güncellesin | Yalnızca AGRONOMIST |
| Sorun Bildirme ve Vakalar | `POST` | `/api/v1/cases/{case_id}/messages` | Vakaya mesaj veya ek bilgi isteği ekle | FARMER / AGRONOMIST (rol kapsamı uygulanır) |
| Sorun Bildirme ve Vakalar | `POST` | `/api/v1/cases/{case_id}/expert-response` | Uzman cevabı ekle ve isteğe bağlı vakayı kapat | Yalnızca AGRONOMIST |
| Bildirimler | `POST` | `/api/v1/notifications/devices` | Push bildirim cihaz tokenını kaydet | Bearer token |
| Bildirimler | `DELETE` | `/api/v1/notifications/devices/{device_id}` | Cihaz tokenını pasifleştir | Bearer token |
| Bildirimler | `GET` | `/api/v1/notifications` | Kullanıcının bildirim kutusunu getir | Bearer token |
| Bildirimler | `POST` | `/api/v1/notifications/{notification_id}/read` | Bildirimi okundu olarak işaretle | Bearer token |
| Pilot Ölçümü ve Geri Bildirim | `GET` | `/api/v1/pilot/feedback` | Pilot geri bildirimlerini listele | Yalnızca AGRONOMIST |
| Pilot Ölçümü ve Geri Bildirim | `POST` | `/api/v1/pilot/feedback` | Haftalık geri bildirim veya yanlış uyarı kaydı oluştur | Bearer token |
| Pilot Ölçümü ve Geri Bildirim | `PATCH` | `/api/v1/pilot/feedback/{feedback_id}` | Pilot geri bildirim durumunu güncelle | Yalnızca AGRONOMIST |
| Pilot Ölçümü ve Geri Bildirim | `GET` | `/api/v1/pilot/metrics` | Pilot başarı ve kalite metriklerini getir | Yalnızca AGRONOMIST |
| Yapay Zeka | `POST` | `/api/v1/ai/chat` | Tarla bağlamında AI sohbeti başlat veya sürdür | Bearer token |

## Endpoint Ayrıntıları

### Sistem

<a id="endpoint-health-health-get"></a>
#### `GET /health` — Health

- **Erişim:** Herkese açık
- **Operation ID:** `health_health_get`

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · object |

**Örnek başarılı yanıt (`200`)**

```json
{}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/health'
```

<a id="endpoint-liveness-health-live-get"></a>
#### `GET /health/live` — Liveness

- **Erişim:** Herkese açık
- **Operation ID:** `liveness_health_live_get`

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · object |

**Örnek başarılı yanıt (`200`)**

```json
{}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/health/live'
```

<a id="endpoint-readiness-health-ready-get"></a>
#### `GET /health/ready` — Readiness

- **Erişim:** Herkese açık
- **Operation ID:** `readiness_health_ready_get`

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · object |

**Örnek başarılı yanıt (`200`)**

```json
{}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/health/ready'
```

### Kimlik doğrulama

<a id="endpoint-request-otp-api-v1-auth-request-otp-post"></a>
#### `POST /api/v1/auth/request-otp` — Telefon numarasına tek kullanımlık kod gönder

- **Erişim:** Herkese açık
- **Operation ID:** `request_otp_api_v1_auth_request_otp_post`

**Request body** — `application/json` · [RequestOtpRequest](#schema-requestotprequest)

```json
{
  "phone_number": "+905551234567"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [RequestOtpResponse](#schema-requestotpresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "message": "Doğrulama kodu gönderildi.",
  "expires_in": 1,
  "debug_otp": "string"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/auth/request-otp' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "phone_number": "+905551234567"
}'
```

<a id="endpoint-verify-otp-api-v1-auth-verify-otp-post"></a>
#### `POST /api/v1/auth/verify-otp` — Tek kullanımlık kodu doğrula ve JWT üret

- **Erişim:** Herkese açık
- **Operation ID:** `verify_otp_api_v1_auth_verify_otp_post`

**Request body** — `application/json` · [VerifyOtpRequest](#schema-verifyotprequest)

```json
{
  "phone_number": "+905551234567",
  "otp_code": "123456"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [TokenResponse](#schema-tokenresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "access_token": "example-device-or-refresh-token",
  "refresh_token": "example-device-or-refresh-token",
  "token_type": "bearer",
  "expires_in": 1,
  "user": {
    "id": "11111111-1111-4111-8111-111111111111",
    "phone_number": "+905551234567",
    "full_name": null,
    "province": null,
    "district": null,
    "role": null,
    "terms_accepted": false,
    "notifications_enabled": false,
    "profile_complete": false
  }
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/auth/verify-otp' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "phone_number": "+905551234567",
  "otp_code": "123456"
}'
```

<a id="endpoint-refresh-session-api-v1-auth-refresh-post"></a>
#### `POST /api/v1/auth/refresh` — Refresh tokenı döndürerek oturumu yenile

- **Erişim:** Herkese açık
- **Operation ID:** `refresh_session_api_v1_auth_refresh_post`

**Request body** — `application/json` · [RefreshTokenRequest](#schema-refreshtokenrequest)

```json
{
  "refresh_token": "example-device-or-refresh-token"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [TokenResponse](#schema-tokenresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "access_token": "example-device-or-refresh-token",
  "refresh_token": "example-device-or-refresh-token",
  "token_type": "bearer",
  "expires_in": 1,
  "user": {
    "id": "11111111-1111-4111-8111-111111111111",
    "phone_number": "+905551234567",
    "full_name": null,
    "province": null,
    "district": null,
    "role": null,
    "terms_accepted": false,
    "notifications_enabled": false,
    "profile_complete": false
  }
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/auth/refresh' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "refresh_token": "example-device-or-refresh-token"
}'
```

<a id="endpoint-logout-api-v1-auth-logout-post"></a>
#### `POST /api/v1/auth/logout` — Refresh oturumunu iptal et

- **Erişim:** Herkese açık
- **Operation ID:** `logout_api_v1_auth_logout_post`

**Request body** — `application/json` · [RefreshTokenRequest](#schema-refreshtokenrequest)

```json
{
  "refresh_token": "example-device-or-refresh-token"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `204` | Başarılı yanıt | Boş gövde |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/auth/logout' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "refresh_token": "example-device-or-refresh-token"
}'
```

<a id="endpoint-me-api-v1-auth-me-get"></a>
#### `GET /api/v1/auth/me` — Aktif oturumu getir

- **Erişim:** Bearer token
- **Operation ID:** `me_api_v1_auth_me_get`

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [UserResponse](#schema-userresponse) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "phone_number": "+905551234567",
  "full_name": "string",
  "province": "string",
  "district": "string",
  "role": "FARMER",
  "terms_accepted": false,
  "notifications_enabled": false,
  "profile_complete": false
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/auth/me' \
  -H 'Authorization: Bearer <access_token>'
```

### Kullanıcılar

<a id="endpoint-update-profile-api-v1-users-me-put"></a>
#### `PUT /api/v1/users/me` — Zorunlu profili tamamla

- **Erişim:** Bearer token
- **Operation ID:** `update_profile_api_v1_users_me_put`

**Request body** — `application/json` · [ProfileUpdate](#schema-profileupdate)

```json
{
  "full_name": "Örnek kayıt",
  "province": "string",
  "district": "string",
  "terms_accepted": false,
  "notifications_enabled": true
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [UserResponse](#schema-userresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "phone_number": "+905551234567",
  "full_name": "string",
  "province": "string",
  "district": "string",
  "role": "FARMER",
  "terms_accepted": false,
  "notifications_enabled": false,
  "profile_complete": false
}
```

**cURL**

```bash
curl -X PUT 'http://localhost:8100/api/v1/users/me' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "full_name": "Örnek kayıt",
  "province": "string",
  "district": "string",
  "terms_accepted": false,
  "notifications_enabled": true
}'
```

### Tarlalar

<a id="endpoint-list-farms-api-v1-farms-get"></a>
#### `GET /api/v1/farms` — Kullanıcının tarlalarını listele

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `list_farms_api_v1_farms_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `include_archived` | query | boolean | Hayır | varsayılan: `False` |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [FarmListResponse](#schema-farmlistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "owner_id": null,
      "name": null,
      "latitude": null,
      "longitude": null,
      "size_in_hectares": null,
      "irrigation_method": null,
      "soil_type": null,
      "note": null,
      "archived_at": null,
      "created_at": null,
      "updated_at": null,
      "current_crop": null
    }
  ],
  "total": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-farm-api-v1-farms-post"></a>
#### `POST /api/v1/farms` — Tarla ve ilk üretim dönemini oluştur

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `create_farm_api_v1_farms_post`

**Request body** — `application/json` · [FarmCreate](#schema-farmcreate)

```json
{
  "name": "Örnek kayıt",
  "latitude": 1.0,
  "longitude": 1.0,
  "size_in_hectares": 1.0,
  "irrigation_method": "DRIP",
  "soil_type": "string",
  "note": "string",
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [FarmMutationResponse](#schema-farmmutationresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "farm": {
    "id": "11111111-1111-4111-8111-111111111111",
    "owner_id": "11111111-1111-4111-8111-111111111111",
    "name": "Örnek kayıt",
    "latitude": null,
    "longitude": null,
    "size_in_hectares": null,
    "irrigation_method": null,
    "soil_type": null,
    "note": null,
    "archived_at": null,
    "created_at": "2026-08-09T12:00:00Z",
    "updated_at": "2026-08-09T12:00:00Z",
    "current_crop": null
  },
  "warnings": [
    "string"
  ]
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/farms' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "name": "Örnek kayıt",
  "latitude": 1.0,
  "longitude": 1.0,
  "size_in_hectares": 1.0,
  "irrigation_method": "DRIP",
  "soil_type": "string",
  "note": "string",
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09"
}'
```

<a id="endpoint-get-farm-api-v1-farms-farm-id-get"></a>
#### `GET /api/v1/farms/{farm_id}` — Sahibi olunan tarlayı getir

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `get_farm_api_v1_farms__farm_id__get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [FarmResponse](#schema-farmresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "owner_id": "11111111-1111-4111-8111-111111111111",
  "name": "Örnek kayıt",
  "latitude": 1.0,
  "longitude": 1.0,
  "size_in_hectares": 1.0,
  "irrigation_method": "DRIP",
  "soil_type": "string",
  "note": "string",
  "archived_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "current_crop": {
    "id": null,
    "farm_id": null,
    "crop_type": null,
    "variety": null,
    "planted_at": null,
    "harvested_at": null,
    "status": null,
    "created_at": null,
    "updated_at": null
  }
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-update-farm-api-v1-farms-farm-id-patch"></a>
#### `PATCH /api/v1/farms/{farm_id}` — Sahibi olunan tarlayı güncelle

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `update_farm_api_v1_farms__farm_id__patch`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [FarmUpdate](#schema-farmupdate)

```json
{
  "name": "string",
  "latitude": 1.0,
  "longitude": 1.0,
  "size_in_hectares": 1.0,
  "irrigation_method": "DRIP",
  "soil_type": "string",
  "note": "string"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [FarmMutationResponse](#schema-farmmutationresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "farm": {
    "id": "11111111-1111-4111-8111-111111111111",
    "owner_id": "11111111-1111-4111-8111-111111111111",
    "name": "Örnek kayıt",
    "latitude": null,
    "longitude": null,
    "size_in_hectares": null,
    "irrigation_method": null,
    "soil_type": null,
    "note": null,
    "archived_at": null,
    "created_at": "2026-08-09T12:00:00Z",
    "updated_at": "2026-08-09T12:00:00Z",
    "current_crop": null
  },
  "warnings": [
    "string"
  ]
}
```

**cURL**

```bash
curl -X PATCH 'http://localhost:8100/api/v1/farms/{farm_id}' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "name": "string",
  "latitude": 1.0,
  "longitude": 1.0,
  "size_in_hectares": 1.0,
  "irrigation_method": "DRIP",
  "soil_type": "string",
  "note": "string"
}'
```

<a id="endpoint-archive-farm-api-v1-farms-farm-id-delete"></a>
#### `DELETE /api/v1/farms/{farm_id}` — Sahibi olunan tarlayı arşivle

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `archive_farm_api_v1_farms__farm_id__delete`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `204` | Başarılı yanıt | Boş gövde |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**cURL**

```bash
curl -X DELETE 'http://localhost:8100/api/v1/farms/{farm_id}' \
  -H 'Authorization: Bearer <access_token>'
```

### Üretim Dönemleri

<a id="endpoint-list-production-periods-api-v1-farms-farm-id-production-periods-get"></a>
#### `GET /api/v1/farms/{farm_id}/production-periods` — Tarlanın üretim dönemi geçmişini listele

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `list_production_periods_api_v1_farms__farm_id__production_periods_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [CropPeriodListResponse](#schema-cropperiodlistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "farm_id": null,
      "crop_type": null,
      "variety": null,
      "planted_at": null,
      "harvested_at": null,
      "status": null,
      "created_at": null,
      "updated_at": null
    }
  ]
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}/production-periods' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-production-period-api-v1-farms-farm-id-production-periods-post"></a>
#### `POST /api/v1/farms/{farm_id}/production-periods` — Yeni üretim dönemi başlat

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `create_production_period_api_v1_farms__farm_id__production_periods_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [CropPeriodCreate](#schema-cropperiodcreate)

```json
{
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09",
  "close_existing": false
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [CropPeriodResponse](#schema-cropperiodresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09",
  "harvested_at": "2026-08-09",
  "status": "ACTIVE",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/farms/{farm_id}/production-periods' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09",
  "close_existing": false
}'
```

<a id="endpoint-close-production-period-api-v1-farms-farm-id-production-periods-period-id-close-post"></a>
#### `POST /api/v1/farms/{farm_id}/production-periods/{period_id}/close` — Aktif üretim dönemini kapat

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `close_production_period_api_v1_farms__farm_id__production_periods__period_id__close_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |
| `period_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [CropPeriodClose](#schema-cropperiodclose)

```json
{
  "harvested_at": "2026-08-09"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [CropPeriodResponse](#schema-cropperiodresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_type": "WHEAT",
  "variety": "string",
  "planted_at": "2026-08-09",
  "harvested_at": "2026-08-09",
  "status": "ACTIVE",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/farms/{farm_id}/production-periods/{period_id}/close' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "harvested_at": "2026-08-09"
}'
```

### Hava Durumu

<a id="endpoint-get-farm-weather-api-v1-farms-farm-id-weather-get"></a>
#### `GET /api/v1/farms/{farm_id}/weather` — Tarla için hava tahmini ve tarımsal riskleri getir

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `get_farm_weather_api_v1_farms__farm_id__weather_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [FarmWeatherResponse](#schema-farmweatherresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "provider": "string",
  "fetched_at": "2026-08-09T12:00:00Z",
  "is_stale": false,
  "stale_reason": "string",
  "points": [
    {
      "observed_at": null,
      "temperature_c": null,
      "precipitation_probability": null,
      "precipitation_mm": null,
      "wind_speed_kmh": null
    }
  ],
  "risks": [
    {
      "risk_type": null,
      "severity": null,
      "starts_at": null,
      "ends_at": null,
      "message": null,
      "suggested_action": null
    }
  ]
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}/weather' \
  -H 'Authorization: Bearer <access_token>'
```

### Günlük Görevler

<a id="endpoint-list-daily-tasks-api-v1-farms-farm-id-tasks-get"></a>
#### `GET /api/v1/farms/{farm_id}/tasks` — Günlük öncelikli görevleri listele

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `list_daily_tasks_api_v1_farms__farm_id__tasks_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |
| `date` | query | string (date) | Hayır | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [DailyTaskListResponse](#schema-dailytasklistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "date": "2026-08-09",
  "items": [
    {
      "id": null,
      "farm_id": null,
      "crop_period_id": null,
      "created_by_id": null,
      "title": null,
      "description": null,
      "reason": null,
      "priority": null,
      "status": null,
      "source": null,
      "confidence": null,
      "due_date": null,
      "not_applied_reason": null,
      "completion_note": null,
      "photo_url": null,
      "viewed_at": null,
      "completed_at": null,
      "created_at": null,
      "updated_at": null,
      "expert_review_recommended": null
    }
  ],
  "critical_weather_alerts": [
    {
      "id": null,
      "farm_id": null,
      "crop_period_id": null,
      "created_by_id": null,
      "title": null,
      "description": null,
      "reason": null,
      "priority": null,
      "status": null,
      "source": null,
      "confidence": null,
      "due_date": null,
      "not_applied_reason": null,
      "completion_note": null,
      "photo_url": null,
      "viewed_at": null,
      "completed_at": null,
      "created_at": null,
      "updated_at": null,
      "expert_review_recommended": null
    }
  ],
  "overdue": [
    {
      "id": null,
      "farm_id": null,
      "crop_period_id": null,
      "created_by_id": null,
      "title": null,
      "description": null,
      "reason": null,
      "priority": null,
      "status": null,
      "source": null,
      "confidence": null,
      "due_date": null,
      "not_applied_reason": null,
      "completion_note": null,
      "photo_url": null,
      "viewed_at": null,
      "completed_at": null,
      "created_at": null,
      "updated_at": null,
      "expert_review_recommended": null
    }
  ],
  "visible_limit": 3
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}/tasks' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-expert-task-api-v1-farms-farm-id-tasks-post"></a>
#### `POST /api/v1/farms/{farm_id}/tasks` — Uzman görevi oluştur

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `create_expert_task_api_v1_farms__farm_id__tasks_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [TaskCreate](#schema-taskcreate)

```json
{
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "HIGH",
  "confidence": "HIGH",
  "due_date": "2026-08-09",
  "crop_period_id": "11111111-1111-4111-8111-111111111111"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [TaskResponse](#schema-taskresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "LOW",
  "status": "NEW",
  "source": "SYSTEM",
  "confidence": "LOW",
  "due_date": "2026-08-09",
  "not_applied_reason": "string",
  "completion_note": "string",
  "photo_url": "string",
  "viewed_at": "2026-08-09T12:00:00Z",
  "completed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "expert_review_recommended": false
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/farms/{farm_id}/tasks' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "HIGH",
  "confidence": "HIGH",
  "due_date": "2026-08-09",
  "crop_period_id": "11111111-1111-4111-8111-111111111111"
}'
```

<a id="endpoint-get-task-api-v1-tasks-task-id-get"></a>
#### `GET /api/v1/tasks/{task_id}` — Görev detayını getir

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `get_task_api_v1_tasks__task_id__get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `task_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [TaskResponse](#schema-taskresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "LOW",
  "status": "NEW",
  "source": "SYSTEM",
  "confidence": "LOW",
  "due_date": "2026-08-09",
  "not_applied_reason": "string",
  "completion_note": "string",
  "photo_url": "string",
  "viewed_at": "2026-08-09T12:00:00Z",
  "completed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "expert_review_recommended": false
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/tasks/{task_id}' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-update-task-status-api-v1-tasks-task-id-status-patch"></a>
#### `PATCH /api/v1/tasks/{task_id}/status` — Görev durumunu güncelle

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `update_task_status_api_v1_tasks__task_id__status_patch`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `task_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [TaskStatusUpdate](#schema-taskstatusupdate)

```json
{
  "status": "NEW",
  "not_applied_reason": "string",
  "note": "string",
  "photo_url": "string"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [TaskResponse](#schema-taskresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "LOW",
  "status": "NEW",
  "source": "SYSTEM",
  "confidence": "LOW",
  "due_date": "2026-08-09",
  "not_applied_reason": "string",
  "completion_note": "string",
  "photo_url": "string",
  "viewed_at": "2026-08-09T12:00:00Z",
  "completed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "expert_review_recommended": false
}
```

**cURL**

```bash
curl -X PATCH 'http://localhost:8100/api/v1/tasks/{task_id}/status' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "status": "NEW",
  "not_applied_reason": "string",
  "note": "string",
  "photo_url": "string"
}'
```

<a id="endpoint-complete-task-api-v1-tasks-task-id-complete-post"></a>
#### `POST /api/v1/tasks/{task_id}/complete` — Görevi tamamla ve tarla günlüğüne kaydet

- **Erişim:** Bearer; sahiplik ve rol denetimi
- **Operation ID:** `complete_task_api_v1_tasks__task_id__complete_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `task_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [TaskCompleteRequest](#schema-taskcompleterequest)

```json
{
  "note": "string",
  "photo_url": "string"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [TaskResponse](#schema-taskresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "reason": "Örnek açıklama",
  "priority": "LOW",
  "status": "NEW",
  "source": "SYSTEM",
  "confidence": "LOW",
  "due_date": "2026-08-09",
  "not_applied_reason": "string",
  "completion_note": "string",
  "photo_url": "string",
  "viewed_at": "2026-08-09T12:00:00Z",
  "completed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "expert_review_recommended": false
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/tasks/{task_id}/complete' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "note": "string",
  "photo_url": "string"
}'
```

### Faaliyetler ve Tarla Günlüğü

<a id="endpoint-list-activities-api-v1-farms-farm-id-activities-get"></a>
#### `GET /api/v1/farms/{farm_id}/activities` — Tarla faaliyetlerini listele

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `list_activities_api_v1_farms__farm_id__activities_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |
| `include_drafts` | query | boolean | Hayır | varsayılan: `True` |
| `include_archived` | query | boolean | Hayır | varsayılan: `False` |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [ActivityListResponse](#schema-activitylistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "farm_id": null,
      "crop_period_id": null,
      "task_id": null,
      "created_by_id": null,
      "activity_type": null,
      "status": null,
      "source": null,
      "description": null,
      "occurred_at": null,
      "duration_minutes": null,
      "amount": null,
      "unit": null,
      "photo_url": null,
      "voice_url": null,
      "voice_transcript": null,
      "performed_by": null,
      "cost": null,
      "confirmed_at": null,
      "archived_at": null,
      "created_at": null,
      "updated_at": null
    }
  ],
  "total": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}/activities' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-activity-api-v1-farms-farm-id-activities-post"></a>
#### `POST /api/v1/farms/{farm_id}/activities` — Faaliyet veya sesli faaliyet taslağı oluştur

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `create_activity_api_v1_farms__farm_id__activities_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [ActivityCreate](#schema-activitycreate)

```json
{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "input_method": "MANUAL",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [ActivityResponse](#schema-activityresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "task_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "status": "DRAFT",
  "source": "MANUAL",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0,
  "confirmed_at": "2026-08-09T12:00:00Z",
  "archived_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/farms/{farm_id}/activities' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "input_method": "MANUAL",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0
}'
```

<a id="endpoint-update-activity-api-v1-activities-activity-id-patch"></a>
#### `PATCH /api/v1/activities/{activity_id}` — Faaliyeti güncelle ve önceki değerleri koru

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `update_activity_api_v1_activities__activity_id__patch`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `activity_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [ActivityUpdate](#schema-activityupdate)

```json
{
  "activity_type": "IRRIGATION",
  "description": "string",
  "occurred_at": "2026-08-09T12:00:00Z",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [ActivityResponse](#schema-activityresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "task_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "status": "DRAFT",
  "source": "MANUAL",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0,
  "confirmed_at": "2026-08-09T12:00:00Z",
  "archived_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X PATCH 'http://localhost:8100/api/v1/activities/{activity_id}' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "activity_type": "IRRIGATION",
  "description": "string",
  "occurred_at": "2026-08-09T12:00:00Z",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0
}'
```

<a id="endpoint-archive-activity-api-v1-activities-activity-id-delete"></a>
#### `DELETE /api/v1/activities/{activity_id}` — Faaliyeti arşivle

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `archive_activity_api_v1_activities__activity_id__delete`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `activity_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `204` | Başarılı yanıt | Boş gövde |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**cURL**

```bash
curl -X DELETE 'http://localhost:8100/api/v1/activities/{activity_id}' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-confirm-activity-api-v1-activities-activity-id-confirm-post"></a>
#### `POST /api/v1/activities/{activity_id}/confirm` — Sesli faaliyet taslağını doğrula

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `confirm_activity_api_v1_activities__activity_id__confirm_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `activity_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [ActivityResponse](#schema-activityresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "task_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "status": "DRAFT",
  "source": "MANUAL",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0,
  "confirmed_at": "2026-08-09T12:00:00Z",
  "archived_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/activities/{activity_id}/confirm' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-list-activity-revisions-api-v1-activities-activity-id-revisions-get"></a>
#### `GET /api/v1/activities/{activity_id}/revisions` — Faaliyet değişiklik geçmişini getir

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `list_activity_revisions_api_v1_activities__activity_id__revisions_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `activity_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · array<[ActivityRevisionResponse](#schema-activityrevisionresponse)> |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
[
  {
    "id": "11111111-1111-4111-8111-111111111111",
    "activity_id": "11111111-1111-4111-8111-111111111111",
    "changed_by_id": "11111111-1111-4111-8111-111111111111",
    "previous_values": {},
    "changed_at": "2026-08-09T12:00:00Z"
  }
]
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/activities/{activity_id}/revisions' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-restore-activity-api-v1-activities-activity-id-restore-post"></a>
#### `POST /api/v1/activities/{activity_id}/restore` — Arşivlenmiş faaliyeti geri yükle

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `restore_activity_api_v1_activities__activity_id__restore_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `activity_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [ActivityResponse](#schema-activityresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "crop_period_id": "11111111-1111-4111-8111-111111111111",
  "task_id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "activity_type": "IRRIGATION",
  "status": "DRAFT",
  "source": "MANUAL",
  "description": "Örnek açıklama",
  "occurred_at": "2026-08-09T12:00:00Z",
  "duration_minutes": 1,
  "amount": 1.0,
  "unit": "string",
  "photo_url": "string",
  "voice_url": "string",
  "voice_transcript": "string",
  "performed_by": "string",
  "cost": 1.0,
  "confirmed_at": "2026-08-09T12:00:00Z",
  "archived_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/activities/{activity_id}/restore' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-get-farm-journal-api-v1-farms-farm-id-journal-get"></a>
#### `GET /api/v1/farms/{farm_id}/journal` — Tarla günlüğünü kronolojik olarak getir

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `get_farm_journal_api_v1_farms__farm_id__journal_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `farm_id` | path | string (uuid) | Evet | — |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [FarmJournalResponse](#schema-farmjournalresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "entry_type": null,
      "id": null,
      "occurred_at": null,
      "title": null,
      "description": null,
      "metadata": null
    }
  ],
  "total": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/farms/{farm_id}/journal' \
  -H 'Authorization: Bearer <access_token>'
```

### Medya

<a id="endpoint-upload-media-api-v1-media-post"></a>
#### `POST /api/v1/media` — Fotoğraf veya ses dosyası yükle

- **Erişim:** Bearer token
- **Operation ID:** `upload_media_api_v1_media_post`

**Request body** — `multipart/form-data` · [Body_upload_media_api_v1_media_post](#schema-body-upload-media-api-v1-media-post)

```json
{
  "file": "string"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [MediaAssetResponse](#schema-mediaassetresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "owner_id": "11111111-1111-4111-8111-111111111111",
  "kind": "IMAGE",
  "original_name": "Örnek kayıt",
  "content_type": "string",
  "size_bytes": 1,
  "checksum_sha256": "string",
  "url": "https://example.com/resource",
  "created_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/media' \
  -H 'Authorization: Bearer <access_token>' \
  -F 'file=@ornek.jpg'
```

<a id="endpoint-get-media-content-api-v1-media-media-id-content-get"></a>
#### `GET /api/v1/media/{media_id}/content` — Yetkili medya içeriğini getir

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `get_media_content_api_v1_media__media_id__content_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `media_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · object |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/media/{media_id}/content' \
  -H 'Authorization: Bearer <access_token>'
```

### Sorun Bildirme ve Vakalar

<a id="endpoint-list-cases-api-v1-cases-get"></a>
#### `GET /api/v1/cases` — Rol bazlı vaka listesini getir

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `list_cases_api_v1_cases_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `status` | query | [CaseStatus](#schema-casestatus) | null | Hayır | — |
| `priority` | query | [CasePriority](#schema-casepriority) | null | Hayır | — |
| `farm_id` | query | string (uuid) | null | Hayır | — |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [CaseListResponse](#schema-caselistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "farm_id": null,
      "farm_name": null,
      "farmer_name": null,
      "created_by_id": null,
      "assigned_expert_id": null,
      "category": null,
      "priority": null,
      "status": null,
      "title": null,
      "description": null,
      "media": null,
      "closed_at": null,
      "created_at": null,
      "updated_at": null
    }
  ],
  "total": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/cases' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-case-api-v1-cases-post"></a>
#### `POST /api/v1/cases` — Fotoğraf, ses veya yazı ile vaka oluştur

- **Erişim:** Yalnızca FARMER
- **Operation ID:** `create_case_api_v1_cases_post`

**Request body** — `application/json` · [CaseCreate](#schema-casecreate)

```json
{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ]
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [CaseDetailResponse](#schema-casedetailresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "farm_name": "Örnek kayıt",
  "farmer_name": "Örnek kayıt",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "assigned_expert_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "priority": "LOW",
  "status": "OPEN",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media": [
    {
      "id": null,
      "owner_id": null,
      "kind": null,
      "original_name": null,
      "content_type": null,
      "size_bytes": null,
      "checksum_sha256": null,
      "url": null,
      "created_at": null
    }
  ],
  "closed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "messages": [
    {
      "id": null,
      "case_id": null,
      "sender_id": null,
      "sender_name": null,
      "sender_role": null,
      "message_type": null,
      "body": null,
      "media": null,
      "created_at": null
    }
  ]
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/cases' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ]
}'
```

<a id="endpoint-get-case-api-v1-cases-case-id-get"></a>
#### `GET /api/v1/cases/{case_id}` — Vaka, tarla bağlamı ve mesajlarını getir

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `get_case_api_v1_cases__case_id__get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `case_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [CaseDetailResponse](#schema-casedetailresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "farm_name": "Örnek kayıt",
  "farmer_name": "Örnek kayıt",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "assigned_expert_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "priority": "LOW",
  "status": "OPEN",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media": [
    {
      "id": null,
      "owner_id": null,
      "kind": null,
      "original_name": null,
      "content_type": null,
      "size_bytes": null,
      "checksum_sha256": null,
      "url": null,
      "created_at": null
    }
  ],
  "closed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "messages": [
    {
      "id": null,
      "case_id": null,
      "sender_id": null,
      "sender_name": null,
      "sender_role": null,
      "message_type": null,
      "body": null,
      "media": null,
      "created_at": null
    }
  ]
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/cases/{case_id}' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-update-case-status-api-v1-cases-case-id-status-patch"></a>
#### `PATCH /api/v1/cases/{case_id}/status` — Uzman vaka durumunu ve önceliğini güncellesin

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `update_case_status_api_v1_cases__case_id__status_patch`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `case_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [CaseStatusUpdate](#schema-casestatusupdate)

```json
{
  "status": "OPEN",
  "priority": "LOW",
  "assign_to_me": false
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [CaseDetailResponse](#schema-casedetailresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "farm_name": "Örnek kayıt",
  "farmer_name": "Örnek kayıt",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "assigned_expert_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "priority": "LOW",
  "status": "OPEN",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media": [
    {
      "id": null,
      "owner_id": null,
      "kind": null,
      "original_name": null,
      "content_type": null,
      "size_bytes": null,
      "checksum_sha256": null,
      "url": null,
      "created_at": null
    }
  ],
  "closed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "messages": [
    {
      "id": null,
      "case_id": null,
      "sender_id": null,
      "sender_name": null,
      "sender_role": null,
      "message_type": null,
      "body": null,
      "media": null,
      "created_at": null
    }
  ]
}
```

**cURL**

```bash
curl -X PATCH 'http://localhost:8100/api/v1/cases/{case_id}/status' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "status": "OPEN",
  "priority": "LOW",
  "assign_to_me": false
}'
```

<a id="endpoint-create-case-message-api-v1-cases-case-id-messages-post"></a>
#### `POST /api/v1/cases/{case_id}/messages` — Vakaya mesaj veya ek bilgi isteği ekle

- **Erişim:** FARMER / AGRONOMIST (rol kapsamı uygulanır)
- **Operation ID:** `create_case_message_api_v1_cases__case_id__messages_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `case_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [CaseMessageCreate](#schema-casemessagecreate)

```json
{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "message_type": "COMMENT",
  "body": "string",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ]
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [CaseMessageResponse](#schema-casemessageresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "case_id": "11111111-1111-4111-8111-111111111111",
  "sender_id": "11111111-1111-4111-8111-111111111111",
  "sender_name": "Örnek kayıt",
  "sender_role": "FARMER",
  "message_type": "COMMENT",
  "body": "string",
  "media": [
    {
      "id": null,
      "owner_id": null,
      "kind": null,
      "original_name": null,
      "content_type": null,
      "size_bytes": null,
      "checksum_sha256": null,
      "url": null,
      "created_at": null
    }
  ],
  "created_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/cases/{case_id}/messages' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "message_type": "COMMENT",
  "body": "string",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ]
}'
```

<a id="endpoint-create-expert-response-api-v1-cases-case-id-expert-response-post"></a>
#### `POST /api/v1/cases/{case_id}/expert-response` — Uzman cevabı ekle ve isteğe bağlı vakayı kapat

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `create_expert_response_api_v1_cases__case_id__expert_response_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `case_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [ExpertResponseCreate](#schema-expertresponsecreate)

```json
{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "body": "string",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ],
  "close_case": false
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [CaseDetailResponse](#schema-casedetailresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "farm_id": "11111111-1111-4111-8111-111111111111",
  "farm_name": "Örnek kayıt",
  "farmer_name": "Örnek kayıt",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "assigned_expert_id": "11111111-1111-4111-8111-111111111111",
  "category": "DISEASE",
  "priority": "LOW",
  "status": "OPEN",
  "title": "Örnek başlık",
  "description": "Örnek açıklama",
  "media": [
    {
      "id": null,
      "owner_id": null,
      "kind": null,
      "original_name": null,
      "content_type": null,
      "size_bytes": null,
      "checksum_sha256": null,
      "url": null,
      "created_at": null
    }
  ],
  "closed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z",
  "messages": [
    {
      "id": null,
      "case_id": null,
      "sender_id": null,
      "sender_name": null,
      "sender_role": null,
      "message_type": null,
      "body": null,
      "media": null,
      "created_at": null
    }
  ]
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/cases/{case_id}/expert-response' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "client_operation_id": "11111111-1111-4111-8111-111111111111",
  "body": "string",
  "media_ids": [
    "11111111-1111-4111-8111-111111111111"
  ],
  "close_case": false
}'
```

### Bildirimler

<a id="endpoint-register-device-api-v1-notifications-devices-post"></a>
#### `POST /api/v1/notifications/devices` — Push bildirim cihaz tokenını kaydet

- **Erişim:** Bearer token
- **Operation ID:** `register_device_api_v1_notifications_devices_post`

**Request body** — `application/json` · [DeviceTokenRegister](#schema-devicetokenregister)

```json
{
  "token": "example-device-or-refresh-token",
  "platform": "ANDROID"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [DeviceTokenResponse](#schema-devicetokenresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "user_id": "11111111-1111-4111-8111-111111111111",
  "platform": "ANDROID",
  "active": false,
  "last_seen_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/notifications/devices' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "token": "example-device-or-refresh-token",
  "platform": "ANDROID"
}'
```

<a id="endpoint-deactivate-device-api-v1-notifications-devices-device-id-delete"></a>
#### `DELETE /api/v1/notifications/devices/{device_id}` — Cihaz tokenını pasifleştir

- **Erişim:** Bearer token
- **Operation ID:** `deactivate_device_api_v1_notifications_devices__device_id__delete`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `device_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `204` | Başarılı yanıt | Boş gövde |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**cURL**

```bash
curl -X DELETE 'http://localhost:8100/api/v1/notifications/devices/{device_id}' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-list-notifications-api-v1-notifications-get"></a>
#### `GET /api/v1/notifications` — Kullanıcının bildirim kutusunu getir

- **Erişim:** Bearer token
- **Operation ID:** `list_notifications_api_v1_notifications_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `unread_only` | query | boolean | Hayır | varsayılan: `False` |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [NotificationListResponse](#schema-notificationlistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "user_id": null,
      "notification_type": null,
      "title": null,
      "body": null,
      "deep_link": null,
      "data": null,
      "status": null,
      "attempt_count": null,
      "last_error": null,
      "sent_at": null,
      "read_at": null,
      "created_at": null
    }
  ],
  "total": 1,
  "unread": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/notifications' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-mark-notification-read-api-v1-notifications-notification-id-read-post"></a>
#### `POST /api/v1/notifications/{notification_id}/read` — Bildirimi okundu olarak işaretle

- **Erişim:** Bearer token
- **Operation ID:** `mark_notification_read_api_v1_notifications__notification_id__read_post`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `notification_id` | path | string (uuid) | Evet | — |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [NotificationResponse](#schema-notificationresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "user_id": "11111111-1111-4111-8111-111111111111",
  "notification_type": "TASK_ASSIGNED",
  "title": "Örnek başlık",
  "body": "string",
  "deep_link": "string",
  "data": {},
  "status": "PENDING",
  "attempt_count": 1,
  "last_error": "string",
  "sent_at": "2026-08-09T12:00:00Z",
  "read_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/notifications/{notification_id}/read' \
  -H 'Authorization: Bearer <access_token>'
```

### Pilot Ölçümü ve Geri Bildirim

<a id="endpoint-list-feedback-api-v1-pilot-feedback-get"></a>
#### `GET /api/v1/pilot/feedback` — Pilot geri bildirimlerini listele

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `list_feedback_api_v1_pilot_feedback_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `feedback_type` | query | [FeedbackType](#schema-feedbacktype) | null | Hayır | — |
| `status` | query | [FeedbackStatus](#schema-feedbackstatus) | null | Hayır | — |
| `limit` | query | integer | Hayır | varsayılan: `50`; min: `1`; maks: `100` |
| `offset` | query | integer | Hayır | varsayılan: `0`; min: `0` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [PilotFeedbackListResponse](#schema-pilotfeedbacklistresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "items": [
    {
      "id": null,
      "created_by_id": null,
      "created_by_name": null,
      "feedback_type": null,
      "status": null,
      "rating": null,
      "comment": null,
      "related_task_id": null,
      "related_case_id": null,
      "reviewed_by_id": null,
      "reviewed_at": null,
      "created_at": null,
      "updated_at": null
    }
  ],
  "total": 1,
  "limit": 1,
  "offset": 1
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/pilot/feedback' \
  -H 'Authorization: Bearer <access_token>'
```

<a id="endpoint-create-feedback-api-v1-pilot-feedback-post"></a>
#### `POST /api/v1/pilot/feedback` — Haftalık geri bildirim veya yanlış uyarı kaydı oluştur

- **Erişim:** Bearer token
- **Operation ID:** `create_feedback_api_v1_pilot_feedback_post`

**Request body** — `application/json` · [PilotFeedbackCreate](#schema-pilotfeedbackcreate)

```json
{
  "feedback_type": "WEEKLY_CHECKIN",
  "rating": 1,
  "comment": "Örnek açıklama",
  "related_task_id": "11111111-1111-4111-8111-111111111111",
  "related_case_id": "11111111-1111-4111-8111-111111111111"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `201` | Başarılı yanıt | application/json · [PilotFeedbackResponse](#schema-pilotfeedbackresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`201`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "created_by_name": "Örnek kayıt",
  "feedback_type": "WEEKLY_CHECKIN",
  "status": "OPEN",
  "rating": 1,
  "comment": "Örnek açıklama",
  "related_task_id": "11111111-1111-4111-8111-111111111111",
  "related_case_id": "11111111-1111-4111-8111-111111111111",
  "reviewed_by_id": "11111111-1111-4111-8111-111111111111",
  "reviewed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/pilot/feedback' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "feedback_type": "WEEKLY_CHECKIN",
  "rating": 1,
  "comment": "Örnek açıklama",
  "related_task_id": "11111111-1111-4111-8111-111111111111",
  "related_case_id": "11111111-1111-4111-8111-111111111111"
}'
```

<a id="endpoint-update-feedback-api-v1-pilot-feedback-feedback-id-patch"></a>
#### `PATCH /api/v1/pilot/feedback/{feedback_id}` — Pilot geri bildirim durumunu güncelle

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `update_feedback_api_v1_pilot_feedback__feedback_id__patch`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `feedback_id` | path | string (uuid) | Evet | — |

**Request body** — `application/json` · [PilotFeedbackUpdate](#schema-pilotfeedbackupdate)

```json
{
  "status": "OPEN"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [PilotFeedbackResponse](#schema-pilotfeedbackresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "created_by_id": "11111111-1111-4111-8111-111111111111",
  "created_by_name": "Örnek kayıt",
  "feedback_type": "WEEKLY_CHECKIN",
  "status": "OPEN",
  "rating": 1,
  "comment": "Örnek açıklama",
  "related_task_id": "11111111-1111-4111-8111-111111111111",
  "related_case_id": "11111111-1111-4111-8111-111111111111",
  "reviewed_by_id": "11111111-1111-4111-8111-111111111111",
  "reviewed_at": "2026-08-09T12:00:00Z",
  "created_at": "2026-08-09T12:00:00Z",
  "updated_at": "2026-08-09T12:00:00Z"
}
```

**cURL**

```bash
curl -X PATCH 'http://localhost:8100/api/v1/pilot/feedback/{feedback_id}' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "status": "OPEN"
}'
```

<a id="endpoint-pilot-metrics-api-v1-pilot-metrics-get"></a>
#### `GET /api/v1/pilot/metrics` — Pilot başarı ve kalite metriklerini getir

- **Erişim:** Yalnızca AGRONOMIST
- **Operation ID:** `pilot_metrics_api_v1_pilot_metrics_get`

**Parametreler**

| Ad | Konum | Tip | Zorunlu | Kurallar |
|---|---|---|---|---|
| `window_days` | query | integer | Hayır | varsayılan: `7`; min: `1`; maks: `90` |

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [PilotMetricsResponse](#schema-pilotmetricsresponse) |
| `422` | Doğrulama hatası | application/json · [HTTPValidationError](#schema-httpvalidationerror) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "window_days": 1,
  "active_farmers": 1,
  "tasks_created": 1,
  "tasks_completed": 1,
  "task_completion_rate": 1.0,
  "critical_weather_alerts": 1,
  "false_alerts": 1,
  "false_alert_rate": 1.0,
  "cases_created": 1,
  "cases_answered": 1,
  "average_expert_response_minutes": 1.0,
  "notifications_created": 1,
  "notifications_sent": 1,
  "notification_delivery_rate": 1.0,
  "feedback_count": 1,
  "average_feedback_rating": 1.0
}
```

**cURL**

```bash
curl -X GET 'http://localhost:8100/api/v1/pilot/metrics' \
  -H 'Authorization: Bearer <access_token>'
```

### Yapay Zeka

<a id="endpoint-chat-api-v1-ai-chat-post"></a>
#### `POST /api/v1/ai/chat` — Tarla bağlamında AI sohbeti başlat veya sürdür

- **Erişim:** Bearer token
- **Operation ID:** `chat_api_v1_ai_chat_post`

**Request body** — `application/json` · object

```json
{
  "message": "string",
  "field_id": "string",
  "conversation_id": "string",
  "history": "string"
}
```

**Yanıtlar**

| HTTP | Açıklama | Gövde |
|---|---|---|
| `200` | Başarılı yanıt | application/json · [AIChatResponse](#schema-aichatresponse) |

**Örnek başarılı yanıt (`200`)**

```json
{
  "reply": "string",
  "conversation_id": "string"
}
```

**cURL**

```bash
curl -X POST 'http://localhost:8100/api/v1/ai/chat' \
  -H 'Authorization: Bearer <access_token>' \
  -H 'Content-Type: application/json' \
  --data-binary '{
  "message": "string",
  "field_id": "string",
  "conversation_id": "string",
  "history": "string"
}'
```

## Ortak Hata Cevapları

| HTTP | Anlam | Tipik neden |
|---|---|---|
| `400` | Bad Request | Geçersiz OTP veya iş isteği |
| `401` | Unauthorized | Eksik, süresi dolmuş veya geçersiz oturum |
| `403` | Forbidden | Rolün işlemi yapmaya yetkili olmaması |
| `404` | Not Found | Kaynak yok veya kullanıcı için görünür değil |
| `409` | Conflict | Geçersiz durum geçişi, yinelenen işlem veya aktif dönem çakışması |
| `413` | Payload Too Large | Medya dosyasının boyut sınırını aşması |
| `415` | Unsupported Media Type | Desteklenmeyen medya tipi |
| `422` | Validation Error | Alan, biçim veya iş kuralı doğrulamasının başarısız olması |
| `429` | Too Many Requests | OTP bekleme süresi dolmadan yeniden istek |
| `500` | Internal Server Error | Beklenmeyen hata; destek için `request_id` kullanılır |
| `503` | Service Unavailable | Hava sağlayıcısı kullanılamıyor ve geçerli önbellek yok |

Doğrulama hatası örneği:

```json
{"detail":[{"loc":["body","field"],"msg":"Field required","type":"missing"}]}
```

Sunucu hatası örneği:

```json
{"detail":"Beklenmeyen bir hata oluştu.","request_id":"uuid"}
```

## Operasyonel Uçlar

| Metot | Yol | Açıklama |
|---|---|---|
| `GET` | `/health/live` | Sürecin çalıştığını kontrol eder. |
| `GET` | `/health/ready` | Veritabanı ve Redis erişimini kontrol eder. |
| `GET` | `/health` | Hazırlık kontrolünün uyumluluk adresidir. |
| `GET` | `/metrics` | Etkinse Prometheus metni döndürür; OpenAPI dışında tutulur. |

## Model Şemaları

<a id="schema-aichatresponse"></a>
### `AIChatResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `reply` | string | Evet | — |
| `conversation_id` | string | Evet | — |

<a id="schema-activitycreate"></a>
### `ActivityCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `client_operation_id` | string (uuid) | null | Hayır | Çevrimdışı tekrar gönderimlerini güvenle tekilleştiren istemci işlem kimliği. |
| `activity_type` | [ActivityType](#schema-activitytype) | Evet | — |
| `description` | string | Evet | min uzunluk: `2`; maks uzunluk: `4000` |
| `occurred_at` | string (date-time) | Hayır | — |
| `crop_period_id` | string (uuid) | null | Hayır | — |
| `input_method` | [ActivitySource](#schema-activitysource) | Hayır | varsayılan: `MANUAL` |
| `duration_minutes` | integer | null | Hayır | — |
| `amount` | number | null | Hayır | — |
| `unit` | string | null | Hayır | — |
| `photo_url` | string | null | Hayır | — |
| `voice_url` | string | null | Hayır | — |
| `voice_transcript` | string | null | Hayır | — |
| `performed_by` | string | null | Hayır | — |
| `cost` | number | null | Hayır | — |

<a id="schema-activitylistresponse"></a>
### `ActivityListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[ActivityResponse](#schema-activityresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-activityresponse"></a>
### `ActivityResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `farm_id` | string (uuid) | Evet | — |
| `crop_period_id` | string (uuid) | null | Evet | — |
| `task_id` | string (uuid) | null | Evet | — |
| `created_by_id` | string (uuid) | null | Evet | — |
| `activity_type` | [ActivityType](#schema-activitytype) | Evet | — |
| `status` | [ActivityStatus](#schema-activitystatus) | Evet | — |
| `source` | [ActivitySource](#schema-activitysource) | Evet | — |
| `description` | string | Evet | — |
| `occurred_at` | string (date-time) | Evet | — |
| `duration_minutes` | integer | null | Evet | — |
| `amount` | number | null | Evet | — |
| `unit` | string | null | Evet | — |
| `photo_url` | string | null | Evet | — |
| `voice_url` | string | null | Evet | — |
| `voice_transcript` | string | null | Evet | — |
| `performed_by` | string | null | Evet | — |
| `cost` | number | null | Evet | — |
| `confirmed_at` | string (date-time) | null | Evet | — |
| `archived_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |

<a id="schema-activityrevisionresponse"></a>
### `ActivityRevisionResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `activity_id` | string (uuid) | Evet | — |
| `changed_by_id` | string (uuid) | null | Evet | — |
| `previous_values` | object | Evet | — |
| `changed_at` | string (date-time) | Evet | — |

<a id="schema-activitysource"></a>
### `ActivitySource`

- **Tip:** `string`
- **Değerler:** `MANUAL`, `VOICE`, `TASK`

<a id="schema-activitystatus"></a>
### `ActivityStatus`

- **Tip:** `string`
- **Değerler:** `DRAFT`, `CONFIRMED`

<a id="schema-activitytype"></a>
### `ActivityType`

- **Tip:** `string`
- **Değerler:** `IRRIGATION`, `FERTILIZATION`, `SPRAYING`, `PRUNING`, `FIELD_CHECK`, `HARVEST`, `OTHER`

<a id="schema-activityupdate"></a>
### `ActivityUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `activity_type` | [ActivityType](#schema-activitytype) | null | Hayır | — |
| `description` | string | null | Hayır | — |
| `occurred_at` | string (date-time) | null | Hayır | — |
| `crop_period_id` | string (uuid) | null | Hayır | — |
| `duration_minutes` | integer | null | Hayır | — |
| `amount` | number | null | Hayır | — |
| `unit` | string | null | Hayır | — |
| `photo_url` | string | null | Hayır | — |
| `voice_url` | string | null | Hayır | — |
| `voice_transcript` | string | null | Hayır | — |
| `performed_by` | string | null | Hayır | — |
| `cost` | number | null | Hayır | — |

<a id="schema-body-upload-media-api-v1-media-post"></a>
### `Body_upload_media_api_v1_media_post`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `file` | string (binary) | Evet | — |

<a id="schema-casecategory"></a>
### `CaseCategory`

- **Tip:** `string`
- **Değerler:** `DISEASE`, `PEST`, `IRRIGATION`, `NUTRITION`, `WEATHER`, `OTHER`

<a id="schema-casecreate"></a>
### `CaseCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `client_operation_id` | string (uuid) | null | Hayır | — |
| `farm_id` | string (uuid) | Evet | — |
| `category` | [CaseCategory](#schema-casecategory) | Evet | — |
| `title` | string | Evet | min uzunluk: `2`; maks uzunluk: `160` |
| `description` | string | Evet | min uzunluk: `2`; maks uzunluk: `6000` |
| `media_ids` | array<string (uuid)> | Hayır | maks öğe: `10` |

<a id="schema-casedetailresponse"></a>
### `CaseDetailResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `farm_id` | string (uuid) | Evet | — |
| `farm_name` | string | Evet | — |
| `farmer_name` | string | Evet | — |
| `created_by_id` | string (uuid) | Evet | — |
| `assigned_expert_id` | string (uuid) | null | Evet | — |
| `category` | [CaseCategory](#schema-casecategory) | Evet | — |
| `priority` | [CasePriority](#schema-casepriority) | Evet | — |
| `status` | [CaseStatus](#schema-casestatus) | Evet | — |
| `title` | string | Evet | — |
| `description` | string | Evet | — |
| `media` | array<[MediaAssetResponse](#schema-mediaassetresponse)> | Evet | — |
| `closed_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |
| `messages` | array<[CaseMessageResponse](#schema-casemessageresponse)> | Evet | — |

<a id="schema-caselistresponse"></a>
### `CaseListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[CaseSummaryResponse](#schema-casesummaryresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-casemessagecreate"></a>
### `CaseMessageCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `client_operation_id` | string (uuid) | null | Hayır | — |
| `message_type` | [CaseMessageType](#schema-casemessagetype) | Hayır | varsayılan: `COMMENT` |
| `body` | string | Evet | min uzunluk: `2`; maks uzunluk: `6000` |
| `media_ids` | array<string (uuid)> | Hayır | maks öğe: `10` |

<a id="schema-casemessageresponse"></a>
### `CaseMessageResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `case_id` | string (uuid) | Evet | — |
| `sender_id` | string (uuid) | Evet | — |
| `sender_name` | string | Evet | — |
| `sender_role` | [UserRole](#schema-userrole) | Evet | — |
| `message_type` | [CaseMessageType](#schema-casemessagetype) | Evet | — |
| `body` | string | Evet | — |
| `media` | array<[MediaAssetResponse](#schema-mediaassetresponse)> | Evet | — |
| `created_at` | string (date-time) | Evet | — |

<a id="schema-casemessagetype"></a>
### `CaseMessageType`

- **Tip:** `string`
- **Değerler:** `COMMENT`, `ADDITIONAL_INFO_REQUEST`, `EXPERT_RESPONSE`

<a id="schema-casepriority"></a>
### `CasePriority`

- **Tip:** `string`
- **Değerler:** `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`

<a id="schema-casestatus"></a>
### `CaseStatus`

- **Tip:** `string`
- **Değerler:** `OPEN`, `IN_REVIEW`, `WAITING_FARMER`, `ANSWERED`, `CLOSED`

<a id="schema-casestatusupdate"></a>
### `CaseStatusUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `status` | [CaseStatus](#schema-casestatus) | Evet | — |
| `priority` | [CasePriority](#schema-casepriority) | null | Hayır | — |
| `assign_to_me` | boolean | Hayır | varsayılan: `False` |

<a id="schema-casesummaryresponse"></a>
### `CaseSummaryResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `farm_id` | string (uuid) | Evet | — |
| `farm_name` | string | Evet | — |
| `farmer_name` | string | Evet | — |
| `created_by_id` | string (uuid) | Evet | — |
| `assigned_expert_id` | string (uuid) | null | Evet | — |
| `category` | [CaseCategory](#schema-casecategory) | Evet | — |
| `priority` | [CasePriority](#schema-casepriority) | Evet | — |
| `status` | [CaseStatus](#schema-casestatus) | Evet | — |
| `title` | string | Evet | — |
| `description` | string | Evet | — |
| `media` | array<[MediaAssetResponse](#schema-mediaassetresponse)> | Evet | — |
| `closed_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |

<a id="schema-cropperiodclose"></a>
### `CropPeriodClose`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `harvested_at` | string (date) | Hayır | — |

<a id="schema-cropperiodcreate"></a>
### `CropPeriodCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `crop_type` | [CropType](#schema-croptype) | Evet | — |
| `variety` | string | null | Hayır | — |
| `planted_at` | string (date) | Evet | — |
| `close_existing` | boolean | Hayır | varsayılan: `False` |

<a id="schema-cropperiodlistresponse"></a>
### `CropPeriodListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[CropPeriodResponse](#schema-cropperiodresponse)> | Evet | — |

<a id="schema-cropperiodresponse"></a>
### `CropPeriodResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `farm_id` | string (uuid) | Evet | — |
| `crop_type` | [CropType](#schema-croptype) | Evet | — |
| `variety` | string | null | Evet | — |
| `planted_at` | string (date) | Evet | — |
| `harvested_at` | string (date) | null | Evet | — |
| `status` | [CropPeriodStatus](#schema-cropperiodstatus) | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |

<a id="schema-cropperiodstatus"></a>
### `CropPeriodStatus`

- **Tip:** `string`
- **Değerler:** `ACTIVE`, `ARCHIVED`

<a id="schema-croptype"></a>
### `CropType`

- **Tip:** `string`
- **Değerler:** `WHEAT`, `BARLEY`, `CORN`, `SUNFLOWER`, `TOMATO`

<a id="schema-dailytasklistresponse"></a>
### `DailyTaskListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `date` | string (date) | Evet | — |
| `items` | array<[TaskResponse](#schema-taskresponse)> | Evet | — |
| `critical_weather_alerts` | array<[TaskResponse](#schema-taskresponse)> | Evet | — |
| `overdue` | array<[TaskResponse](#schema-taskresponse)> | Evet | — |
| `visible_limit` | integer | Hayır | varsayılan: `3` |

<a id="schema-deviceplatform"></a>
### `DevicePlatform`

- **Tip:** `string`
- **Değerler:** `ANDROID`, `IOS`, `WEB`

<a id="schema-devicetokenregister"></a>
### `DeviceTokenRegister`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `token` | string | Evet | min uzunluk: `16`; maks uzunluk: `512` |
| `platform` | [DevicePlatform](#schema-deviceplatform) | Evet | — |

<a id="schema-devicetokenresponse"></a>
### `DeviceTokenResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `user_id` | string (uuid) | Evet | — |
| `platform` | [DevicePlatform](#schema-deviceplatform) | Evet | — |
| `active` | boolean | Evet | — |
| `last_seen_at` | string (date-time) | Evet | — |
| `created_at` | string (date-time) | Evet | — |

<a id="schema-expertresponsecreate"></a>
### `ExpertResponseCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `client_operation_id` | string (uuid) | null | Hayır | — |
| `body` | string | Evet | min uzunluk: `2`; maks uzunluk: `6000` |
| `media_ids` | array<string (uuid)> | Hayır | maks öğe: `10` |
| `close_case` | boolean | Hayır | varsayılan: `False` |

<a id="schema-farmcreate"></a>
### `FarmCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `name` | string | Evet | min uzunluk: `2`; maks uzunluk: `120` |
| `latitude` | number | Evet | min: `-90.0`; maks: `90.0` |
| `longitude` | number | Evet | min: `-180.0`; maks: `180.0` |
| `size_in_hectares` | number | null | Hayır | — |
| `irrigation_method` | [IrrigationMethod](#schema-irrigationmethod) | null | Hayır | — |
| `soil_type` | string | null | Hayır | — |
| `note` | string | null | Hayır | — |
| `crop_type` | [CropType](#schema-croptype) | Evet | — |
| `variety` | string | null | Hayır | — |
| `planted_at` | string (date) | Evet | — |

<a id="schema-farmjournalresponse"></a>
### `FarmJournalResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[JournalEntryResponse](#schema-journalentryresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-farmlistresponse"></a>
### `FarmListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[FarmResponse](#schema-farmresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-farmmutationresponse"></a>
### `FarmMutationResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `farm` | [FarmResponse](#schema-farmresponse) | Evet | — |
| `warnings` | array<string> | Hayır | — |

<a id="schema-farmresponse"></a>
### `FarmResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `owner_id` | string (uuid) | Evet | — |
| `name` | string | Evet | — |
| `latitude` | number | null | Evet | — |
| `longitude` | number | null | Evet | — |
| `size_in_hectares` | number | null | Evet | — |
| `irrigation_method` | [IrrigationMethod](#schema-irrigationmethod) | null | Evet | — |
| `soil_type` | string | null | Evet | — |
| `note` | string | null | Evet | — |
| `archived_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |
| `current_crop` | [CropPeriodResponse](#schema-cropperiodresponse) | null | Evet | — |

<a id="schema-farmupdate"></a>
### `FarmUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `name` | string | null | Hayır | — |
| `latitude` | number | null | Hayır | — |
| `longitude` | number | null | Hayır | — |
| `size_in_hectares` | number | null | Hayır | — |
| `irrigation_method` | [IrrigationMethod](#schema-irrigationmethod) | null | Hayır | — |
| `soil_type` | string | null | Hayır | — |
| `note` | string | null | Hayır | — |

<a id="schema-farmweatherresponse"></a>
### `FarmWeatherResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `farm_id` | string (uuid) | Evet | — |
| `provider` | string | Evet | — |
| `fetched_at` | string (date-time) | Evet | — |
| `is_stale` | boolean | Evet | — |
| `stale_reason` | string | null | Evet | — |
| `points` | array<[WeatherPointResponse](#schema-weatherpointresponse)> | Evet | — |
| `risks` | array<[WeatherRiskResponse](#schema-weatherriskresponse)> | Evet | — |

<a id="schema-feedbackstatus"></a>
### `FeedbackStatus`

- **Tip:** `string`
- **Değerler:** `OPEN`, `REVIEWED`, `RESOLVED`

<a id="schema-feedbacktype"></a>
### `FeedbackType`

- **Tip:** `string`
- **Değerler:** `WEEKLY_CHECKIN`, `FALSE_ALERT`, `BUG`, `SUGGESTION`

<a id="schema-httpvalidationerror"></a>
### `HTTPValidationError`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `detail` | array<[ValidationError](#schema-validationerror)> | Hayır | — |

<a id="schema-irrigationmethod"></a>
### `IrrigationMethod`

- **Tip:** `string`
- **Değerler:** `DRIP`, `SPRINKLER`, `FLOOD`, `RAINFED`, `OTHER`

<a id="schema-journalentryresponse"></a>
### `JournalEntryResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `entry_type` | string | Evet | — |
| `id` | string (uuid) | Evet | — |
| `occurred_at` | string (date-time) | Evet | — |
| `title` | string | Evet | — |
| `description` | string | Evet | — |
| `metadata` | object | Evet | — |

<a id="schema-mediaassetresponse"></a>
### `MediaAssetResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `owner_id` | string (uuid) | Evet | — |
| `kind` | [MediaKind](#schema-mediakind) | Evet | — |
| `original_name` | string | Evet | — |
| `content_type` | string | Evet | — |
| `size_bytes` | integer | Evet | — |
| `checksum_sha256` | string | Evet | — |
| `url` | string | Evet | — |
| `created_at` | string (date-time) | Evet | — |

<a id="schema-mediakind"></a>
### `MediaKind`

- **Tip:** `string`
- **Değerler:** `IMAGE`, `AUDIO`

<a id="schema-notificationlistresponse"></a>
### `NotificationListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[NotificationResponse](#schema-notificationresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `unread` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-notificationresponse"></a>
### `NotificationResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `user_id` | string (uuid) | Evet | — |
| `notification_type` | [NotificationType](#schema-notificationtype) | Evet | — |
| `title` | string | Evet | — |
| `body` | string | Evet | — |
| `deep_link` | string | Evet | — |
| `data` | object | Evet | — |
| `status` | [NotificationStatus](#schema-notificationstatus) | Evet | — |
| `attempt_count` | integer | Evet | — |
| `last_error` | string | null | Evet | — |
| `sent_at` | string (date-time) | null | Evet | — |
| `read_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |

<a id="schema-notificationstatus"></a>
### `NotificationStatus`

- **Tip:** `string`
- **Değerler:** `PENDING`, `SENT`, `FAILED`

<a id="schema-notificationtype"></a>
### `NotificationType`

- **Tip:** `string`
- **Değerler:** `TASK_ASSIGNED`, `CRITICAL_WEATHER`, `EXPERT_RESPONSE`

<a id="schema-pilotfeedbackcreate"></a>
### `PilotFeedbackCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `feedback_type` | [FeedbackType](#schema-feedbacktype) | Evet | — |
| `rating` | integer | null | Hayır | — |
| `comment` | string | Evet | min uzunluk: `2`; maks uzunluk: `6000` |
| `related_task_id` | string (uuid) | null | Hayır | — |
| `related_case_id` | string (uuid) | null | Hayır | — |

<a id="schema-pilotfeedbacklistresponse"></a>
### `PilotFeedbackListResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `items` | array<[PilotFeedbackResponse](#schema-pilotfeedbackresponse)> | Evet | — |
| `total` | integer | Evet | — |
| `limit` | integer | Evet | — |
| `offset` | integer | Evet | — |

<a id="schema-pilotfeedbackresponse"></a>
### `PilotFeedbackResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `created_by_id` | string (uuid) | Evet | — |
| `created_by_name` | string | Evet | — |
| `feedback_type` | [FeedbackType](#schema-feedbacktype) | Evet | — |
| `status` | [FeedbackStatus](#schema-feedbackstatus) | Evet | — |
| `rating` | integer | null | Evet | — |
| `comment` | string | Evet | — |
| `related_task_id` | string (uuid) | null | Evet | — |
| `related_case_id` | string (uuid) | null | Evet | — |
| `reviewed_by_id` | string (uuid) | null | Evet | — |
| `reviewed_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |

<a id="schema-pilotfeedbackupdate"></a>
### `PilotFeedbackUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `status` | [FeedbackStatus](#schema-feedbackstatus) | Evet | — |

<a id="schema-pilotmetricsresponse"></a>
### `PilotMetricsResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `window_days` | integer | Evet | — |
| `active_farmers` | integer | Evet | — |
| `tasks_created` | integer | Evet | — |
| `tasks_completed` | integer | Evet | — |
| `task_completion_rate` | number | Evet | — |
| `critical_weather_alerts` | integer | Evet | — |
| `false_alerts` | integer | Evet | — |
| `false_alert_rate` | number | Evet | — |
| `cases_created` | integer | Evet | — |
| `cases_answered` | integer | Evet | — |
| `average_expert_response_minutes` | number | null | Evet | — |
| `notifications_created` | integer | Evet | — |
| `notifications_sent` | integer | Evet | — |
| `notification_delivery_rate` | number | Evet | — |
| `feedback_count` | integer | Evet | — |
| `average_feedback_rating` | number | null | Evet | — |

<a id="schema-profileupdate"></a>
### `ProfileUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `full_name` | string | Evet | min uzunluk: `2`; maks uzunluk: `120` |
| `province` | string | Evet | min uzunluk: `2`; maks uzunluk: `80` |
| `district` | string | Evet | min uzunluk: `2`; maks uzunluk: `80` |
| `terms_accepted` | boolean | Evet | — |
| `notifications_enabled` | boolean | Hayır | varsayılan: `True` |

<a id="schema-refreshtokenrequest"></a>
### `RefreshTokenRequest`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `refresh_token` | string | Evet | min uzunluk: `32` |

<a id="schema-requestotprequest"></a>
### `RequestOtpRequest`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `phone_number` | string | Evet | — |

<a id="schema-requestotpresponse"></a>
### `RequestOtpResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `message` | string | Hayır | varsayılan: `Doğrulama kodu gönderildi.` |
| `expires_in` | integer | Evet | — |
| `debug_otp` | string | null | Hayır | Yalnızca açıkça etkinleştirilen yerel geliştirme ortamında döner. |

<a id="schema-taskcompleterequest"></a>
### `TaskCompleteRequest`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `note` | string | null | Hayır | — |
| `photo_url` | string | null | Hayır | — |

<a id="schema-taskconfidence"></a>
### `TaskConfidence`

- **Tip:** `string`
- **Değerler:** `LOW`, `MEDIUM`, `HIGH`

<a id="schema-taskcreate"></a>
### `TaskCreate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `title` | string | Evet | min uzunluk: `2`; maks uzunluk: `160` |
| `description` | string | Evet | min uzunluk: `2`; maks uzunluk: `4000` |
| `reason` | string | Evet | min uzunluk: `2`; maks uzunluk: `2000` |
| `priority` | [TaskPriority](#schema-taskpriority) | Hayır | varsayılan: `HIGH` |
| `confidence` | [TaskConfidence](#schema-taskconfidence) | Hayır | varsayılan: `HIGH` |
| `due_date` | string (date) | Hayır | — |
| `crop_period_id` | string (uuid) | null | Hayır | — |

<a id="schema-taskpriority"></a>
### `TaskPriority`

- **Tip:** `string`
- **Değerler:** `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`

<a id="schema-taskresponse"></a>
### `TaskResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `farm_id` | string (uuid) | Evet | — |
| `crop_period_id` | string (uuid) | null | Evet | — |
| `created_by_id` | string (uuid) | null | Evet | — |
| `title` | string | Evet | — |
| `description` | string | Evet | — |
| `reason` | string | Evet | — |
| `priority` | [TaskPriority](#schema-taskpriority) | Evet | — |
| `status` | [TaskStatus](#schema-taskstatus) | Evet | — |
| `source` | [TaskSource](#schema-tasksource) | Evet | — |
| `confidence` | [TaskConfidence](#schema-taskconfidence) | Evet | — |
| `due_date` | string (date) | Evet | — |
| `not_applied_reason` | string | null | Evet | — |
| `completion_note` | string | null | Evet | — |
| `photo_url` | string | null | Evet | — |
| `viewed_at` | string (date-time) | null | Evet | — |
| `completed_at` | string (date-time) | null | Evet | — |
| `created_at` | string (date-time) | Evet | — |
| `updated_at` | string (date-time) | Evet | — |
| `expert_review_recommended` | boolean | Evet | — |

<a id="schema-tasksource"></a>
### `TaskSource`

- **Tip:** `string`
- **Değerler:** `SYSTEM`, `CROP_CALENDAR`, `WEATHER`, `EXPERT`

<a id="schema-taskstatus"></a>
### `TaskStatus`

- **Tip:** `string`
- **Değerler:** `NEW`, `VIEWED`, `PLANNED`, `COMPLETED`, `NOT_APPLIED`, `OVERDUE`, `CANCELLED`

<a id="schema-taskstatusupdate"></a>
### `TaskStatusUpdate`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `status` | [TaskStatus](#schema-taskstatus) | Evet | — |
| `not_applied_reason` | string | null | Hayır | — |
| `note` | string | null | Hayır | — |
| `photo_url` | string | null | Hayır | — |

<a id="schema-tokenresponse"></a>
### `TokenResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `access_token` | string | Evet | — |
| `refresh_token` | string | Evet | — |
| `token_type` | string | Hayır | varsayılan: `bearer` |
| `expires_in` | integer | Evet | — |
| `user` | [UserResponse](#schema-userresponse) | Evet | — |

<a id="schema-userresponse"></a>
### `UserResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `id` | string (uuid) | Evet | — |
| `phone_number` | string | Evet | — |
| `full_name` | string | null | Evet | — |
| `province` | string | null | Evet | — |
| `district` | string | null | Evet | — |
| `role` | [UserRole](#schema-userrole) | Evet | — |
| `terms_accepted` | boolean | Evet | — |
| `notifications_enabled` | boolean | Evet | — |
| `profile_complete` | boolean | Evet | — |

<a id="schema-userrole"></a>
### `UserRole`

- **Tip:** `string`
- **Değerler:** `FARMER`, `AGRONOMIST`

<a id="schema-validationerror"></a>
### `ValidationError`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `loc` | array<string | integer> | Evet | — |
| `msg` | string | Evet | — |
| `type` | string | Evet | — |

<a id="schema-verifyotprequest"></a>
### `VerifyOtpRequest`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `phone_number` | string | Evet | — |
| `otp_code` | string | Evet | — |

<a id="schema-weatherpointresponse"></a>
### `WeatherPointResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `observed_at` | string (date-time) | Evet | — |
| `temperature_c` | number | null | Evet | — |
| `precipitation_probability` | number | null | Evet | — |
| `precipitation_mm` | number | null | Evet | — |
| `wind_speed_kmh` | number | null | Evet | — |

<a id="schema-weatherriskresponse"></a>
### `WeatherRiskResponse`

| Alan | Tip | Zorunlu | Kurallar / Açıklama |
|---|---|---|---|
| `risk_type` | string | Evet | — |
| `severity` | string | Evet | — |
| `starts_at` | string (date-time) | Evet | — |
| `ends_at` | string (date-time) | Evet | — |
| `message` | string | Evet | — |
| `suggested_action` | string | Evet | — |

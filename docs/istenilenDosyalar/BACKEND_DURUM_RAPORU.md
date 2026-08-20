# Backend Mevcut Durum Raporu

Tarih: 20 Ağustos 2026

## Endpoint durumu

| Alan | Kaynak kodu | Yerel test | Staging deploy |
| --- | --- | --- | --- |
| Medya | Mevcut: `/api/v1/media` | Mevcut | Doğrulanmadı |
| Vaka | Mevcut: `/api/v1/cases` | Mevcut | Doğrulanmadı |
| Vaka mesajlaşması | Mevcut: `/api/v1/cases/{case_id}/messages` ve uzman yanıtı | Mevcut | Doğrulanmadı |
| Bildirim | Mevcut: `/api/v1/notifications` | Mevcut | Doğrulanmadı |
| Pilot geri bildirim/metrik | Mevcut: `/api/v1/pilot` | Mevcut | Doğrulanmadı |
| Liveness | Mevcut: `/health/live` | Mevcut | Doğrulanmadı |
| Readiness | Mevcut: `/health/ready` | Mevcut | Doğrulanmadı |
| AI sohbet | Mevcut: `/api/v1/ai/chat` | Mevcut | Doğrulanmadı |

Router kayıtları `backend/app/main.py` içinde aktiftir. Endpointlerin kaynak kodda ve testlerde bulunması, internetten erişilen bir staging ortamına deploy edildiklerini kanıtlamaz.

## Entegrasyonların gerçek durumu

### Telefon doğrulama

- Yeni mobil akış Firebase Phone Authentication kullanır ve kullanıcı tarafından Firebase Console'da etkinleştirilmiştir.
- Flutter, Firebase ID tokenını FastAPI'ye Bearer token olarak gönderir.
- Eski FastAPI `/auth/request-otp` ve `/auth/verify-otp` endpointleri geri dönüş amacıyla durur.
- Eski FastAPI OTP akışında Redis doğrulama kodu saklama/deneme sınırı vardır; dışarı SMS gönderen gerçek bir SMS sağlayıcısı yoktur.

### Medya depolama

- `local` ve Cloudflare R2 sağlayıcıları geliştirilmiştir.
- R2 bağlantısı yerel ortamda doğrulanmıştır; staging deploy kanıtı yoktur.
- JPEG/PNG yükleme sınırı yapılandırmayla, varsayılan 5 MB olarak uygulanır.

### FCM

- Firebase Admin SDK kullanan FCM sağlayıcısı geliştirilmiştir.
- Cihaz tokenı kaydetme/silme ve bildirim kayıtları mevcuttur.
- Servis hesabı Git'e eklenmez.
- Gerçek cihaz teslim testi/staging dağıtımı bu teslim kapsamında doğrulanmamıştır.

### Offline tekrar önleme

- `client_operation_id` tabanlı tekrar önleme vaka ve bazı faaliyet oluşturma işlemlerinde uygulanmıştır.
- `Idempotency-Key` CORS başlıklarında izinli olmasına rağmen routerlarda işlenmemektedir.
- Mobil Firestore tarla/faaliyet yazıları Firestore çevrimdışı kuyruğu ve belge kimliği sayesinde tekrar güvenliğine sahiptir.
- Tüm POST/PATCH endpointleri için genel bir idempotency sözleşmesi yoktur.

### Çiftçi–uzman yetkilendirmesi

- Roller `FARMER` ve `AGRONOMIST` olarak tanımlıdır.
- Çiftçi yalnızca kendi tarlası/vakası üzerinden işlem yapar.
- Uzman vaka üstlenebilir, uzman yanıtı yazabilir ve izin verilen uzman akışlarını çalıştırabilir.
- Yetki ilişkisi ayrı bir çiftçi–uzman eşleştirme tablosu değil; vaka sahipliği, atanmış uzman ve rol kontrolleriyle uygulanır.

### Hesap silme

- Kullanıcı hesabını tamamen silen endpoint mevcut değildir.
- Tarla/faaliyet arşivleme/silme özellikleri hesap silme yerine geçmez.
- Firebase Auth kullanıcısı, Firestore profili ve PostgreSQL kayıtlarını birlikte silen KVKK uyumlu iş akışı eksiktir.

## Staging

- Güncel ve doğrulanmış staging URL'si kaynak depoda yoktur.
- `compose.staging.yaml` yalnızca override dosyasıdır; tek başına kullanılmaz.
- Deploy sonrası zorunlu kanıtlar: `/health/live`, `/health/ready`, Firebase telefon girişi, AI Bearer token çağrısı, R2 medya ve FCM gerçek cihaz testi.

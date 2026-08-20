# Eksikler ve Aksiyonlar

## Yayına çıkmadan önce zorunlu

1. Gerçek staging URL'sini oluştur, TLS ile yayınla ve URL'yi bu pakete ekle.
2. Alembic migrationlarını staging veritabanına uygula.
3. Firestore Rules ve indeksleri deploy et; iki kullanıcıyla sahiplik testini tekrar çalıştır.
4. Staging backendinde `FIREBASE_AUTH_ENABLED=true` kullan ve containerı yeniden oluştur.
5. Fiziksel Android cihazda gerçek SMS, çevrimdışı yazma, AI, R2 ve FCM teslim testini yap.
6. Release keystore SHA-1/SHA-256 değerlerini Firebase Android uygulamasına ekle.
7. Mevcut `AGRONOMIST` hesaplarının Firebase UID eşlemesini otomatik telefon eşleşmesiyle yapma; operatör onaylı ve denetim kayıtlı bir uzman bağlama akışı eklenene kadar backend Firebase doğrulamasını uzman staging hesabında açma.

## Ürün/backlog eksikleri

- FastAPI legacy OTP için dış SMS sağlayıcısı yoktur; yeni mobil Firebase Phone Auth kullanır.
- Hesap silme ve bağlı verileri anonimleştirme/silme iş akışı yoktur.
- Genel `Idempotency-Key` middleware/sözleşmesi yoktur.
- Hata yanıtları tek bir standart envelope altında değildir.
- OpenAPI ile deploy edilen staging şemasının otomatik karşılaştırıldığı CI gate yoktur.
- Staging ve production URL'leri/deploy kanıtları depoda yoktur.
- Mevcut PostgreSQL `AGRONOMIST` hesabını doğrulanmış aynı telefonla otomatik Firebase UID'ye bağlayan kod, operatör onayı/audit kaydı olmadığı için güvenlik incelemesinde açık Important bulgu olarak kalmıştır. `FIREBASE_AUTH_ENABLED` varsayılanı `false` tutulmalıdır; çiftçi dışı hesap geçişi ayrıca tamamlanmalıdır.
- Flutter `analyze`, bu Windows çalışma yolunda LSP JSON kesilmesi nedeniyle kaynak teşhisi üretmeden kapanmaktadır; ASCII karakterli temiz CI/workspace üzerinde tekrar çalıştırılmalıdır.

## Güvenlik notu

Servis hesabı, `.env`, `google-services.json`, API anahtarları, gerçek telefon numaraları ve tokenlar bu teslim paketine eklenmemiştir.

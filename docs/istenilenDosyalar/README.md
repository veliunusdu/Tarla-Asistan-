# Mobil Backend Teslim Paketi

Bu klasör, mobil ekipten gelen backend sözleşmesi ve mevcut durum sorularına kaynak kodu esas alınarak hazırlanmıştır.

## Dosyalar

- `BACKEND_DURUM_RAPORU.md`: İstenen servislerin geliştirme, yapılandırma ve deploy durumu.
- `AI_CHAT_API_SOZLESMESI.md`: `POST /api/v1/ai/chat` kesin mobil sözleşmesi.
- `MOBIL_DTO_NOTLARI.md`: Hata, nullable, enum, tarih ve pagination kuralları.
- `EKSIKLER_VE_AKSIYONLAR.md`: Eksik veya dış ortamda doğrulanmamış maddeler.
- `openapi.json`: Kaynak depodaki güncel FastAPI OpenAPI çıktısının kopyası.
- `API_DOCUMENTATION.md`: İnsan tarafından okunabilir ayrıntılı API belgesinin kopyası.

## Kritik ortam bilgisi

Kaynak kodunda doğrulanmış bir staging alan adı/URL bulunmamaktadır. Staging deploy işlemi bu çalışma sırasında yapılmamıştır. Bu nedenle mobil ekip, URL paylaşılana ve `/health/ready` ile doğrulanana kadar yerel adres kullanmalıdır.

Yerel API tabanı: `http://localhost:8000/api/v1` veya fiziksel cihaz için bilgisayarın LAN IP adresi.

## Doğrulama özeti

- Backend: 64 test geçti; Ruff temiz.
- Flutter: 31 test geçti.
- Firestore Rules emülatörü: 4 test geçti.
- Firebase projesi: `demo2-c4265`.
- Firestore: Enterprise Native, `tarla-asistani`, `europe-west3`.
- Flutter statik analiz aracı, çalışma yolu/iletişim katmanındaki LSP JSON kesilmesi nedeniyle kaynak teşhisi üretmeden kapandı; bu durum ayrıca eksikler dosyasında kayıtlıdır.

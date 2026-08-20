# Mobil DTO Notları

Kesin alan listeleri ve endpoint bazlı şemalar için aynı klasördeki `openapi.json` esas alınmalıdır.

## Genel kurallar

- UUID alanları JSON'da stringdir.
- `date` alanları `YYYY-MM-DD` biçimindedir.
- `date-time` alanları ISO 8601 ve saat dilimi bilgili biçimdedir; örnek: `2026-08-20T10:15:30Z`.
- Nullable alanlar OpenAPI şemasında `null` kabul eden birleşim olarak gösterilir.
- Eksik opsiyonel alan ile açık `null` aynı varsayılmamalıdır.
- Liste endpointleri çoğunlukla `limit` ve `offset` kullanır. Varsayılan `limit=50`, üst sınır `100`, `offset=0`dır.
- Sayfalı yanıtlar genel olarak `items`, `total`, `limit`, `offset` alanlarını taşır.

## Önemli enum grupları

Enumların kesin değerleri `openapi.json` içindeki `components.schemas` bölümünden üretilmelidir. Kaynak model grupları:

- Kullanıcı: `FARMER`, `AGRONOMIST`.
- Vaka: kategori, öncelik, durum ve mesaj türü.
- Bildirim: platform, bildirim türü ve gönderim durumu.
- Pilot: geri bildirim türü ve inceleme durumu.
- Görev/faaliyet: öncelik, durum, kaynak ve faaliyet türü.
- Medya: medya türü.

## Hata sözleşmesi

FastAPI doğrulama hataları ile uygulama hataları tamamen tek bir zarf altında standartlaştırılmamıştır:

- Uygulama hataları çoğunlukla `{"detail": "..."}` döner.
- FastAPI `422` doğrulama yanıtında `detail` bir hata listesi olabilir.
- Firebase servis/yapılandırma sorunu `503`, doğrulanmış telefon çakışması `409` dönebilir.

Mobil DTO katmanında en az iki hata modeli desteklenmelidir: string `detail` ve doğrulama listesi `detail`.

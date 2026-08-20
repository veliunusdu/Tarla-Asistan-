# Tarla Asistanı Backend

FastAPI tabanlı modüler monolit. PostgreSQL kalıcı veriyi, Redis ise kısa ömürlü
OTP verisini tutar.

## Yerel çalıştırma

Kök dizinde `.env.example` dosyasını `.env` olarak kopyalayın ve sırları değiştirin:

```sh
docker compose up --build
```

- API: http://localhost:8000
- Swagger: http://localhost:8000/docs
- Health check: http://localhost:8000/health

Statik ve paylaşılabilir API çıktıları:

- `docs/api-docs.html`: aranabilir, tek dosyalık görsel doküman
- `docs/API_DOCUMENTATION.md`: tüm endpoint ve model ayrıntıları
- `docs/openapi.json`: istemci üretimi ve API araçları için OpenAPI 3 tanımı

Backend değişikliklerinden sonra kök dizinde dokümanları yenilemek için:

```sh
backend/.venv/Scripts/python scripts/generate_api_docs.py
```

Container başlarken `alembic upgrade head` otomatik çalışır. Temiz migration testi:

```sh
docker compose down -v
docker compose up --build
```

## Test

```sh
pip install -r requirements-dev.txt
pytest
ruff check app tests
```

`OTP_EXPOSE_IN_RESPONSE=true` yalnızca yerel geliştirme içindir. Staging ve
production ortamlarında kapalı tutulmalı, `request-otp` akışı gerçek SMS
sağlayıcısına bağlanmalıdır.

## Firebase Cloud Messaging (FCM)

Mobil uygulama Firebase tokenını `/api/v1/notifications/devices` adresine
kaydeder. Gerçek cihaz bildirimi için Firebase Console'dan bir servis hesabı
JSON dosyası oluşturun ve dosyayı `backend/secrets/firebase-service-account.json`
olarak koyun. Bu klasördeki JSON dosyaları git tarafından izlenmez.

Ardından `.env` dosyasında aşağıdakileri ayarlayın:

```env
PUSH_PROVIDER=firebase
FIREBASE_SERVICE_ACCOUNT_PATH=/run/secrets/firebase-service-account.json
FIREBASE_PROJECT_ID=Firebase-proje-kimliğiniz
```

Docker, `backend/secrets` klasörünü container içinde salt-okunur
`/run/secrets` adresine bağlar. Firebase geçersiz veya süresi dolmuş bir cihaz
tokenı döndürürse backend ilgili tokenı pasifleştirir; kullanıcı uygulamayı
yeniden açtığında mobil istemci güncel tokenı tekrar kaydeder.

## Hava durumu sağlayıcısı

Varsayılan `WEATHER_PROVIDER=open_meteo` adaptörü tarlanın koordinatlarına göre
saatlik tahmini normalize eder. Sağlayıcı değiştirilecekse aynı `WeatherProvider`
arayüzünü uygulayan yeni adaptör eklenir. Son başarılı yanıt veritabanında
saklanır; sağlayıcı kesintisinde API bu kaydı `is_stale=true` ve açıklayıcı bir
`stale_reason` ile döndürür. `WEATHER_STALE_AFTER_HOURS` varsayılan olarak 3'tür.

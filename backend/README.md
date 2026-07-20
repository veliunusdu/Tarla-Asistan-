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

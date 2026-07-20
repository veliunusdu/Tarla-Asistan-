# Tarla Asistanı Uzman Paneli

Next.js tabanlı ziraat mühendisi paneli. Telefon + OTP ile giriş yapar, JWT
oturumunu doğrular ve yalnızca `AGRONOMIST` rolüne izin verir.

Kök dizinden tüm sistemi çalıştırmak için:

```sh
docker compose up --build
```

Yerel uzman telefonu varsayılan olarak `+905551112233` değeridir. Yerel Compose
ortamında OTP giriş ekranında gösterilir. Production ortamında
`OTP_EXPOSE_IN_RESPONSE=false` kullanılmalıdır.

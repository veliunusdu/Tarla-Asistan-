# AI Chat API Sözleşmesi

## Endpoint

`POST /api/v1/ai/chat`

Kimlik doğrulama: Yeni mobil sürümde `Authorization: Bearer <Firebase ID token>`. Backend `FIREBASE_AUTH_ENABLED=false` çalıştırılan geri dönüş ortamında aynı endpoint legacy JWT doğrulamasına döner; iki token türü aynı anda kabul edilmez.

## JSON isteği

`Content-Type: application/json`

```json
{
  "message": "Yapraklarda sararma var, ne yapmalıyım?",
  "field_id": "f2bc159f-17e0-4cee-b06b-b08260697251",
  "conversation_id": null,
  "history": [
    {"role": "user", "content": "Mısır ekili."},
    {"role": "assistant", "content": "Sulama durumunu paylaşır mısınız?"}
  ]
}
```

`message` zorunludur. `field_id`, `conversation_id` ve `history` opsiyoneldir.

## Fotoğraflı istek

`Content-Type: multipart/form-data`

Alanlar:

- `message`: zorunlu metin.
- `photo`: opsiyonel JPEG/PNG, en fazla 5 MB.
- `field_id`: opsiyonel UUID metni.
- `conversation_id`: opsiyonel UUID metni.
- `history`: opsiyonel JSON dizisi metni.

DeepSeek metin sağlayıcısı fotoğrafı doğrudan desteklemiyorsa fotoğraflı istek `422` döner; istemci bu yanıtı kullanıcıya anlaşılır göstermelidir.

## Başarılı yanıt

```json
{
  "reply": "Önce sulama ve azot durumunu kontrol edin.",
  "conversation_id": "20c78746-670b-4449-9913-a58c55a376ba"
}
```

## Hatalar

- `401`: Geçersiz/süresi dolmuş kimlik tokenı.
- `403`: `field_id` başka kullanıcıya ait.
- `404`: Tarla bulunamadı.
- `413`: Fotoğraf boyutu sınırı aşıldı.
- `415`: Desteklenmeyen fotoğraf türü.
- `422`: Geçersiz alan/history veya sağlayıcının fotoğraf kabul etmemesi.
- `502`: AI sağlayıcısı geçersiz/başarısız yanıt verdi.
- `503`: AI/Firebase hizmeti yapılandırılmamış veya geçici olarak kullanılamıyor.

Hata gövdesinin ortak minimum biçimi:

```json
{"detail": "Kullanıcıya gösterilebilir hata açıklaması"}
```

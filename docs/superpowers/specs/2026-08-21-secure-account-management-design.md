# Güvenli Uzman Eşleme ve Hesap Silme Tasarımı

## Amaç

Bu çalışma iki güvenlik açığını kapatır:

1. Mevcut `AGRONOMIST` kullanıcıların yalnızca telefon eşleşmesiyle otomatik Firebase UID'ye bağlanmasını engellemek.
2. Firebase Auth, Enterprise Firestore ve PostgreSQL arasında tekrar çalıştırılabilir bir hesap kapatma ve kişisel veri anonimleştirme akışı sağlamak.

Bu tasarım yalnızca güvenli hesap yönetimini kapsar. Genel `Idempotency-Key`, standart hata zarfı ve staging dağıtımı ayrı alt projelerdir.

## İlkeler

- Uzman hesap bağlama açık operatör onayı olmadan gerçekleşmez.
- Çiftçi için doğrulanmış telefonla mevcut güvenli bağlama davranışı korunur.
- Kullanıcı silme isteği verildiği anda hesap yeni işlemlere kapatılır.
- Kişisel veriler silinir veya anonimleştirilir; zorunlu tarımsal/audit kayıtları anonim kullanıcıya bağlı kalır.
- Firebase, Firestore ve PostgreSQL tek atomik transaction paylaşmadığından işlem kalıcı bir iş kaydıyla idempotent ve tekrar çalıştırılabilir olur.
- Silme işlemi başarısız olduğunda veri erişimi yeniden açılmaz.
- Servis hesabı, kullanıcı tokenı ve gerçek kişisel veriler loglara veya hata gövdelerine yazılmaz.

## Uzman Firebase UID Eşleme

### Veri modeli

Yeni `firebase_link_approvals` tablosu:

- `id`: UUID, primary key.
- `user_id`: hedef PostgreSQL kullanıcısı; yalnız `AGRONOMIST` olabilir.
- `firebase_uid`: onaylanan Firebase UID, unique.
- `approved_by`: operatör tanımı, en fazla 120 karakter.
- `approved_at`: saat dilimli timestamp.
- `expires_at`: saat dilimli timestamp; varsayılan 24 saat.
- `consumed_at`: nullable saat dilimli timestamp.
- `created_at`: saat dilimli timestamp.

Aynı kullanıcı için yalnızca bir aktif, tüketilmemiş onay bulunabilir. Süresi dolmuş veya tüketilmiş onay kullanılamaz.

### Operatör komutu

Sunucu yönetim komutu:

```text
python -m app.manage approve-firebase-link \
  --user-id <postgres-user-uuid> \
  --firebase-uid <firebase-uid> \
  --operator <operator-label>
```

Komut hedef kullanıcının rolünü doğrular, UID'nin başka kullanıcıya bağlı olmadığını kontrol eder ve audit onayı oluşturur. Telefon veya token komut çıktısına yazılmaz.

### Giriş akışı

Firebase token doğrulandıktan sonra:

1. `firebase_uid` ile kullanıcı varsa normal devam edilir.
2. Doğrulanmış telefonla eşleşen kullanıcı `FARMER` ve UID alanı boşsa mevcut atomik bağlama uygulanır.
3. Eşleşen kullanıcı `AGRONOMIST` ise hedef kullanıcı ve token UID'si için geçerli onay aranır.
4. Onay yoksa `403` ve genel bir açıklama döner.
5. Onay varsa aynı transaction içinde kullanıcının UID'si atanır ve `consumed_at` yazılır.
6. Unique/race çakışmasında transaction geri alınır; mevcut doğru eşleme tekrar okunur, farklı eşleme `409` döndürür.

## Hesap Silme ve Anonimleştirme

### API

```text
POST /api/v1/users/me/deletion-request
Authorization: Bearer <Firebase ID token veya aktif legacy JWT>
Content-Type: application/json

{
  "confirmation": "HESABIMI SIL"
}
```

Başarılı kabul:

```json
{
  "request_id": "uuid",
  "status": "PENDING"
}
```

HTTP durumu `202 Accepted` olur. Aynı kullanıcının tekrar isteği mevcut iş kaydını döndürür.

### PostgreSQL modeli

`users` tablosuna:

- `account_status`: `ACTIVE`, `DELETION_PENDING`, `ANONYMIZED`.
- `deleted_at`: nullable saat dilimli timestamp.
- `anonymized_subject_id`: nullable, rastgele UUID/string; kişisel olmayan kayıtlarda yeni sahip kimliği.

Yeni `account_deletion_jobs` tablosu:

- `id`, `user_id`, `firebase_uid_snapshot`.
- `status`: `PENDING`, `PROCESSING`, `RETRY_REQUIRED`, `COMPLETED`.
- `attempt_count`, `last_error_code`, `next_retry_at`.
- `firebase_auth_deleted_at`, `firestore_anonymized_at`, `postgres_anonymized_at`.
- `created_at`, `updated_at`, `completed_at`.

Hata alanında sağlayıcı hata metni veya kişisel veri değil, sabit hata kodu saklanır.

### İşlem sırası

İstek transactionı:

1. Kullanıcı `DELETION_PENDING` yapılır.
2. Tüm legacy refresh session kayıtları iptal edilir.
3. Tekil silme işi oluşturulur veya mevcut iş döndürülür.
4. Transaction tamamlandıktan sonra arka plan işlemcisi tetiklenir.

İşlemci adımları:

1. Firebase refresh tokenları revoke edilir.
2. Named Enterprise Firestore `tarla-asistani` içinde kullanıcının `farms` belgeleri `anonymousOwnerId` ile anonimleştirilir; `ownerId` gerçek UID'den rastgele anonim özne değerine değiştirilir.
3. `users/{uid}` profili silinir.
4. Firebase Auth kullanıcısı silinir.
5. PostgreSQL kişisel alanları değiştirilir:
   - `phone_number`: unique ve geri döndürülemez `deleted-<random>` değeri.
   - `full_name`, `province`, `district`: `NULL`.
   - `firebase_uid`: `NULL`.
   - `notifications_enabled`: `false`.
   - `deleted_at`: işlem zamanı.
   - `account_status`: `ANONYMIZED`.
6. Aktif cihaz tokenları pasifleştirilir.
7. İş `COMPLETED` yapılır.

Vaka, mesaj, faaliyet, görev, medya metadatası ve audit kayıtları mevcut user satırına bağlı kalır; user satırı artık kişisel veri taşımaz. Medya nesnelerinin içeriği kişisel veri içerebileceği için bu ilk sürümde kullanıcıya ait medya nesneleri silinir, metadata kaydı anonim audit için tutulur.

### Tekrar deneme

- Her adım kendi tamamlanma timestampına bakar ve tamamlanan işi yeniden yapmaz.
- Geçici Firebase/Firestore hatasında iş `RETRY_REQUIRED` olur.
- Uygulama başlangıcında ve yönetim komutuyla bekleyen işler güvenli şekilde yeniden çalıştırılır.
- Maksimum otomatik deneme sonrası yönetim komutu gerekir; kullanıcı hesabı kapalı kalır.

## Yetkilendirme

- `get_current_user`, `ACTIVE` olmayan kullanıcıları `403` ile reddeder.
- Silme isteği yalnız kullanıcının kendisi için verilebilir.
- Uzman link onayı HTTP endpointi olarak sunulmaz; yalnız yönetim komutudur.
- Firestore Security Rules istemcinin `ownerId`, rol veya anonim sahip kimliği değiştirmesine izin vermez; anonimleştirme yalnız Admin SDK ile yapılır.

## Mobil Akış

- Ayarlar/Hesap ekranına “Hesabımı sil” eylemi eklenir.
- Kullanıcı ikinci onay ekranında `HESABIMI SIL` metnini girer.
- `202` sonrası yerel Firebase oturumu kapatılır, önbellekteki kişisel uygulama verisi temizlenir ve giriş ekranına dönülür.
- İşlem kısmi kaldığında kullanıcı tekrar giriş yapamaz; destek ekibi `request_id` üzerinden durumu görebilir.
- Silme işlemi tamamlandı mesajı, uygulamanın silinmiş hesaba push gönderememesi nedeniyle istek kabul ekranında beklenti olarak açıklanır.

## Hata Sözleşmesi

- `400`: Onay metni yanlış.
- `401`: Geçersiz token.
- `403`: Hesap aktif değil veya uzman link onayı yok.
- `409`: UID/telefon başka hesaba bağlı.
- `202`: Silme işi kabul edildi veya mevcut iş döndürüldü.
- `503`: Firebase/Firestore yapılandırması kullanılamıyor; silme işi kaydı oluşturulmuşsa hesap kapalı kalır ve iş retry edilir.

## Test Stratejisi

### Backend

- Onaysız uzman bağlama `403`.
- Süresi dolmuş/tüketilmiş onay reddi.
- Onaylı uzman bağlama ve tek kullanımlık tüketim.
- Aynı UID/user tekrar isteğinin idempotent olması.
- UID ve transaction yarışları.
- Çiftçi otomatik bağlamanın korunması.
- Silme onay metni, `202` idempotency ve oturum iptali.
- Firebase revoke/delete, Firestore anonimleştirme ve PostgreSQL anonimleştirme adımlarının ayrı hata/retry testleri.
- `DELETION_PENDING` ve `ANONYMIZED` kullanıcı erişim reddi.

### Firebase/Firestore

- Named database kullanımı.
- Admin anonimleştirmesinden sonra eski UID ile tarla erişim reddi.
- Profil silme ve medya nesnesi temizliği mock/emülatör doğrulaması.

### Mobil

- Onay metni zorunluluğu.
- `202` sonrası sign-out ve yerel kişisel önbellek temizliği.
- Hata ve retry mesajlarının Türkçe gösterimi.

## Operasyon ve Geri Dönüş

- Migration uygulanmadan özellik endpointi açılmaz.
- Uzman bağlama komutu ve silme işlemcisi dry-run seçeneği sunar.
- `FIREBASE_AUTH_ENABLED=false` geri dönüşü yeni Firebase girişlerini kapatır; daha önce `DELETION_PENDING` olan hesapları yeniden açmaz.
- Silme/anonimleştirme geri alınamaz; operatör komutu kullanıcıdan tekrar onay almadan hesabı aktifleştiremez.

## Kapsam Dışı

- Genel yönetim paneli.
- 30 günlük bekleme süresi.
- Bütün POST endpointleri için genel `Idempotency-Key`.
- Staging domain ve production deploy.

# Firebase Auth ve Firestore Geçiş Tasarımı

## Amaç

Tarla Asistanı mobil uygulamasında telefonla giriş için Firebase Authentication,
çiftçi profili, tarla ve faaliyet verileri için Cloud Firestore kullanılacaktır.
FastAPI; AI sohbet, R2 medya ve mevcut görev/vaka/uzman iş akışları için
çalışmaya devam eder.

Firebase projesi `demo2-c4265` olup Android uygulaması `TarlaAsistan` şu
kimlikle kayıtlıdır: `1:167065176851:android:b4fbe23246580cac2ca8e6`.
Android paket adı `com.tarlaasistani.pilot`dır.

## Hedef Mimari

```text
Flutter -> Firebase Phone Authentication -> Firebase ID token
Flutter -> Cloud Firestore (profil, tarla, faaliyet)
Flutter -> FastAPI (Firebase ID token ile) -> DeepSeek / R2 / görev-vaka akışları
```

Mobil uygulama Firebase ID tokenını FastAPI'nin korunan uçlarına Bearer token
olarak gönderir. FastAPI Firebase Admin SDK ile tokenı doğrular. Mevcut
PostgreSQL bağlı AI ve medya kayıtları için FastAPI, ilk erişimde Firebase UID
ile eşleştirilmiş minimal bir kullanıcı kaydı oluşturabilir.

## Kapsam

### Firebase Authentication

- Telefon numarasıyla SMS doğrulaması kullanılır.
- Mobil uygulamadaki mevcut FastAPI `request-otp` ve `verify-otp` çağrıları
  Firebase Auth çağrılarıyla değiştirilir.
- Başarılı ilk girişte kullanıcı Firestore profili oluşturulur.
- Yeni mobil sürüm eski FastAPI OTP akışını kullanmaz; endpointler geçiş
  dönemi boyunca silinmeden tutulur.

### Cloud Firestore

```text
users/{uid}
  phoneNumber, role: "FARMER", notificationsEnabled, createdAt, updatedAt

farms/{farmId}
  ownerId, name, latitude, longitude, sizeInHectares, cropType,
  irrigationMethod, plantedAt, createdAt, updatedAt

farms/{farmId}/activities/{activityId}
  activityType, description, occurredAt, inputMethod, createdAt
```

- İlk geçişte mevcut yerel geliştirme verisi taşınmaz; Firestore temiz başlar.
- Firestore çevrimdışı önbelleği tarla ve faaliyet verilerinin çevrimdışı
  kullanılmasını sağlar.
- Mevcut özel faaliyet senkronizasyon kuyruğu, Firestore akışı doğrulandıktan
  sonra kaldırılır.

### FastAPI'de Kalacak Alanlar

- DeepSeek tabanlı AI sohbet
- Cloudflare R2 medya depolama ve yetkili medya erişimi
- Görevler, vakalar, mesajlaşma ve uzman iş akışları
- Pilot metrikleri ve mevcut operasyonel uçlar

## Güvenlik

- Firestore Security Rules yalnızca doğrulanmış kullanıcıları kabul eder.
- Kullanıcı yalnızca kendi `users/{uid}` kaydını okuyup günceller.
- Tarla oluşturulurken `ownerId == request.auth.uid` zorunludur.
- Tarla sahibi değiştirilemez; kullanıcı yalnızca sahibi olduğu tarlaları
  okuyup günceller.
- Faaliyet erişimi, üst tarla belgesinin `ownerId` alanı üzerinden denetlenir.
- `role` istemci tarafından değiştirilemez; ilk aşamada Firestore kullanıcısı
  `FARMER` olarak oluşturulur.
- Firebase servis hesabı dosyaları, mobil istemciye veya Git'e eklenmez.

## Firebase Hazırlıkları

1. Firebase CLI ile `demo2-c4265` erişimi doğrulanır.
2. Android `google-services.json` dosyası CLI ile indirilir ve projeye eklenir.
3. Firebase Console'da Phone sağlayıcısı etkinleştirilir.
4. Android debug ve release SHA-1/SHA-256 parmak izleri Firebase projesine
   eklenir.
5. Var olan Firestore veritabanları listelenir; hedef örnek ve sürümü
   doğrulanır. Yoksa konum seçilerek yeni örnek oluşturulur.
6. Firestore Security Rules ve gerekli indeksler yayınlanır.

## Hata Yönetimi

- Telefon doğrulama hataları kullanıcıya Firebase hata durumuna uygun, anlaşılır
  Türkçe mesajlarla gösterilir.
- Firestore izin reddi kullanıcıyı oturum ve sahiplik sorununa yönlendirir;
  istemci hata ayrıntısını açığa çıkarmaz.
- Firestore bağlantısı yokken SDK yerel önbelleği kullanır; yazma işlemi tekrar
  bağlantı sağlandığında senkronize edilir.
- FastAPI, geçersiz veya süresi dolmuş Firebase ID tokenlarında `401` döndürür.

## Doğrulama

- Fiziksel Android cihazda Firebase Phone Auth ile SMS giriş testi
- İlk girişte Firestore profil oluşturma testi
- Tarla ve faaliyet oluşturma, sahiplik ve çevrimdışı senkronizasyon testleri
- İkinci kullanıcıyla başka çiftçinin verisine erişim reddi testi
- Firebase ID tokenıyla FastAPI AI ve medya çağrısı testi
- Flutter birim/widget testleri, Firestore Rules testleri ve FastAPI testleri

## Geri Dönüş

FastAPI, PostgreSQL ve Redis servisleri silinmez. Firebase akışında sorun
olursa mobil uygulamanın önceki sürümü ve mevcut FastAPI OTP akışı çalışmaya
devam eder. Veriler temiz Firestore başlangıcı nedeniyle ilk aşamada iki yönlü
migrasyon gerektirmez.

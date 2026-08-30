# Tarla Konumu ve Google Maps Tasarımı

## Amaç

Kullanıcı tarla eklerken konumunu tek seferlik izinle GPS'ten alabilmeli. Konum iznini reddederse veya GPS kullanmak istemezse Google Maps üzerinde nokta seçebilmeli. Koordinat backend'de tarlaya kaydedilmeli; hava durumu bu koordinattan alınmalı.

Arka planda konum takibi yapılmayacak. Konum yalnızca kullanıcı "Konumumu kullan" seçeneğine bastığında alınacak.

## Kullanıcı Akışı

1. Kullanıcı "Yeni Tarla Ekle" formunda ad, büyüklük, ürün ve ekim tarihini girer.
2. Konum bölümünde üç durum bulunur:
   - Konum seçilmedi: "Konumumu kullan" ve "Haritada seç" düğmeleri görünür.
   - Konum seçildi: koordinat özeti, "Haritada değiştir" ve "Konumu kaldır" işlemleri görünür.
   - Konum alınamadı: neden ve güvenli bir tekrar deneme seçeneği gösterilir.
3. "Konumumu kullan" yalnızca uygulama açıkken konum izni ister ve mevcut konumu alır.
4. İzin reddedilirse kullanıcıya haritada seçim sunulur; tarla konumsuz da kaydedilebilir.
5. "Haritada seç" Google Maps ekranını açar. Kullanıcı haritaya dokunur, işaretçiyi görür ve "Bu konumu kullan" ile onaylar.
6. Tarla backend'e koordinatla birlikte kaydedilir. Konum yoksa tarla yine kaydedilir, ancak ana sayfadaki hava kartı "Hava durumu için tarla konumu ekleyin" durumunu gösterir.

## Mimari

### Mobil

- `geolocator` konum servisi için eklenecek.
- `google_maps_flutter` yalnızca tarla konumu seçme ekranında kullanılacak.
- `LocationService` izin durumunu, GPS'in açık/kapalı oluşunu ve mevcut koordinatı tek bir arayüzden sağlayacak.
- `FieldLocationPickerScreen` harita, işaretçi ve onay işlemini kapsayacak.
- `TarlaEklemeEkrani` seçilen koordinatı form durumunda tutacak; koordinat alanlarını 0.0 ile doldurmayacak.
- Yeni backend uyumlu `TarlaRepository` adaptörü, mevcut `TarlaRepository` arayüzünü koruyarak tarla ekleme/listeleme işlerini `/api/v1/farms` üzerinden yapacak. Bu adaptör `AnaEkran`, tarla listesi ve tarla ekleme ekranına aktarılacak.

### Platform Yapılandırması

- Android: hassas ve yaklaşık konum izinleri eklenecek; Google Maps Android API anahtarı Android manifestte metadata olarak tanımlanacak.
- iOS: kullanım sırasında konum açıklaması eklenecek; Google Maps iOS API anahtarı uygulama başlangıcında yapılandırılacak.
- API anahtarları kaynak koduna ya da Git'e yazılmayacak. Yerel geliştirme ve CI/CD için gizli değişkenler kullanılacak.

### Backend

Mevcut `/api/v1/farms` oluşturma ve güncelleme endpointleri `latitude` ve `longitude` alanlarını zaten kabul ediyor. Bu çalışma yeni backend endpointi gerektirmez.

Hava endpointi, koordinatı olan ilk aktif tarlanın hava tahminini kullanmaya devam eder. Konumu olmayan kullanıcı için mobil uygulama teknik hata yerine yönlendirici boş durum gösterecek.

## Güvenlik ve Gizlilik

- Konum izni açıklaması yalnızca tarla konumunu kaydetmek ve yerel hava tahmini sağlamak amacıyla yazılacak.
- Arka plan konumu, konum geçmişi ve sürekli takip olmayacak.
- Google Maps API anahtarları paket/uygulama kimliği ile sınırlandırılacak; Android ve iOS için ayrı anahtar kullanılacak.
- Konum verisi sadece kullanıcının kendi tarla kaydına yazılacak; mevcut JWT tabanlı sahiplik kontrolü korunacak.

## Hata Davranışları

- Konum servisi kapalıysa kullanıcıya açma yönlendirmesi gösterilir.
- İzin geçici reddedildiyse tekrar isteme seçeneği verilir.
- İzin kalıcı reddedildiyse uygulama ayarlarına yönlendirme ve harita alternatifi sunulur.
- GPS zaman aşımında harita seçimi önerilir.
- Harita yüklenemezse tarla konumsuz kaydedilebilir; uygulama kullanıcıyı engellemez.
- Backend kaydı başarısız olursa form verisi korunur ve kullanıcı tekrar deneyebilir.

## Testler

- `LocationService`: izin verilmiş, reddedilmiş, kalıcı reddedilmiş, servis kapalı ve konum alınmış durumları.
- Tarla ekleme ekranı: GPS sonucu koordinata dönüşür; harita seçimi koordinatı değiştirir; konum olmadan kayıt davranışı korunur.
- Backend tarla adaptörü: koordinatlı ve konumsuz tarla oluşturma/listeleme, API hata iletimi.
- Hava durumu: koordinatlı tarla için endpoint çağrısı, tarlası/konumu olmayan kullanıcı için yönlendirici durum.
- Android debug APK ile fiziksel cihaz testi: izin, GPS, haritadan seçim, tarla kaydı ve hava kartı.

## Gerekli Kullanıcı İşlemleri

Kod uygulamasından önce veya sırasında Google Cloud Console'da Google Maps Platform etkinleştirilecek. Android için Maps SDK for Android, iOS için Maps SDK for iOS açılacak; faturalandırma hesabı bağlanacak ve iki ayrı, kısıtlanmış API anahtarı oluşturulacak.

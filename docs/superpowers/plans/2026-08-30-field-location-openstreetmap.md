# Tarla Konumu ve OpenStreetMap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kullanıcıların tarlayı GPS veya OpenStreetMap ile konumlandırmasını, tarlayı backend'e kaydetmesini ve o konumdan hava durumu almasını sağlamak.

**Architecture:** Mobil uygulama tek seferlik konum izni için `LocationService` kullanacak. Tarla formu GPS sonucunu veya OpenStreetMap seçim sonucunu `TarlaLocation` olarak tutacak. Mevcut REST istemcisi üstünde çalışan backend tarla adaptörü, tarla işlemlerini `/api/v1/farms` ile senkronize edecek; hava deposu aynı backend tarlasını kullanacak.

**Tech Stack:** Flutter, `geolocator`, `flutter_map`, Firebase Auth, REST API, .NET 8 backend, Open-Meteo.

**Spec:** `docs/superpowers/specs/2026-08-30-field-location-design.md`

## Global Constraints

- Konum yalnızca kullanıcı "Konumumu kullan" eylemiyle alınacak; arka plan konumu ve konum geçmişi olmayacak.
- Harita için API anahtarı veya faturalandırma hesabı kullanılmayacak.
- Harita üzerinde görünür OpenStreetMap atfı ve uygulamayı tanımlayan bir User-Agent kullanılacak.
- Kamuya açık OpenStreetMap tile sunucusu yalnızca düşük trafikli pilot kullanım için kullanılacak; tile URL yapılandırılabilir olacak.
- Konumsuz tarla kaydedilebilecek; hava kartı teknik hata yerine yönlendirme gösterecek.
- Yeni üretim davranışı test önce yazılarak uygulanacak.

---

### Task 1: OpenStreetMap ve konum altyapısı

**Files:**
- Modify: `mobile/pubspec.yaml`
- Modify: `mobile/android/app/src/main/AndroidManifest.xml`
- Modify: `mobile/ios/Runner/Info.plist`
- Test: `mobile/test/platform_configuration_test.dart`

**Interfaces:**
- Produces: Android ve iOS'ta kullanılabilir OpenStreetMap görünümü ile `geolocator` çalışma zamanı izinleri.
- Consumes: OpenStreetMap tile kullanım politikası ve uygulamanın destek URL'si.

- [ ] **Step 1: Paket ve platform davranışı için başarısız test yaz**

`mobile/test/platform_configuration_test.dart` dosyasında manifestte hassas/approximate konum izinlerini, `pubspec.yaml` içinde iki paketi ve iOS plistte kullanım açıklamasını kontrol eden metin tabanlı testleri yaz.

```dart
test('Android manifest declares foreground location permissions', () {
  final manifest = File('android/app/src/main/AndroidManifest.xml').readAsStringSync();
  expect(manifest, contains('android.permission.ACCESS_FINE_LOCATION'));
  expect(manifest, contains('android.permission.ACCESS_COARSE_LOCATION'));
});
```

- [ ] **Step 3: Testin başarısız olduğunu doğrula**

Run: `flutter test test/platform_configuration_test.dart`

Expected: Konum izinleri ve OpenStreetMap paketi bulunamadığı için FAIL.

- [ ] **Step 4: Minimal platform yapılandırmasını ekle**

`pubspec.yaml` içine aşağıdaki bağımlılıkları ekle:

```yaml
geolocator: ^14.0.2
flutter_map: ^8.2.2
```

Android manifestine `ACCESS_FINE_LOCATION` ve `ACCESS_COARSE_LOCATION` ekle. Harita anahtarı, manifest metadata'sı veya faturalandırma yapılandırması ekleme.

iOS `Info.plist` içine `NSLocationWhenInUseUsageDescription` olarak `Tarla konumunuzu kaydetmek ve yerel hava durumunu göstermek için konumunuza erişiyoruz.` ekle. Harita anahtarı, iOS SDK başlatma kodu veya faturalandırma yapılandırması ekleme.

- [ ] **Step 5: Yapılandırma testlerini ve bağımlılık çözümlemesini çalıştır**

Run: `flutter pub get && flutter test test/platform_configuration_test.dart`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add mobile/pubspec.yaml mobile/android/app/src/main/AndroidManifest.xml mobile/ios/Runner/Info.plist mobile/test/platform_configuration_test.dart mobile/pubspec.lock
git commit -m "feat: configure location and OpenStreetMap"
```

### Task 2: Tek seferlik GPS konum servisi

**Files:**
- Create: `mobile/lib/features/location/domain/tarla_location.dart`
- Create: `mobile/lib/features/location/data/location_service.dart`
- Create: `mobile/lib/features/location/data/geolocator_location_service.dart`
- Test: `mobile/test/features/location/data/geolocator_location_service_test.dart`

**Interfaces:**
- Produces: `TarlaLocation(double latitude, double longitude)` ve `LocationService.getCurrentLocation(): Future<TarlaLocation>`.
- Consumes: `geolocator` izin ve konum API'leri.

- [ ] **Step 1: Başarısız servis testlerini yaz**

`LocationService` için sahte platform adaptörüyle aşağıdaki senaryoları test et: servis kapalı, izin reddedilmiş, izin kalıcı reddedilmiş ve geçerli koordinat dönüşü.

```dart
await expectLater(
  service.getCurrentLocation(),
  throwsA(isA<LocationUnavailableException>()),
);
```

- [ ] **Step 2: Testlerin başarısız olduğunu doğrula**

Run: `flutter test test/features/location/data/geolocator_location_service_test.dart`

Expected: `LocationService` ve hata türleri tanımlı olmadığı için FAIL.

- [ ] **Step 3: Minimal servis arayüzünü ve uygulamasını yaz**

`TarlaLocation` immutable bir değer türü olsun. `GeolocatorLocationService.getCurrentLocation` sırasıyla `isLocationServiceEnabled`, `checkPermission`, gerekirse `requestPermission` ve `getCurrentPosition` çağrılarını kullanacak. Başarısızlıkları `LocationServiceDisabledException`, `LocationPermissionDeniedException` ve `LocationPermissionPermanentlyDeniedException` olarak dönüştürecek. `LocationAccuracy.high` ve 15 saniye `timeLimit` kullanacak.

- [ ] **Step 4: Servis testlerini çalıştır**

Run: `flutter test test/features/location/data/geolocator_location_service_test.dart`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/location mobile/test/features/location/data/geolocator_location_service_test.dart
git commit -m "feat: add single-use field location service"
```

### Task 3: OpenStreetMap nokta seçme ekranı

**Files:**
- Create: `mobile/lib/features/location/presentation/field_location_picker_screen.dart`
- Test: `mobile/test/features/location/presentation/field_location_picker_screen_test.dart`

**Interfaces:**
- Consumes: `TarlaLocation`, isteğe bağlı başlangıç koordinatı.
- Produces: `Navigator.pop<TarlaLocation>(context, selectedLocation)` veya iptal için `null`.

- [ ] **Step 1: Başarısız widget testini yaz**

Harita adaptörünü enjekte ederek, harita dokunuşunun seçili işaretçiyi değiştirdiğini ve `Bu konumu kullan` düğmesinin doğru `TarlaLocation` değerini döndürdüğünü test et.

```dart
await tester.tap(find.text('Bu konumu kullan'));
expect(result, const TarlaLocation(38.4237, 27.1428));
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `flutter test test/features/location/presentation/field_location_picker_screen_test.dart`

Expected: Seçici ekran tanımlı olmadığı için FAIL.

- [ ] **Step 3: Harita ekranını uygula**

Başlangıç merkezi olarak seçilmiş konumu, yoksa Türkiye merkezi `39.0, 35.0` kullan. `FlutterMap` dokunma olayı işaretçiyi günceller. İşaretçi olmadan onay düğmesini devre dışı bırak. `TileLayer` için `https://tile.openstreetmap.org/{z}/{x}/{y}.png` kullan, `userAgentPackageName: 'com.tarlaasistani.pilot'` ayarla ve görünür `© OpenStreetMap contributors` atfını göster. Tile URL'sini tek bir yapılandırma sabitinde tut. Harita yükleme hatasında açıklama ve geri dönme düğmesi göster.

- [ ] **Step 4: Widget testini çalıştır**

Run: `flutter test test/features/location/presentation/field_location_picker_screen_test.dart`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/location/presentation/field_location_picker_screen.dart mobile/test/features/location/presentation/field_location_picker_screen_test.dart
git commit -m "feat: add OpenStreetMap field location picker"
```

### Task 4: Backend uyumlu tarla deposu

**Files:**
- Create: `mobile/lib/features/fields/data/backend_tarla_repository.dart`
- Modify: `mobile/lib/features/fields/data/backend_farm_repository.dart`
- Modify: `mobile/lib/main.dart`
- Modify: `mobile/lib/screens/ana_ekran.dart`
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_listesi_ekrani.dart`
- Modify: `mobile/lib/screens/tarla_ekleme_ekrani.dart`
- Test: `mobile/test/features/fields/data/backend_tarla_repository_test.dart`

**Interfaces:**
- Consumes: `TarlaRepository.addTarla(Tarla)`, `TarlaRepository.getTarlalar()`, `BackendFarmRepository` ve `FarmMapper`.
- Produces: Backend'e kaydedilen `Tarla` kayıtları; UI içindeki tüm tarla ekranlarına aynı repository örneği.

- [ ] **Step 1: Başarısız repository testini yaz**

`BackendTarlaRepository.addTarla` çağrısının `FarmCreateRequestDto` içinde aynı latitude/longitude değerlerini, dönümden hektara dönüşmüş büyüklüğü ve ürün/tarih bilgisini gönderdiğini test et. `getTarlalar` için backend DTO listesinin `Tarla` listesine dönüştüğünü test et.

```dart
expect(captured.latitude, 38.4237);
expect(captured.longitude, 27.1428);
expect(captured.sizeInHectares, closeTo(1.0, 0.0001));
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `flutter test test/features/fields/data/backend_tarla_repository_test.dart`

Expected: `BackendTarlaRepository` tanımlı olmadığı için FAIL.

- [ ] **Step 3: Repository adaptörünü uygula ve ekrana aktar**

`BackendTarlaRepository`, `TarlaRepository` arayüzünü tamamen uygulasın. UI dönüm kullandığından `size / 10` ile hektara dönüştür; API'den dönen hektarı ekranda dönüm olarak göster. `main.dart` tek `BackendTarlaRepository` örneğini oluştursun ve `AnaEkran` bu örneği tüm alt tarla ekranlarına iletsin. `TarlaEklemeEkrani` navigator çağrılarında bu örneği koru; varsayılan `LocalTarlaRepository` sadece test/bağımsız ekran kullanımı için kalsın.

- [ ] **Step 4: Repository ve ekran testlerini çalıştır**

Run: `flutter test test/features/fields/data/backend_tarla_repository_test.dart test/screens/tarla_ekleme_ekrani_test.dart test/screens/ana_sayfa_ekrani_test.dart`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/fields/data/backend_tarla_repository.dart mobile/lib/features/fields/data/backend_farm_repository.dart mobile/lib/main.dart mobile/lib/screens/ana_ekran.dart mobile/lib/screens/ana_sayfa_ekrani.dart mobile/lib/screens/tarla_listesi_ekrani.dart mobile/lib/screens/tarla_ekleme_ekrani.dart mobile/test/features/fields/data/backend_tarla_repository_test.dart
git commit -m "feat: persist mobile fields through backend"
```

### Task 5: Tarla formunda konum akışı

**Files:**
- Modify: `mobile/lib/screens/tarla_ekleme_ekrani.dart`
- Test: `mobile/test/screens/tarla_ekleme_ekrani_test.dart`

**Interfaces:**
- Consumes: `LocationService.getCurrentLocation`, `FieldLocationPickerScreen`, `BackendTarlaRepository`.
- Produces: Konumlu veya konumsuz `Tarla` kaydı.

- [ ] **Step 1: Başarısız form testlerini yaz**

GPS başarısında formun koordinatı kaydettiğini, harita seçim sonucunun GPS sonucunu değiştirdiğini ve izin reddinde kayıt butonunun kullanılabilir kaldığını test et.

```dart
expect(savedTarla.latitude, 38.4237);
expect(find.text('Konum seçilmedi'), findsOneWidget);
```

- [ ] **Step 2: Testlerin başarısız olduğunu doğrula**

Run: `flutter test test/screens/tarla_ekleme_ekrani_test.dart`

Expected: Konum eylemleri olmadığı için FAIL.

- [ ] **Step 3: Form davranışını uygula**

`_selectedLocation` başlangıçta `null` olsun. GPS düğmesi işlem sırasında devre dışı kalsın ve servis istisnalarını Türkçe, eyleme dönük metinlere çevirsin. Harita sonucu `_selectedLocation`a yazılsın. Kaydetme `latitude: _selectedLocation?.latitude` ve `longitude: _selectedLocation?.longitude` kullansın; 0.0 varsayılanlarını kaldır. Konum kaldırma düğmesi tekrar `null` yapmalı.

- [ ] **Step 4: Form testlerini çalıştır**

Run: `flutter test test/screens/tarla_ekleme_ekrani_test.dart`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/screens/tarla_ekleme_ekrani.dart mobile/test/screens/tarla_ekleme_ekrani_test.dart
git commit -m "feat: capture field location during creation"
```

### Task 6: Konumsuz hava durumu boş durumu

**Files:**
- Modify: `mobile/lib/features/weather/data/backend_weather_repository.dart`
- Modify: `mobile/lib/screens/ana_sayfa_ekrani.dart`
- Test: `mobile/test/features/weather/data/backend_weather_repository_test.dart`
- Test: `mobile/test/screens/ana_sayfa_ekrani_test.dart`

**Interfaces:**
- Consumes: Backend farm listesi ve hava endpointi.
- Produces: `WeatherLocationRequiredException` ve kullanıcıya görünen "Hava durumu için tarla konumu ekleyin" durumu.

- [ ] **Step 1: Başarısız testleri yaz**

Tarlası olmayan veya latitude/longitude alanı boş olan kullanıcıda deponun `WeatherLocationRequiredException` ürettiğini ve ana sayfanın teknik hata yerine konum yönlendirmesi gösterdiğini test et.

```dart
expect(find.text('Hava durumu için tarla konumu ekleyin'), findsOneWidget);
```

- [ ] **Step 2: Testlerin başarısız olduğunu doğrula**

Run: `flutter test test/features/weather/data/backend_weather_repository_test.dart test/screens/ana_sayfa_ekrani_test.dart`

Expected: Yeni istisna ve yönlendirici metin olmadığı için FAIL.

- [ ] **Step 3: Yönlendirici hata davranışını uygula**

`BackendWeatherRepository` ilk aktif backend tarlayı bulamazsa veya koordinat alanları boşsa `WeatherLocationRequiredException` atsın. Ana sayfa bu istisnayı yakalayıp `Tarla Ekle` eylemine bağlanan boş durum kartı göstersin. Gerçek ağ/API hatalarını ayrı genel hata kartında bırak.

- [ ] **Step 4: Hava durumu testlerini çalıştır**

Run: `flutter test test/features/weather/data/backend_weather_repository_test.dart test/screens/ana_sayfa_ekrani_test.dart`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/weather/data/backend_weather_repository.dart mobile/lib/screens/ana_sayfa_ekrani.dart mobile/test/features/weather/data/backend_weather_repository_test.dart mobile/test/screens/ana_sayfa_ekrani_test.dart
git commit -m "feat: guide users when weather location is missing"
```

### Task 7: Uçtan uca doğrulama ve Android fiziksel cihaz testi

**Files:**
- Modify: `mobile/README.md` (yoksa Create: `mobile/README.md`)

**Interfaces:**
- Consumes: Task 1-6 tamamlanmış uygulama.
- Produces: Tekrar edilebilir fiziksel test yönergesi ve debug APK.

- [ ] **Step 1: Tam test paketini çalıştır**

Run: `flutter test`

Expected: Tüm testler PASS.

- [ ] **Step 2: Android debug APK oluştur**

Run: `flutter build apk --debug`

Expected: `build/app/outputs/flutter-apk/app-debug.apk` oluşturulur.

- [ ] **Step 3: Fiziksel cihazda doğrula**

1. Eski uygulamayı güncelle.
2. Yeni tarla formunda `Konumumu kullan` ile izin ver ve koordinatın göründüğünü doğrula.
3. Harita ile farklı bir noktayı seçip kaydet.
4. Tarlanın yeniden açıldığında backend'de korunduğunu doğrula.
5. Ana sayfada hava kartının sıcaklık ve özet gösterdiğini doğrula.
6. Konumu kaldırılmış bir tarla ile hava kartının yönlendirme metnini gösterdiğini doğrula.

- [ ] **Step 4: Kullanım notunu ekle**

`mobile/README.md` içine OpenStreetMap atıf ve tile kullanım kurallarını, yalnızca foreground konum izni kullanıldığını ve fiziksel test adımlarını ekle.

- [ ] **Step 5: Commit**

```bash
git add mobile/README.md
git commit -m "docs: add field location setup and verification"
```

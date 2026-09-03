# Mobile Case Creation Design (Saha Bildirimi / Vaka Oluşturma)

## 1. Goal & Context

Tarla Asistanı platformunda backend ve web uzman paneli üzerinde vaka yönetimi (`/api/v1/cases`) ve medya yükleme (`/api/v1/media`) altyapısı hazır olmasına rağmen, mobil uygulamada çiftçinin sahadan vaka açabileceği bir ekran bulunmamaktadır.

Bu çalışmanın amacı: Çiftçinin tarlasında karşılaştığı hastalık, zararlı, sulama, besleme vb. zirai problemleri fotoğraf ve sesli/yazılı açıklamalarla sisteme bildirmesini (`POST /api/v1/cases`) sağlayan **Sorun Bildir / Vaka Aç** özelliğini mobil uygulamaya kazandırmaktır.

---

## 2. Architecture & Components

Mevcut projenin Feature-First mimarisine uygun olarak `mobile/lib/features/cases/` dizini altında şu bileşenler oluşturulacaktır:

```
mobile/lib/features/cases/
├── domain/
│   └── models/
│       ├── case_category.dart      # Kategori enum'ı ve UI/Backend haritalaması
│       └── create_case_input.dart  # Formdan repository'ye aktarılan veri modeli
├── data/
│   ├── case_repository.dart        # Abstract arayüz
│   └── backend_case_repository.dart# ApiClient üzerinden /media ve /cases entegrasyonu
└── presentation/
    └── sorun_bildir_ekrani.dart    # Çiftçi sorun bildirme formu
```

### 2.1. Domain Modelleri
* **`CaseCategory`:**
  * Değerler: `disease`, `pest`, `irrigation`, `nutrition`, `weather`, `other`.
  * Türkçe Görünür İsimler: *Hastalık*, *Zararlı*, *Sulama*, *Besleme / Gübre*, *Hava Koşulları*, *Diğer*.
  * Backend Değerleri: `Disease`, `Pest`, `Irrigation`, `Nutrition`, `Weather`, `Other`.
* **`CreateCaseInput`:**
  * `farmId`: `String` (Zorunlu)
  * `category`: `CaseCategory` (Zorunlu)
  * `title`: `String` (Min 2, Max 160 karakter)
  * `description`: `String` (Zorunlu)
  * `imageBytes`: `List<int>?` (Opsiyonel fotoğraf byte verisi)
  * `imageFileName`: `String?` (Opsiyonel dosya adı, örn. `sorun.jpg`)

### 2.2. Data Katmanı (`BackendCaseRepository`)
* `CaseRepository` arayüzü:
  ```dart
  abstract interface class CaseRepository {
    Future<String> createCase(CreateCaseInput input);
  }
  ```
* `BackendCaseRepository` iş akışı:
  1. `input.imageBytes != null` ise:
     * `ApiClient.postMultipart('/media', files: [ApiMultipartFile(...)])` çağrılır.
     * Dönen yanıttan `id` (GUID) çözülür.
  2. `ApiClient.postJson('/cases', ...)` çağrılır:
     * `farm_id`: `input.farmId`
     * `category`: `input.category.backendValue`
     * `title`: `input.title`
     * `description`: `input.description`
     * `media_ids`: Fotoğraf varsa `[mediaId]`, yoksa `null`
     * `client_operation_id`: `Uuid().v4()`
  3. Dönen vaka kaydının `id`'si döndürülür.

### 2.3. Kullanıcı Arayüzü (`SorunBildirEkrani`)
* **Görsel Tasarım:**
  * Tarlada güneş altında rahat okunabilir geniş dokunma alanları (hitboxes).
  * En üstte kamera/galeri fotoğraf alanı (önizleme ve silme/değiştirme desteği).
  * Kategori çipleri (`FilterChip` / `ChoiceChip`) ile tek dokunuşla seçim.
  * Başlık ve çok satırlı açıklama alanı.
  * Açıklama alanında mikrofondan konuşarak metne dökme (Speech-to-Text) simgesi.
  * Büyük "Uzmana Gönder" birincil butonu (işlem sırasında çift tıklamayı önleyen spinner).
* **Giriş Noktaları (Entry Points):**
  1. `TarlaDetayEkrani`: Tarla bilgi kartında *"🚨 Sorun Bildir / Ziraat Mühendisine Danış"* butonu (tarlanın `id`'si ile kilitli/önseçili açılır).
  2. `AnaSayfaEkrani`: Hızlı işlem kartlarında *"Sorun Bildir"* butonu (tarla seçim açılır menüsü ile açılır).

---

## 3. Data Flow & Error Handling

```
[Kullanıcı Formu Doldurur]
         │
  (Validasyon Kontrolü) ───[Hata]──► [Eksik Alan Uyarısı Göster]
         │ [Başarılı]
  [Fotoğraf Var mı?]
    ├── Evet ──► POST /api/v1/media ──► MediaId Alındı
    └── Hayır ──► (Devam et)
         │
   POST /api/v1/cases (MediaIds, Category, Title, Description)
         │
    ├── [201 Created] ──► Yeşil SnackBar ("Uzmana iletildi") & Navigator.pop(context, true)
    └── [Hata / Network / 4xx / 5xx] ──► ApiException yakalanır, hata SnackBar'da gösterilir; form korunur.
```

---

## 4. Testing Strategy

1. **Unit Tests (`test/features/cases/data/backend_case_repository_test.dart`):**
   * Fotoğrafsız vaka gönderiminin doğrudan `POST /cases` tetiklemesi.
   * Fotoğraflı vaka gönderiminde önce `POST /media`, ardından `media_ids` ile `POST /cases` çağrılması.
   * Medya yüklemesi veya vaka gönderiminde ağ/sunucu hatalarının doğru şekilde `ApiException` olarak iletilmesi.
2. **Widget Tests (`test/features/cases/presentation/sorun_bildir_ekrani_test.dart`):**
   * Tüm bileşenlerin (fotoğraf alanı, kategori çipleri, başlık, açıklama, gönder butonu) ekranda render edilmesi.
   * Eksik zorunlu alanlarda form doğrulama uyarılarının çıkması.
   * Başarılı form gönderiminde mock repository'nin tetiklenmesi ve ekranın kapanması.
3. **Integration / Navigation Tests:**
   * `TarlaDetayEkrani` üzerindeki butona tıklandığında `SorunBildirEkrani`'nın doğru tarla ile açılması.

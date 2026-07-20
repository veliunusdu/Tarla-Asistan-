# Product Backlog & Detailed Sprint Roadmap 🌾

This document details the 5-week Sprint Roadmap for **Tarla Asistanı**, breaking down tasks between **Developer A (Backend & Web)** and **Developer B (Mobile)**.

---

## 🔹 Sprint 1: Mimari, Kimlik Doğrulama & Temeller (Hafta 1)
**Target:** Çalışan OTP altyapısı, veritabanı şemaları, temel mobil ve web proje iskeletleri.

### Developer A (Backend & Web)
- [ ] Dockerize edilmiş Backend ortamını kur (`FastAPI` + `PostgreSQL` + `Redis`)
- [ ] Alembic migration altyapısını ve core tabloları (`users`, `farms`) oluştur
- [ ] OTP Gönderme ve Doğrulama API'lerini yaz (`POST /auth/request-otp`, `POST /auth/verify-otp`)
- [ ] JWT token & Rol bazlı yetkilendirme (RBAC: `FARMER`, `AGRONOMIST`) katmanını ekle
- [ ] Ziraat Mühendisi Web Panel projesini (`Next.js`/`React`) ve Giriş ekranını oluştur

### Developer B (Mobile)
- [ ] Flutter mobil uygulamasını Clean Architecture / Feature-first yapısıyla başlat
- [ ] Tasarım sistemini kur (Güneş altında okunabilir renkler, büyük butonlar, fontlar)
- [ ] Telefon Numarası Girişi ve SMS OTP Doğrulama ekranlarını tasarla
- [ ] Auth State Management (`Riverpod`/`Bloc`) ve Secure Storage (Token saklama) kur
- [ ] Profil Oluşturma ekranını geliştir (Ad, Soyad, İl, İlçe seçimi)

---

## 🔹 Sprint 2: Tarla Yönetimi & Hava Durumu Entegrasyonu (Hafta 2)
**Target:** Çiftçinin haritadan konum seçerek tarla eklemesi, hava risklerinin işlenmesi.

### Developer A (Backend & Web)
- [ ] Tarla CRUD API uç noktalarını geliştir (`GET /farms`, `POST /farms`, `PUT /farms/{id}`)
- [ ] Ürün ve Ekim Dönemi API'lerini yaz (`POST /farms/{id}/crops`)
- [ ] Hava Durumu servisini bağla (OpenWeatherMap / Tomorrow.io API)
- [ ] Hava Riski Değerlendirme Motorunu yaz (Don, Kuvvetli Rüzgar, Aşırı Yağış tespiti)
- [ ] Web Panelde Mühendisler için "Çiftçiler ve Tarlalar" listesini yap

### Developer B (Mobile)
- [ ] Harita seçici bileşenini (Map Picker) entegre et (Konum seçimi için)
- [ ] "Tarla Ekle" formunu geliştir (Tarla Adı, Alan, Ürün Türü, Ekim Tarihi)
- [ ] Tarla Seçim ve Liste ekranını yap
- [ ] Mobil Ana Ekran üst bölümüne Güncel Hava Durumu & Risk Rozetlerini ekle
- [ ] Birden fazla tarla arasında geçiş yapabilme özelliğini ekle

---

## 🔹 Sprint 3: Günlük Görev Motoru & Faaliyet Kaydı (Hafta 3)
**Target:** Ana ekranda günlük 3 öncelikli görev gösterimi ve faaliyet kaydı (yazılı/sesli).

### Developer A (Backend & Web)
- [ ] Günlük Görev Oluşturucu servisini yaz (Ürün takvimi + Hava riski -> Maks 3 görev/gün)
- [ ] Görev API uç noktalarını geliştir (`GET /tasks`, `POST /tasks/{id}/complete`)
- [ ] Faaliyet Kaydı API'sini yaz (Sulama, Gübreleme, İlaçlama vb.)
- [ ] Tarla Günlüğü / Geçmişi API'sini oluştur
- [ ] Web Paneline Ziraat Mühendisinin Çiftçiye "Özel Görev Oluşturma" modülünü ekle

### Developer B (Mobile)
- [ ] Mobil Ana Ekran "Günün 3 Önemli Görevi" kart tasarımını yap
- [ ] Görev Detay ve Tamamlama akışını yap (Tamamla / Fotoğraf ekle / Uygulamama nedeni seç)
- [ ] Cihaz mikrofonu ve Speech-to-Text (Sesle Komut/Kayıt) eklentisini entegre et
- [ ] Faaliyet Kaydet formunu yap (Sesli anlatımı otomatik parse etme veya manuel giriş)
- [ ] Tarla Günlüğü / Geçmiş kronolojik akış ekranını geliştir

---

## 🔹 Sprint 4: Çevrimdışı Çalışma (Offline-First) & Sorun Bildirme (Hafta 4)
**Target:** İnternet yokken veri kaydetme, senkronizasyon ve fotoğraflı vaka bildirme.

### Developer A (Backend & Web)
- [ ] Medya yükleme servisini yaz (Fotoğraf ve ses kayıtları için S3/MinIO)
- [ ] Vaka (Sorun Bildirimi) API'sini yaz (`POST /cases`, `GET /cases`, `POST /cases/{id}/messages`)
- [ ] Çevrimdışı senkronizasyonda mükerrer kaydı önleyici (Idempotency) mantığı API'ye ekle
- [ ] Web Panelde Mühendis "Vaka Kutusu ve Detay Ekranı"nı geliştir (Fotoğraf, ses, hava tek ekranda)
- [ ] Mühendisin Vakaya Cevap Yazma / Sesli Cevap Yükleme modülünü yap

### Developer B (Mobile)
- [ ] Yerel Veritabanını kur (`SQLite`/`Hive` - Görevler ve Tarlaları cihazda saklamak için)
- [ ] Offline Sync Manager & Kuyruk (Queue) yapısını kur (`Idempotency-Key` kullanımıyla)
- [ ] "Sorun Bildir" kamera akışını yap (Fotoğraf çekme, ses kaydı ekleme, yayılım seçme)
- [ ] "Gönderdiğim Sorunlar / Vakalarım" durum takip ekranını geliştir
- [ ] İnternet bağlantısı koptuğunda / geldiğinde çalışan uyarı durumlarını ekle

---

## 🔹 Sprint 5: Bildirimler, Testler & Pilot Yayını (Hafta 5)
**Target:** Push bildirimler, uçtan uca testler, güvenlik taramaları ve canlıya çıkış.

### Developer A (Backend & Web)
- [ ] Firebase Cloud Messaging (FCM) backend entegrasyonunu yap (Kritik uyarı ve uzman cevapları için)
- [ ] Backend güvenlik taramaları (Rate Limiting, IDOR, Input Validation)
- [ ] Staging & Production sunucu kurulumunu tamamla (Docker & Cloud Deploy)
- [ ] Swagger/OpenAPI dokümantasyonunu son hale getir

### Developer B (Mobile)
- [ ] FCM mobil entegrasyonunu yap (Bildirime tıklayınca ilgili Vakaya/Göreve yönlendirme)
- [ ] Düşük bağlantı (2G/3G) ve çevrimdışı kullanım testlerini yap
- [ ] Dokunma alanlarını (Hitboxes) ve okunabilirliği tarladaki kullanım şartlarına göre optimize et
- [ ] Android APK / Bundle (AAB) derlemesini al ve yayına hazır hale getir

---

## 🔮 Future Plans (Post-MVP)
- WhatsApp integration.
- Integration of tractor and hardware sensor data.
- Satellite image analysis.

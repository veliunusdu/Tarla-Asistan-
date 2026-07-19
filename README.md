Tarla Asistanı 🌾
"Her sabah tarlan için en önemli üç işi gösterir."

Tarla Asistanı; çiftçilerin günlük tarımsal faaliyetlerini en az eforla planlamalarını sağlayan, sahadan kolayca veri toplamalarına imkan tanıyan ve uzman ziraat mühendisleriyle merkezi bir dijital panelde buluşmalarını köprüleyen yapay zekâ destekli tarla yönetim sistemidir.

🚀 Temel Özellikler (MVP)
📱 Çiftçi Mobil Uygulaması (Saha Odaklı)
Net Görev Listesi: Ana ekranda gereksiz veri yoğunluğundan arındırılmış, o gün yapılması gereken en kritik 3 eylem kartı.

Çevrimdışı (Offline) Destek: Kırsal alanda internet kesilse dahi veri girişi ve internet geldiğinde Idempotency-Key ile mükerrer kaydı önleyen arka plan senkronizasyonu.

Hızlı Sorun Bildirme: Sahadaki hastalık veya gelişim problemini sadece fotoğraf çekip sesli not bırakarak doğrudan uzmana iletme.

💻 Ziraat Mühendisi Web Paneli (Karar Odaklı)
Vaka Takip Sistemi: Çiftçilerden gelen fotoğraflı/sesli sorun bildirimlerini aciliyet seviyesine göre merkezi olarak listeleme.

Tarla Geçmiş Takvimi: Mühendisin reçete yazmadan veya tavsiye vermeden önce tarlanın o sezondaki tüm geçmiş faaliyetlerini (sulama, gübreleme, ilaçlama) tek bir takvimde görebilmesi.

Talimat Oluşturma: Çiftçiye doğrudan görev atayabilme ve push bildirim mekanizması.

🛠️ Teknoloji Yığını
Sistem, ölçeklenebilirliği korumak amacıyla Modüler Monolit mimariyle tasarlanmıştır:

Backend: Python, FastAPI, SQLAlchemy, Alembic

Veritabanı: PostgreSQL + PostGIS (Coğrafi konum verileri için) & Redis (OTP & Cache)

Mobil Uygulama: Flutter (veya React Native)

Web Panel: React / Next.js, TypeScript & Tailwind CSS

📁 Proje Klasör Yapısı
Plaintext
Tarla-Asistani/
├── backend/       # FastAPI Backend API ve Kural Motoru
├── mobile/        # Mobil Uygulama (Çevrimdışı Önbellek & Donanım Entegrasyonu)
├── web/           # Mühendis Karar Destek Paneli (Frontend)
└── docs/          # PROJECT_OVERVIEW, DATABASE ve API Dokümantasyonları
💻 İlk Kurulum (Geliştiriciler İçin)
Projeyi klonlayın:

Bash
git clone https://github.com/[kullanici-adi]/Tarla-Asistani.git
Gerekli ortam değişkenlerini tanımlamak için .env.example dosyalarını kopyalayıp .env adıyla yapılandırın.

Altyapıyı (PostgreSQL, PostGIS, Redis) ayağa kaldırmak için ana dizinde Docker'ı tetikleyin:

Bash
docker-compose up -d

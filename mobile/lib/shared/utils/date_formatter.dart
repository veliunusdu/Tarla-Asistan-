/// Türkçe ay kısaltmaları.
const List<String> trAylar = [
  'Oca',
  'Şub',
  'Mar',
  'Nis',
  'May',
  'Haz',
  'Tem',
  'Ağu',
  'Eyl',
  'Eki',
  'Kas',
  'Ara',
];

/// Tarihi "Gün Ay Yıl" formatında döndürür (Örn: "15 May 2026").
String formatTarih(DateTime dt) =>
    '${dt.day} ${trAylar[dt.month - 1]} ${dt.year}';

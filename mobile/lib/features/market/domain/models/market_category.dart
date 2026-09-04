/// Tarımsal piyasa verilerinin kategorilerini temsil eden enum.
enum MarketCategory {
  /// Akaryakıt ürünleri (Motorin, Benzin vb.)
  fuel,

  /// Gübre ürünleri (Üre, DAP vb.)
  fertilizer,

  /// Mahsul ve hububat ürünleri (Buğday, Mısır vb.)
  crop,

  /// Döviz kurları (USD, EUR vb.)
  fx,

  /// Tüm kategorileri kapsayan filtre seçeneği
  all;

  /// Kullanıcı arayüzünde gösterilecek Türkçe etiket.
  String get displayName => switch (this) {
        MarketCategory.fuel => 'Akaryakıt',
        MarketCategory.fertilizer => 'Gübre',
        MarketCategory.crop => 'Mahsul',
        MarketCategory.fx => 'Döviz',
        MarketCategory.all => 'Tümü',
      };

  /// REST API sorgularında kullanılan küçük harfli parametre değeri.
  String get apiValue => switch (this) {
        MarketCategory.fuel => 'fuel',
        MarketCategory.fertilizer => 'fertilizer',
        MarketCategory.crop => 'crop',
        MarketCategory.fx => 'fx',
        MarketCategory.all => 'all',
      };

  /// API'den gelen dize değerini büyük/küçük harf duyarsız eşleştirerek enum değerine dönüştürür.
  factory MarketCategory.fromApiValue(String? value) {
    if (value == null || value.trim().isEmpty) {
      return MarketCategory.all;
    }

    final normalized = value.trim().toLowerCase();
    return MarketCategory.values.firstWhere(
      (c) => c.apiValue == normalized || c.name.toLowerCase() == normalized,
      orElse: () => MarketCategory.all,
    );
  }
}

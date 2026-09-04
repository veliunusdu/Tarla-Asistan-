import '../domain/models/market_category.dart';
import '../domain/models/market_item.dart';

/// Piyasa verilerini yerel önbellekten ve uzak backend servisinden yöneten repository sözleşmesi.
abstract interface class MarketRepository {
  /// Piyasa verilerini getirir.
  /// Stale-while-revalidate deseni uyarınca önce önbellekteki veriyi anında döner,
  /// arka planda sunucudan taze veriyi çekip önbelleği yeniler.
  Future<List<MarketItem>> getMarketData({MarketCategory? category});

  /// Yalnızca yerel SQLite önbelleğindeki piyasa verilerini ağ isteği atmadan anında döndürür.
  Future<List<MarketItem>> getCachedMarketData({MarketCategory? category});

  /// Backend üzerinden piyasa verilerini zorla yeniler ve yerel önbelleğe yazar.
  Future<void> refreshMarketData({MarketCategory? category});
}

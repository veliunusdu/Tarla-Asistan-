import 'dart:async';
import 'package:flutter/foundation.dart';
import '../../../services/api_client.dart';
import '../domain/models/market_category.dart';
import '../domain/models/market_item.dart';
import '../domain/models/market_response.dart';
import 'local_market_repository.dart';
import 'market_repository.dart';

/// Piyasa verilerinin UI tarafındaki reaktif durumlarını temsil eden mühürlü (sealed) sınıf.
@immutable
sealed class MarketDataState {
  const MarketDataState();
}

/// Veriler yüklenirken (arka planda tazelenirken) aktif olan durum.
class MarketDataLoading extends MarketDataState {
  const MarketDataLoading({this.cachedItems = const []});

  /// Varsa anında gösterilen yerel önbellek kalemleri.
  final List<MarketItem> cachedItems;
}

/// Veriler başarıyla hazır olduğunda aktif olan durum.
class MarketDataLoaded extends MarketDataState {
  const MarketDataLoaded({
    required this.items,
    this.isStale = false,
    this.lastUpdated,
  });

  /// Güncel piyasa kalemleri.
  final List<MarketItem> items;

  /// Verinin 72 saatten eski olup olmadığını belirtir (çevrimdışı bayat veri uyarısı için).
  final bool isStale;

  /// Verinin sunucudaki son güncellenme zamanı.
  final DateTime? lastUpdated;
}

/// Ağ veya ayrıştırma hatası meydana geldiğinde aktif olan durum.
class MarketDataError extends MarketDataState {
  const MarketDataError({
    this.cachedItems = const [],
    required this.message,
    this.isStale = false,
  });

  /// Hata anında bile kullanıcıya gösterilmeye devam eden önbellek verileri.
  final List<MarketItem> cachedItems;

  /// Kullanıcı dostu Türkçe hata açıklaması.
  final String message;

  /// Önbellekteki verinin bayat olup olmadığı.
  final bool isStale;
}

/// API ve SQLite yerel önbelleğini stale-while-revalidate deseniyle orkestre eden backend repository sınıfı.
class BackendMarketRepository implements MarketRepository {
  BackendMarketRepository({
    required this.apiClient,
    required this.localRepo,
  });

  final ApiClient apiClient;
  final LocalMarketRepository localRepo;

  /// Kullanıcı arayüzünün piyasa verisi değişikliklerini dinleyebilmesi için durum bildiricisi.
  final ValueNotifier<MarketDataState> stateNotifier =
      ValueNotifier<MarketDataState>(const MarketDataLoading());

  /// Stale-while-revalidate akışı:
  /// 1. Varsa SQLite önbelleğindeki verileri anında okur ve UI'a iletir.
  /// 2. Arka planda REST API'den (/api/v1/market) taze veriyi sorgular.
  /// 3. Başarılı olursa yerel önbelleğe yazar ve UI'ı yeni verilerle günceller.
  /// 4. Başarısız olursa yerel veriyi korur ve bayatlık uyarısıyla hata durumunu bildirir.
  @override
  Future<List<MarketItem>> getMarketData({MarketCategory? category}) async {
    final cached = await localRepo.getCachedData(category: category);
    final isStale = await localRepo.isStale();

    if (cached.isNotEmpty) {
      stateNotifier.value = MarketDataLoaded(
        items: cached,
        isStale: isStale,
        lastUpdated: cached.map((e) => e.updatedAtUtc).reduce((a, b) => a.isAfter(b) ? a : b),
      );
    } else {
      stateNotifier.value = const MarketDataLoading();
    }

    // Arka planda API'den yenilemeyi tetikle (UI akışını bekletmez)
    unawaited(_fetchAndCache(category: category, fallbackCached: cached));

    return cached;
  }

  @override
  Future<List<MarketItem>> getCachedMarketData({MarketCategory? category}) {
    return localRepo.getCachedData(category: category);
  }

  @override
  Future<void> refreshMarketData({MarketCategory? category}) async {
    final cached = await localRepo.getCachedData(category: category);
    stateNotifier.value = MarketDataLoading(cachedItems: cached);
    await _fetchAndCache(category: category, fallbackCached: cached);
  }

  /// REST API sorgusu atar, geçici ağ hatalarında üssel geri çekilme (exponential backoff) ile 2 kez yineler.
  Future<void> _fetchAndCache({
    MarketCategory? category,
    required List<MarketItem> fallbackCached,
  }) async {
    const maxRetries = 2;
    var attempt = 0;
    var delayMs = 500;

    Map<String, dynamic>? queryParams;
    if (category != null && category != MarketCategory.all) {
      queryParams = {'category': category.apiValue};
    }

    while (attempt <= maxRetries) {
      try {
        final jsonResponse = await apiClient.getJson(
          '/market',
          queryParameters: queryParams,
          requiresAuth: false,
        );

        final marketResponse = MarketResponse.fromJson(jsonResponse);

        // SQLite önbelleğine kalıcı olarak yaz (hata alsa bile UI akışını engellemez)
        try {
          await localRepo.cacheData(marketResponse.items);
        } catch (cacheErr) {
          debugPrint('BackendMarketRepository: Önbelleğe yazma hatası: $cacheErr');
        }

        // Kategoriye göre filtrelenmiş güncel veriyi hazırla
        final displayItems = (category == null || category == MarketCategory.all)
            ? marketResponse.items
            : marketResponse.items.where((i) => i.category == category).toList();

        final itemsToShow = displayItems.isNotEmpty
            ? displayItems
            : await localRepo.getCachedData(category: category);

        stateNotifier.value = MarketDataLoaded(
          items: itemsToShow,
          isStale: false,
          lastUpdated: marketResponse.lastUpdatedUtc,
        );
        return;
      } catch (e) {
        attempt++;
        if (attempt > maxRetries) {
          final isStale = await localRepo.isStale();
          final userFriendlyMessage = _formatErrorMessage(e);

          stateNotifier.value = MarketDataError(
            cachedItems: fallbackCached,
            message: userFriendlyMessage,
            isStale: isStale,
          );
          return;
        }

        // Kısa bekleme ve üssel geri çekilme
        await Future.delayed(Duration(milliseconds: delayMs));
        delayMs *= 2;
      }
    }
  }

  static String _formatErrorMessage(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Piyasa verileri güncellenemedi. Lütfen internet bağlantınızı kontrol edin.';
  }
}

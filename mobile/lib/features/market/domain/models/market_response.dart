import 'package:flutter/foundation.dart';
import 'market_item.dart';

/// Backend'den dönen piyasa verileri yanıtını ve genel son güncelleme zamanını sarmalayan model.
@immutable
class MarketResponse {
  const MarketResponse({
    required this.lastUpdatedUtc,
    required this.items,
  });

  /// Sunucu tarafındaki en son veri güncelleme zamanı (UTC).
  final DateTime lastUpdatedUtc;

  /// Piyasa ürün ve parite kalemleri listesi.
  final List<MarketItem> items;

  /// API'den dönen JSON sözlüğünden nesne oluşturur.
  factory MarketResponse.fromJson(Map<String, dynamic> json) {
    final rawList = json['items'] as List<dynamic>? ?? [];
    final parsedItems = rawList
        .whereType<Map<String, dynamic>>()
        .map(MarketItem.fromJson)
        .toList();

    final lastUpdated = DateTime.tryParse(json['last_updated_utc']?.toString() ?? '')?.toUtc() ??
        (parsedItems.isNotEmpty
            ? parsedItems.map((e) => e.updatedAtUtc).reduce((a, b) => a.isAfter(b) ? a : b)
            : DateTime.now().toUtc());

    return MarketResponse(
      lastUpdatedUtc: lastUpdated,
      items: parsedItems,
    );
  }
}

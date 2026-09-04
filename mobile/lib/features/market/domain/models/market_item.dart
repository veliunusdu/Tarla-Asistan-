import 'package:flutter/foundation.dart';
import 'market_category.dart';

/// Çiftçiler için takip edilen tekil bir piyasa kalemini (akaryakıt, gübre, mahsul veya döviz) temsil eden değişmez model.
@immutable
class MarketItem {
  const MarketItem({
    required this.code,
    required this.name,
    required this.category,
    required this.price,
    required this.previousPrice,
    required this.changePercent,
    required this.changeDirection,
    required this.unit,
    required this.iconKey,
    required this.updatedAtUtc,
  });

  /// Kalemin tekil sistem kodu (örn: "DIESEL", "USD_TRY").
  final String code;

  /// Ürün veya parite adı (örn: "Motorin (Mazot)", "Dolar").
  final String name;

  /// Ürünün kategorisi.
  final MarketCategory category;

  /// Güncel birim fiyat (TL).
  final double price;

  /// Önceki günün referans/kapanış fiyatı (TL).
  final double previousPrice;

  /// Günlük değişim yüzdesi (örn: 1.47, -0.61).
  final double changePercent;

  /// Fiyat değişim yönü ("up", "down", "neutral").
  final String changeDirection;

  /// Fiyatlandırma birimi (örn: "TL/Lt", "TL/Ton", "TL").
  final String unit;

  /// Arayüz simgesi eşleme anahtarı (örn: "fuel_diesel", "fx_usd_try").
  final String iconKey;

  /// Verinin son güncellenme zamanı (UTC).
  final DateTime updatedAtUtc;

  /// Fiyatın yükselip yükselmediğini belirtir.
  bool get isUp => changeDirection == 'up';

  /// Fiyatın düşüp düşmediğini belirtir.
  bool get isDown => changeDirection == 'down';

  /// Fiyatın sabit kalıp kalmadığını belirtir.
  bool get isNeutral => !isUp && !isDown;

  /// Türkçe ondalık ayracı (virgül) ve birim içeren biçimlendirilmiş fiyat (örn: "44,85 TL/Lt").
  String get formattedPrice {
    final priceStr = _formatTurkishDecimal(price);
    return unit.isNotEmpty ? '$priceStr $unit' : priceStr;
  }

  /// Günlük değişim yüzdesinin işaretli ve Türkçe biçimli hali (örn: "+%1,47", "-%0,61", "%0,00").
  String get formattedChange {
    final absVal = changePercent.abs().toStringAsFixed(2).replaceAll('.', ',');
    if (isUp) return '+%$absVal';
    if (isDown) return '-%$absVal';
    return '%$absVal';
  }

  /// JSON sözlüğünden güvenli şekilde nesne üretir.
  factory MarketItem.fromJson(Map<String, dynamic> json) {
    return MarketItem(
      code: json['code']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      category: MarketCategory.fromApiValue(json['category']?.toString()),
      price: (json['price'] as num?)?.toDouble() ?? 0.0,
      previousPrice: (json['previous_price'] as num?)?.toDouble() ?? 0.0,
      changePercent: (json['change_percent'] as num?)?.toDouble() ?? 0.0,
      changeDirection: json['change_direction']?.toString().toLowerCase() ?? 'neutral',
      unit: json['unit']?.toString() ?? '',
      iconKey: json['icon_key']?.toString() ?? '',
      updatedAtUtc: DateTime.tryParse(json['updated_at_utc']?.toString() ?? '')?.toUtc() ??
          DateTime.now().toUtc(),
    );
  }

  /// SQLite satırından nesne üretir.
  factory MarketItem.fromSqlite(Map<String, dynamic> map) {
    return MarketItem(
      code: map['code']?.toString() ?? '',
      name: map['name']?.toString() ?? '',
      category: MarketCategory.fromApiValue(map['category']?.toString()),
      price: (map['price'] as num?)?.toDouble() ?? 0.0,
      previousPrice: (map['previous_price'] as num?)?.toDouble() ?? 0.0,
      changePercent: (map['change_percent'] as num?)?.toDouble() ?? 0.0,
      changeDirection: map['change_direction']?.toString().toLowerCase() ?? 'neutral',
      unit: map['unit']?.toString() ?? '',
      iconKey: map['icon_key']?.toString() ?? '',
      updatedAtUtc: DateTime.tryParse(map['updated_at_utc']?.toString() ?? '')?.toUtc() ??
          DateTime.now().toUtc(),
    );
  }

  /// SQLite tablosuna yazılmak üzere Map formatına dönüştürür.
  Map<String, dynamic> toSqliteMap({String? cachedAtUtc}) {
    return {
      'code': code,
      'name': name,
      'category': category.apiValue,
      'price': price,
      'previous_price': previousPrice,
      'change_percent': changePercent,
      'change_direction': changeDirection,
      'unit': unit,
      'icon_key': iconKey,
      'updated_at_utc': updatedAtUtc.toIso8601String(),
      'cached_at_utc': cachedAtUtc ?? DateTime.now().toUtc().toIso8601String(),
    };
  }

  static String _formatTurkishDecimal(double value) {
    final fixed = value.toStringAsFixed(2);
    final parts = fixed.split('.');
    final wholePart = parts[0];
    final decimalPart = parts[1];

    // Binlik basamakları nokta ile ayır
    final regex = RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))');
    final formattedWhole = wholePart.replaceAllMapped(regex, (m) => '${m[1]}.');

    return '$formattedWhole,$decimalPart';
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is MarketItem &&
          runtimeType == other.runtimeType &&
          code == other.code &&
          name == other.name &&
          category == other.category &&
          price == other.price &&
          previousPrice == other.previousPrice &&
          changePercent == other.changePercent &&
          changeDirection == other.changeDirection &&
          unit == other.unit &&
          iconKey == other.iconKey &&
          updatedAtUtc == other.updatedAtUtc;

  @override
  int get hashCode => Object.hash(
        code,
        name,
        category,
        price,
        previousPrice,
        changePercent,
        changeDirection,
        unit,
        iconKey,
        updatedAtUtc,
      );
}

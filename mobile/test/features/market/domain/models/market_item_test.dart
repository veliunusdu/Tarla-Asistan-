import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_item.dart';

void main() {
  group('MarketItem', () {
    test('fromJson parses full payload correctly', () {
      final json = {
        'code': 'DIESEL',
        'name': 'Motorin (Mazot)',
        'category': 'fuel',
        'price': 44.85,
        'previous_price': 44.20,
        'change_percent': 1.47,
        'change_direction': 'up',
        'unit': 'TL/Lt',
        'icon_key': 'fuel_diesel',
        'updated_at_utc': '2026-09-04T07:00:00Z',
      };

      final item = MarketItem.fromJson(json);

      expect(item.code, equals('DIESEL'));
      expect(item.name, equals('Motorin (Mazot)'));
      expect(item.category, equals(MarketCategory.fuel));
      expect(item.price, equals(44.85));
      expect(item.previousPrice, equals(44.20));
      expect(item.changePercent, equals(1.47));
      expect(item.changeDirection, equals('up'));
      expect(item.unit, equals('TL/Lt'));
      expect(item.iconKey, equals('fuel_diesel'));
      expect(item.isUp, isTrue);
      expect(item.isDown, isFalse);
      expect(item.isNeutral, isFalse);
      expect(item.formattedPrice, equals('44,85 TL/Lt'));
      expect(item.formattedChange, equals('+%1,47'));
    });

    test('direction indicators work correctly for down and neutral', () {
      final downItem = MarketItem.fromJson({
        'code': 'FERT_UREA',
        'name': 'Üre Gübresi',
        'category': 'fertilizer',
        'price': 14200.0,
        'previous_price': 14350.0,
        'change_percent': -1.05,
        'change_direction': 'down',
        'unit': 'TL/Ton',
        'icon_key': 'fertilizer_urea',
        'updated_at_utc': '2026-09-04T07:00:00Z',
      });

      expect(downItem.isUp, isFalse);
      expect(downItem.isDown, isTrue);
      expect(downItem.isNeutral, isFalse);
      expect(downItem.formattedPrice, equals('14.200,00 TL/Ton'));
      expect(downItem.formattedChange, equals('-%1,05'));

      final neutralItem = MarketItem.fromJson({
        'code': 'FX_EUR',
        'name': 'Euro',
        'category': 'fx',
        'price': 38.00,
        'previous_price': 38.00,
        'change_percent': 0.0,
        'change_direction': 'neutral',
        'unit': 'TL',
        'icon_key': 'fx_eur',
        'updated_at_utc': '2026-09-04T07:00:00Z',
      });

      expect(neutralItem.isUp, isFalse);
      expect(neutralItem.isDown, isFalse);
      expect(neutralItem.isNeutral, isTrue);
      expect(neutralItem.formattedChange, equals('%0,00'));
    });

    test('SQLite serialization and deserialization roundtrip works', () {
      final original = MarketItem(
        code: 'CROP_WHEAT',
        name: 'Ekmeklik Buğday',
        category: MarketCategory.crop,
        price: 9850.50,
        previousPrice: 9700.00,
        changePercent: 1.55,
        changeDirection: 'up',
        unit: 'TL/Ton',
        iconKey: 'crop_wheat',
        updatedAtUtc: DateTime.utc(2026, 9, 4, 8, 30),
      );

      final sqliteMap = original.toSqliteMap(cachedAtUtc: '2026-09-04T08:35:00Z');
      expect(sqliteMap['code'], equals('CROP_WHEAT'));
      expect(sqliteMap['category'], equals('crop'));
      expect(sqliteMap['cached_at_utc'], equals('2026-09-04T08:35:00Z'));

      final restored = MarketItem.fromSqlite(sqliteMap);
      expect(restored, equals(original));
      expect(restored.hashCode, equals(original.hashCode));
    });
  });
}

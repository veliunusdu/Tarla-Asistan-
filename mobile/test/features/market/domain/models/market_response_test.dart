import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_response.dart';

void main() {
  group('MarketResponse', () {
    test('fromJson parses items and last_updated_utc', () {
      final json = {
        'last_updated_utc': '2026-09-04T07:15:00Z',
        'items': [
          {
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
          },
          {
            'code': 'FX_USD',
            'name': 'Dolar',
            'category': 'fx',
            'price': 34.22,
            'previous_price': 34.15,
            'change_percent': 0.20,
            'change_direction': 'up',
            'unit': 'TL',
            'icon_key': 'fx_usd',
            'updated_at_utc': '2026-09-04T07:15:00Z',
          }
        ],
      };

      final response = MarketResponse.fromJson(json);

      expect(response.lastUpdatedUtc, equals(DateTime.utc(2026, 9, 4, 7, 15)));
      expect(response.items.length, equals(2));
      expect(response.items[0].code, equals('DIESEL'));
      expect(response.items[0].category, equals(MarketCategory.fuel));
      expect(response.items[1].code, equals('FX_USD'));
      expect(response.items[1].category, equals(MarketCategory.fx));
    });

    test('fromJson handles empty items gracefully', () {
      final json = {
        'last_updated_utc': '2026-09-04T07:15:00Z',
        'items': <dynamic>[],
      };

      final response = MarketResponse.fromJson(json);

      expect(response.items, isEmpty);
      expect(response.lastUpdatedUtc, equals(DateTime.utc(2026, 9, 4, 7, 15)));
    });

    test('fromJson handles dynamic map types safely', () {
      final dynamicMap = <dynamic, dynamic>{
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

      final json = {
        'last_updated_utc': '2026-09-04T07:15:00Z',
        'items': <dynamic>[dynamicMap, 'invalid_item'],
      };

      final response = MarketResponse.fromJson(json);

      expect(response.items.length, equals(1));
      expect(response.items.first.code, equals('DIESEL'));
      expect(response.items.first.category, equals(MarketCategory.fuel));
    });
  });
}

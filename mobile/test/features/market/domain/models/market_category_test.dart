import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';

void main() {
  group('MarketCategory', () {
    test('displayName returns expected Turkish strings', () {
      expect(MarketCategory.fuel.displayName, equals('Akaryakıt'));
      expect(MarketCategory.fertilizer.displayName, equals('Gübre'));
      expect(MarketCategory.crop.displayName, equals('Mahsul'));
      expect(MarketCategory.fx.displayName, equals('Döviz'));
      expect(MarketCategory.all.displayName, equals('Tümü'));
    });

    test('apiValue returns expected query string values', () {
      expect(MarketCategory.fuel.apiValue, equals('fuel'));
      expect(MarketCategory.fertilizer.apiValue, equals('fertilizer'));
      expect(MarketCategory.crop.apiValue, equals('crop'));
      expect(MarketCategory.fx.apiValue, equals('fx'));
      expect(MarketCategory.all.apiValue, equals('all'));
    });

    test('fromApiValue parses valid and case-insensitive strings', () {
      expect(MarketCategory.fromApiValue('fuel'), equals(MarketCategory.fuel));
      expect(MarketCategory.fromApiValue('FUEL'), equals(MarketCategory.fuel));
      expect(MarketCategory.fromApiValue('fertilizer'), equals(MarketCategory.fertilizer));
      expect(MarketCategory.fromApiValue('crop'), equals(MarketCategory.crop));
      expect(MarketCategory.fromApiValue('fx'), equals(MarketCategory.fx));
      expect(MarketCategory.fromApiValue('all'), equals(MarketCategory.all));
    });

    test('fromApiValue returns all for null, empty or invalid strings', () {
      expect(MarketCategory.fromApiValue(null), equals(MarketCategory.all));
      expect(MarketCategory.fromApiValue(''), equals(MarketCategory.all));
      expect(MarketCategory.fromApiValue('   '), equals(MarketCategory.all));
      expect(MarketCategory.fromApiValue('unknown'), equals(MarketCategory.all));
    });
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_item.dart';
import 'package:mobile/features/market/presentation/widgets/market_item_card.dart';

void main() {
  group('MarketItemCard', () {
    testWidgets('renders item details correctly for price increase (up)', (tester) async {
      var tapped = false;
      final item = MarketItem(
        code: 'DIESEL',
        name: 'Motorin (Mazot)',
        category: MarketCategory.fuel,
        price: 44.85,
        previousPrice: 44.20,
        changePercent: 1.47,
        changeDirection: 'up',
        unit: 'TL/Lt',
        iconKey: 'fuel_diesel',
        updatedAtUtc: DateTime.utc(2026, 9, 4, 8, 0),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: MarketItemCard(
              item: item,
              onTap: () => tapped = true,
            ),
          ),
        ),
      );

      expect(find.text('Motorin (Mazot)'), findsOneWidget);
      expect(find.text('44,85 TL/Lt'), findsOneWidget);
      expect(find.text('⛽'), findsOneWidget);
      expect(find.text('+%1,47'), findsOneWidget);
      expect(find.text('▲'), findsOneWidget);

      await tester.tap(find.byType(MarketItemCard));
      expect(tapped, isTrue);
    });

    testWidgets('renders down direction correctly with green color and ▼', (tester) async {
      final item = MarketItem(
        code: 'FERT_UREA',
        name: 'Üre Gübresi',
        category: MarketCategory.fertilizer,
        price: 14200.0,
        previousPrice: 14350.0,
        changePercent: -1.05,
        changeDirection: 'down',
        unit: 'TL/Ton',
        iconKey: 'fertilizer_urea',
        updatedAtUtc: DateTime.utc(2026, 9, 4, 8, 0),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: MarketItemCard(item: item),
          ),
        ),
      );

      expect(find.text('Üre Gübresi'), findsOneWidget);
      expect(find.text('14.200,00 TL/Ton'), findsOneWidget);
      expect(find.text('🧪'), findsOneWidget);
      expect(find.text('-%1,05'), findsOneWidget);
      expect(find.text('▼'), findsOneWidget);
    });

    testWidgets('renders neutral direction correctly with gray ●', (tester) async {
      final item = MarketItem(
        code: 'FX_EUR',
        name: 'Euro',
        category: MarketCategory.fx,
        price: 38.00,
        previousPrice: 38.00,
        changePercent: 0.0,
        changeDirection: 'neutral',
        unit: 'TL',
        iconKey: 'fx_eur_try',
        updatedAtUtc: DateTime.utc(2026, 9, 4, 8, 0),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: MarketItemCard(item: item),
          ),
        ),
      );

      expect(find.text('Euro'), findsOneWidget);
      expect(find.text('💶'), findsOneWidget);
      expect(find.text('%0,00'), findsOneWidget);
      expect(find.text('●'), findsOneWidget);
    });

    test('iconForItem returns expected emojis for known keys', () {
      expect(MarketItemCard.iconForItem('fuel_diesel'), equals('⛽'));
      expect(MarketItemCard.iconForItem('fuel_gasoline'), equals('🛢️'));
      expect(MarketItemCard.iconForItem('fertilizer_urea'), equals('🧪'));
      expect(MarketItemCard.iconForItem('fertilizer_dap'), equals('🌿'));
      expect(MarketItemCard.iconForItem('crop_wheat'), equals('🌾'));
      expect(MarketItemCard.iconForItem('crop_corn'), equals('🌽'));
      expect(MarketItemCard.iconForItem('fx_usd_try'), equals('💵'));
      expect(MarketItemCard.iconForItem('fx_eur_try'), equals('💶'));
      expect(MarketItemCard.iconForItem('unknown'), equals('📊'));
    });
  });
}

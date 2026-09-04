import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/market/data/backend_market_repository.dart';
import 'package:mobile/features/market/data/local_market_repository.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_item.dart';
import 'package:mobile/features/market/presentation/widgets/market_item_card.dart';
import 'package:mobile/features/market/presentation/widgets/piyasa_bilgileri_widget.dart';
import 'package:mobile/services/api_client.dart';
import 'package:mobile/shared/widgets/app_empty_view.dart';
import 'package:mobile/shared/widgets/app_error_view.dart';
import 'package:mobile/shared/widgets/app_loading_view.dart';

class FakeBackendMarketRepository extends BackendMarketRepository {
  FakeBackendMarketRepository()
      : super(
          apiClient: ApiClient(idTokenProvider: () async => 'test-token'),
          localRepo: const LocalMarketRepository(),
        );

  int refreshCallCount = 0;

  @override
  Future<List<MarketItem>> getMarketData({MarketCategory? category}) async {
    return [];
  }

  @override
  Future<void> refreshMarketData({MarketCategory? category}) async {
    refreshCallCount++;
  }
}

void main() {
  group('PiyasaBilgileriWidget', () {
    late FakeBackendMarketRepository fakeRepo;

    final dieselItem = MarketItem(
      code: 'DIESEL',
      name: 'Motorin (Mazot)',
      category: MarketCategory.fuel,
      price: 44.85,
      previousPrice: 44.20,
      changePercent: 1.47,
      changeDirection: 'up',
      unit: 'TL/Lt',
      iconKey: 'fuel_diesel',
      updatedAtUtc: DateTime.now().subtract(const Duration(minutes: 10)),
    );

    final wheatItem = MarketItem(
      code: 'CROP_WHEAT',
      name: 'Ekmeklik Buğday',
      category: MarketCategory.crop,
      price: 9850.0,
      previousPrice: 9700.0,
      changePercent: 1.55,
      changeDirection: 'up',
      unit: 'TL/Ton',
      iconKey: 'crop_wheat',
      updatedAtUtc: DateTime.now().subtract(const Duration(minutes: 10)),
    );

    setUp(() {
      fakeRepo = FakeBackendMarketRepository();
    });

    testWidgets('shows loading view when state is MarketDataLoading with empty cache', (tester) async {
      fakeRepo.stateNotifier.value = const MarketDataLoading(cachedItems: []);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.text('Piyasa Bilgileri'), findsOneWidget);
      expect(find.byType(AppLoadingView), findsOneWidget);
      expect(find.text('Piyasa verileri yükleniyor…'), findsOneWidget);
    });

    testWidgets('shows cached cards immediately without spinner when loading with cached items', (tester) async {
      fakeRepo.stateNotifier.value = MarketDataLoading(cachedItems: [dieselItem]);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.byType(AppLoadingView), findsNothing);
      expect(find.byType(MarketItemCard), findsOneWidget);
      expect(find.text('Motorin (Mazot)'), findsOneWidget);
    });

    testWidgets('shows loaded cards, chips, and filters by category', (tester) async {
      fakeRepo.stateNotifier.value = MarketDataLoaded(
        items: [dieselItem, wheatItem],
        lastUpdated: DateTime.now().subtract(const Duration(minutes: 5)),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.text('Piyasa Bilgileri'), findsOneWidget);
      expect(find.byType(ChoiceChip), findsNWidgets(MarketCategory.values.length));
      expect(find.byType(MarketItemCard), findsNWidgets(2));

      // Filter by "Akaryakıt"
      await tester.tap(find.widgetWithText(ChoiceChip, 'Akaryakıt'));
      await tester.pumpAndSettle();

      expect(find.byType(MarketItemCard), findsOneWidget);
      expect(find.text('Motorin (Mazot)'), findsOneWidget);
      expect(find.text('Ekmeklik Buğday'), findsNothing);

      // Filter by "Gübre" (none exists)
      await tester.tap(find.widgetWithText(ChoiceChip, 'Gübre'));
      await tester.pumpAndSettle();

      expect(find.byType(MarketItemCard), findsNothing);
      expect(find.text('Bu kategoride veri bulunamadı.'), findsOneWidget);
    });

    testWidgets('shows stale warning banner when isStale is true', (tester) async {
      fakeRepo.stateNotifier.value = MarketDataLoaded(
        items: [dieselItem],
        isStale: true,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.text('Veriler güncel olmayabilir (çevrimdışı önbellek)'), findsOneWidget);
      expect(find.byType(MarketItemCard), findsOneWidget);
    });

    testWidgets('shows AppEmptyView when loaded with empty items', (tester) async {
      fakeRepo.stateNotifier.value = const MarketDataLoaded(items: []);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.byType(AppEmptyView), findsOneWidget);
      expect(find.text('Piyasa Verisi Bulunamadı'), findsOneWidget);
    });

    testWidgets('shows subtle warning banner and cached cards on error with cache', (tester) async {
      fakeRepo.stateNotifier.value = MarketDataError(
        cachedItems: [dieselItem],
        message: 'Sunucu hatası',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.text('Veri güncellenemedi, önbellek gösteriliyor'), findsOneWidget);
      expect(find.byType(MarketItemCard), findsOneWidget);
      expect(find.byType(AppErrorView), findsNothing);
    });

    testWidgets('shows AppErrorView with retry button on error with empty cache', (tester) async {
      fakeRepo.stateNotifier.value = const MarketDataError(
        cachedItems: [],
        message: 'Ağ bağlantısı yok',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(marketRepository: fakeRepo),
          ),
        ),
      );

      expect(find.byType(AppErrorView), findsOneWidget);
      expect(find.text('Ağ bağlantısı yok'), findsOneWidget);

      await tester.tap(find.text('Tekrar Dene'));
      expect(fakeRepo.refreshCallCount, equals(1));
    });

    testWidgets('calls onSeeAll callback when Tümü button is pressed', (tester) async {
      var seeAllCalled = false;
      fakeRepo.stateNotifier.value = MarketDataLoaded(items: [dieselItem]);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: PiyasaBilgileriWidget(
              marketRepository: fakeRepo,
              onSeeAll: () => seeAllCalled = true,
            ),
          ),
        ),
      );

      await tester.tap(find.widgetWithText(TextButton, 'Tümü'));
      expect(seeAllCalled, isTrue);
    });
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/features/market/data/backend_market_repository.dart';
import 'package:mobile/features/market/data/local_market_repository.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_item.dart';
import 'package:mobile/features/market/presentation/widgets/market_item_card.dart';
import 'package:mobile/features/market/presentation/widgets/piyasa_bilgileri_widget.dart';
import 'package:mobile/features/weather/data/weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/ana_sayfa_ekrani.dart';
import 'package:mobile/services/api_client.dart';

class FakeTarlaRepo implements TarlaRepository {
  @override
  Future<List<Tarla>> getTarlalar() async => [];
  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class FakeFaaliyetRepo implements FaaliyetRepository {
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];
  @override
  Future<void> deleteFaaliyet(String id) async {}
  @override
  Future<void> markAsCompleted(String id) async {}
}

class FakeWeatherRepo implements WeatherRepository {
  @override
  Future<WeatherSummary> getWeather({String? farmId}) async => const WeatherSummary(
        temperature: 24,
        description: 'Güneşli',
      );
}

class FakeMarketRepo extends BackendMarketRepository {
  FakeMarketRepo()
      : super(
          apiClient: ApiClient(idTokenProvider: () async => 'test-token'),
          localRepo: const LocalMarketRepository(),
        );

  @override
  Future<List<MarketItem>> getMarketData({MarketCategory? category}) async {
    return [];
  }
}

void main() {
  group('AnaSayfaEkrani Market Integration', () {
    testWidgets('renders PiyasaBilgileriWidget when marketRepository is provided', (tester) async {
      tester.view.physicalSize = const Size(800, 2000);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final fakeMarket = FakeMarketRepo();
      fakeMarket.stateNotifier.value = MarketDataLoaded(
        items: [
          MarketItem(
            code: 'DIESEL',
            name: 'Motorin (Mazot)',
            category: MarketCategory.fuel,
            price: 44.85,
            previousPrice: 44.20,
            changePercent: 1.47,
            changeDirection: 'up',
            unit: 'TL/Lt',
            iconKey: 'fuel_diesel',
            updatedAtUtc: DateTime.now(),
          ),
        ],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: AnaSayfaEkrani(
            tarlaRepository: FakeTarlaRepo(),
            faaliyetRepository: FakeFaaliyetRepo(),
            weatherRepository: FakeWeatherRepo(),
            marketRepository: fakeMarket,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byType(PiyasaBilgileriWidget), findsOneWidget);
      expect(find.text('Piyasa Bilgileri'), findsOneWidget);
      expect(find.byType(MarketItemCard), findsOneWidget);
      expect(find.text('Motorin (Mazot)'), findsOneWidget);
      expect(find.text('44,85 TL/Lt'), findsOneWidget);
    });
  });
}

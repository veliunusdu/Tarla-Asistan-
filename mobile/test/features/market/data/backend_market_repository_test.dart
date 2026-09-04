import 'dart:async';
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/features/market/data/backend_market_repository.dart';
import 'package:mobile/features/market/data/local_market_repository.dart';
import 'package:mobile/features/market/domain/models/market_category.dart';
import 'package:mobile/features/market/domain/models/market_item.dart';
import 'package:mobile/services/api_client.dart';

class MockHttpClient extends http.BaseClient {
  MockHttpClient(this._handler);
  final Future<http.Response> Function(http.BaseRequest request) _handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final response = await _handler(request);
    return http.StreamedResponse(
      Stream.value(response.bodyBytes),
      response.statusCode,
      headers: response.headers,
    );
  }
}

class FakeLocalMarketRepository extends LocalMarketRepository {
  List<MarketItem> memoryItems = [];
  bool staleFlag = false;

  @override
  Future<List<MarketItem>> getCachedData({MarketCategory? category}) async {
    if (category == null || category == MarketCategory.all) {
      return List.unmodifiable(memoryItems);
    }
    return List.unmodifiable(memoryItems.where((i) => i.category == category));
  }

  @override
  Future<void> cacheData(List<MarketItem> items) async {
    final map = {for (final item in memoryItems) item.code: item};
    for (final item in items) {
      map[item.code] = item;
    }
    memoryItems = map.values.toList();
  }

  @override
  Future<bool> isStale({Duration maxAge = const Duration(hours: 72)}) async {
    return staleFlag;
  }
}

void main() {
  group('BackendMarketRepository', () {
    late FakeLocalMarketRepository fakeLocal;

    final cachedItem = MarketItem(
      code: 'DIESEL',
      name: 'Motorin',
      category: MarketCategory.fuel,
      price: 43.0,
      previousPrice: 42.5,
      changePercent: 1.18,
      changeDirection: 'up',
      unit: 'TL/Lt',
      iconKey: 'fuel_diesel',
      updatedAtUtc: DateTime.utc(2026, 9, 3, 12, 0),
    );

    setUp(() {
      fakeLocal = FakeLocalMarketRepository();
    });

    test('getMarketData immediately returns cached data and then updates from API', () async {
      fakeLocal.memoryItems = [cachedItem];

      final apiClient = ApiClient(
        httpClient: MockHttpClient((req) async {
          expect(req.url.path, endsWith('/market'));
          final responseBody = jsonEncode({
            'last_updated_utc': '2026-09-04T08:00:00Z',
            'items': [
              {
                'code': 'DIESEL',
                'name': 'Motorin',
                'category': 'fuel',
                'price': 44.85,
                'previous_price': 43.0,
                'change_percent': 4.30,
                'change_direction': 'up',
                'unit': 'TL/Lt',
                'icon_key': 'fuel_diesel',
                'updated_at_utc': '2026-09-04T08:00:00Z',
              }
            ],
          });
          return http.Response(responseBody, 200, headers: {
            'content-type': 'application/json; charset=utf-8',
          });
        }),
        idTokenProvider: () async => 'test-token',
      );

      final repo = BackendMarketRepository(
        apiClient: apiClient,
        localRepo: fakeLocal,
      );

      final loadedCompleter = Completer<void>();
      repo.stateNotifier.addListener(() {
        if (repo.stateNotifier.value is MarketDataLoaded && !loadedCompleter.isCompleted) {
          final loaded = repo.stateNotifier.value as MarketDataLoaded;
          if (loaded.items.isNotEmpty && loaded.items.first.price == 44.85) {
            loadedCompleter.complete();
          }
        }
      });

      // Immediate return
      final initialData = await repo.getMarketData();
      expect(initialData.length, equals(1));
      expect(initialData.first.price, equals(43.0));

      // Wait for background async update to notify
      await loadedCompleter.future.timeout(const Duration(seconds: 3));

      // StateNotifier should now hold updated item
      final currentState = repo.stateNotifier.value;
      expect(currentState, isA<MarketDataLoaded>());
      final loadedState = currentState as MarketDataLoaded;
      expect(loadedState.items.first.price, equals(44.85));
      expect(loadedState.isStale, isFalse);
    });

    test('getMarketData sets MarketDataError on network failure but keeps cached fallback', () async {
      fakeLocal.memoryItems = [cachedItem];

      final apiClient = ApiClient(
        httpClient: MockHttpClient((req) async {
          return http.Response('{"error":"Internal Server Error"}', 500, headers: {
            'content-type': 'application/json; charset=utf-8',
          });
        }),
        idTokenProvider: () async => 'test-token',
      );

      final repo = BackendMarketRepository(
        apiClient: apiClient,
        localRepo: fakeLocal,
      );

      final errorCompleter = Completer<void>();
      repo.stateNotifier.addListener(() {
        if (repo.stateNotifier.value is MarketDataError && !errorCompleter.isCompleted) {
          errorCompleter.complete();
        }
      });

      final initial = await repo.getMarketData();
      expect(initial.length, equals(1));

      // Wait for retries to exhaust
      await errorCompleter.future.timeout(const Duration(seconds: 5));

      final state = repo.stateNotifier.value;
      expect(state, isA<MarketDataError>());
      final errorState = state as MarketDataError;
      expect(errorState.cachedItems.length, equals(1));
      expect(errorState.cachedItems.first.code, equals('DIESEL'));
    });

    test('getMarketData updates stateNotifier with API items even if local cache was empty', () async {
      fakeLocal.memoryItems = [];

      final apiClient = ApiClient(
        httpClient: MockHttpClient((req) async {
          final responseBody = jsonEncode({
            'last_updated_utc': '2026-09-04T08:00:00Z',
            'items': [
              {
                'code': 'UREA',
                'name': 'Üre Gübresi',
                'category': 'fertilizer',
                'price': 14350.0,
                'previous_price': 14250.0,
                'change_percent': 0.70,
                'change_direction': 'up',
                'unit': 'TL/Ton',
                'icon_key': 'fertilizer_urea',
                'updated_at_utc': '2026-09-04T08:00:00Z',
              }
            ],
          });
          return http.Response(responseBody, 200, headers: {
            'content-type': 'application/json; charset=utf-8',
          });
        }),
        idTokenProvider: () async => 'test-token',
      );

      final repo = BackendMarketRepository(
        apiClient: apiClient,
        localRepo: fakeLocal,
      );

      final loadedCompleter = Completer<void>();
      repo.stateNotifier.addListener(() {
        if (repo.stateNotifier.value is MarketDataLoaded && !loadedCompleter.isCompleted) {
          final loaded = repo.stateNotifier.value as MarketDataLoaded;
          if (loaded.items.isNotEmpty && loaded.items.first.code == 'UREA') {
            loadedCompleter.complete();
          }
        }
      });

      await repo.getMarketData();
      await loadedCompleter.future.timeout(const Duration(seconds: 3));

      final state = repo.stateNotifier.value as MarketDataLoaded;
      expect(state.items.length, equals(1));
      expect(state.items.first.code, equals('UREA'));
    });
  });
}

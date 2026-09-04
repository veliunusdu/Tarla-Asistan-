import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/weather/data/backend_weather_repository.dart';
import 'package:mobile/features/weather/data/local_weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/services/api_client.dart';

class FakeLocalWeatherRepository extends LocalWeatherRepository {
  FakeLocalWeatherRepository({this.cachedWeather});

  WeatherSummary? cachedWeather;
  WeatherSummary? savedWeather;
  String? savedFarmId;

  @override
  Future<WeatherSummary?> getCachedWeather({String? farmId}) async {
    return cachedWeather;
  }

  @override
  Future<void> cacheWeather({
    String? farmId,
    required WeatherSummary weather,
  }) async {
    savedFarmId = farmId;
    savedWeather = weather;
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  ApiClient makeClient({
    required Future<http.Response> Function(http.Request) handler,
  }) => ApiClient(
    httpClient: MockClient(handler),
    idTokenProvider: () async => 'test-id-token',
    forceRefreshTokenProvider: () async => 'refreshed-token',
  );

  // ---------------------------------------------------------------------------
  // Canonical ApiClient usage
  // ---------------------------------------------------------------------------

  group('BackendWeatherRepository – canonical ApiClient', () {
    test('calls correct endpoint for the given farmId', () async {
      late Uri capturedUri;
      final client = makeClient(
        handler: (request) async {
          capturedUri = request.url;
          return http.Response(
            jsonEncode({
              'farm_id': 'farm-abc',
              'provider': 'open_meteo',
              'is_stale': false,
              'stale_reason': null,
              'points': [
                {
                  'observed_at': '2026-08-26T00:00:00Z',
                  'temperature_c': 22.5,
                  'precipitation_probability': 10.0,
                  'precipitation_mm': 0.0,
                  'wind_speed_kmh': 15.0,
                },
              ],
              'risks': [],
            }),
            200,
          );
        },
      );
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-abc',
      );

      await repo.getWeather();

      expect(capturedUri.path, contains('farms/farm-abc/weather'));
      client.close();
    });

    test('sends Firebase ID token in Authorization header', () async {
      String? capturedAuth;
      final client = makeClient(
        handler: (request) async {
          capturedAuth = request.headers['Authorization'];
          return http.Response(
            jsonEncode({
              'is_stale': false,
              'points': [
                {'temperature_c': 18.0, 'precipitation_probability': 5.0},
              ],
              'risks': [],
            }),
            200,
          );
        },
      );
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-1',
      );

      await repo.getWeather();

      expect(capturedAuth, 'Bearer test-id-token');
      client.close();
    });

    test('parses temperature and description correctly', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'is_stale': false,
            'points': [
              {'temperature_c': 36, 'precipitation_probability': 20.0},
            ],
            'risks': [],
          }),
          200,
        ),
      );
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-2',
      );

      final summary = await repo.getWeather();

      expect(summary.temperature, 36);
      expect(summary.description, 'Çok sıcak hava');
      client.close();
    });

    test(
      'returns stale reason when is_stale=true and stale_reason set',
      () async {
        final client = makeClient(
          handler: (_) async => http.Response(
            jsonEncode({
              'is_stale': true,
              'stale_reason': 'Hava durumu güncellenemedi.',
              'points': [
                {'temperature_c': 20, 'precipitation_probability': 0.0},
              ],
              'risks': [],
            }),
            200,
          ),
        );
        final repo = BackendWeatherRepository(
          apiClient: client,
          farmId: 'farm-3',
        );

        final summary = await repo.getWeather();

        expect(summary.description, 'Hava durumu güncellenemedi.');
        client.close();
      },
    );

    test('throws ApiException when points list is empty', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({'is_stale': false, 'points': [], 'risks': []}),
          200,
        ),
      );
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-4',
      );

      await expectLater(repo.getWeather(), throwsA(isA<ApiException>()));
      client.close();
    });

    test('propagates ApiException from ApiClient (e.g. 401)', () async {
      // Both initial and force-refresh responses return 401 → retry exhausted.
      final failClient = ApiClient(
        httpClient: MockClient(
          (_) async => http.Response('{"detail":"expired"}', 401),
        ),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );
      final repo = BackendWeatherRepository(
        apiClient: failClient,
        farmId: 'farm-5',
      );

      await expectLater(repo.getWeather(), throwsA(isA<ApiException>()));
      failClient.close();
    });

    test('throws a typed outcome when no farm has coordinates', () async {
      final client = makeClient(
        handler: (_) async => http.Response(jsonEncode(<dynamic>[]), 200),
      );
      final repo = BackendWeatherRepository(apiClient: client);

      await expectLater(
        repo.getWeather(),
        throwsA(isA<WeatherLocationRequiredException>()),
      );
      client.close();
    });

    test('maps a selected farm without coordinates to the location flow', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          '{"detail":"Hava durumu için tarlanın konumu tanımlanmalıdır."}',
          422,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-no-location');

      await expectLater(
        repo.getWeather(),
        throwsA(isA<WeatherLocationRequiredException>()),
      );
      client.close();
    });

    test('maps a weather provider outage to a typed retryable outcome', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          '{"detail":"Hava durumu şu anda alınamıyor."}',
          503,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-provider-down');

      await expectLater(
        repo.getWeather(),
        throwsA(isA<WeatherUnavailableException>()),
      );
      client.close();
    });

    test('returns cached weather with (önbellek) in description when API returns 503 and cache exists', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          '{"detail":"Hava durumu şu anda alınamıyor."}',
          503,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final fakeLocal = FakeLocalWeatherRepository(
        cachedWeather: const WeatherSummary(
          temperature: 19,
          description: 'Güneşli ve Açık',
        ),
      );
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-cached-503',
        localRepo: fakeLocal,
      );

      final summary = await repo.getWeather();

      expect(summary.temperature, 19);
      expect(summary.description, 'Güneşli ve Açık (önbellek)');
      client.close();
    });

    test('returns cached weather with (önbellek) in description when API throws network/timeout error and cache exists', () async {
      final failClient = ApiClient(
        httpClient: MockClient(
          (_) async => throw http.ClientException('Sunucuya ulaşılamadı'),
        ),
        idTokenProvider: () async => 'tok',
      );
      final fakeLocal = FakeLocalWeatherRepository(
        cachedWeather: const WeatherSummary(
          temperature: 24,
          description: 'Parçalı Bulutlu',
        ),
      );
      final repo = BackendWeatherRepository(
        apiClient: failClient,
        farmId: 'farm-cached-network',
        localRepo: fakeLocal,
      );

      final summary = await repo.getWeather();

      expect(summary.temperature, 24);
      expect(summary.description, 'Parçalı Bulutlu (önbellek)');
      failClient.close();
    });

    test('rethrows WeatherUnavailableException when API returns 503 and cache is empty', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          '{"detail":"Hava durumu şu anda alınamıyor."}',
          503,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final fakeLocal = FakeLocalWeatherRepository(cachedWeather: null);
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-no-cache',
        localRepo: fakeLocal,
      );

      await expectLater(
        repo.getWeather(),
        throwsA(isA<WeatherUnavailableException>()),
      );
      client.close();
    });

    test('caches weather summary to localRepo on successful fetch', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'farm_id': 'farm-cache-success',
            'current': {
              'temperature_c': 28.0,
              'condition': 'Açık',
            },
            'points': [],
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final fakeLocal = FakeLocalWeatherRepository();
      final repo = BackendWeatherRepository(
        apiClient: client,
        farmId: 'farm-cache-success',
        localRepo: fakeLocal,
      );

      final summary = await repo.getWeather();

      expect(summary.temperature, 28);
      expect(summary.description, 'Açık');
      expect(fakeLocal.savedFarmId, 'farm-cache-success');
      expect(fakeLocal.savedWeather?.temperature, 28);
      expect(fakeLocal.savedWeather?.description, 'Açık');
      client.close();
    });

    test(
      'uses farmId parameter directly without requesting farms list',
      () async {
        int farmListCalls = 0;
        late Uri capturedWeatherUri;
        final client = makeClient(
          handler: (request) async {
            if (request.url.path.contains('farms?') ||
                request.url.query.contains('limit=')) {
              farmListCalls++;
              return http.Response('[]', 200);
            }
            capturedWeatherUri = request.url;
            return http.Response(
              jsonEncode({
                'farm_id': 'farm-dynamic-123',
                'provider': 'open_meteo',
                'current': {
                  'temperature_c': 25.4,
                  'condition': 'Parçalı Bulutlu',
                },
                'points': [],
                'risks': [],
              }),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          },
        );
        final repo = BackendWeatherRepository(apiClient: client);

        final summary = await repo.getWeather(farmId: 'farm-dynamic-123');

        expect(farmListCalls, 0);
        expect(
          capturedWeatherUri.path,
          contains('farms/farm-dynamic-123/weather'),
        );
        expect(summary.temperature, 25);
        expect(summary.description, 'Parçalı Bulutlu');
        client.close();
      },
    );

    test(
      'prefers normalized current condition and temperature when present',
      () async {
        final client = makeClient(
          handler: (_) async => http.Response(
            jsonEncode({
              'current': {
                'temperature_c': 21.6,
                'condition': 'Güneşli ve Açık',
              },
              'points': [
                {'temperature_c': 15.0, 'precipitation_probability': 90.0},
              ],
              'risks': [],
            }),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          ),
        );
        final repo = BackendWeatherRepository(
          apiClient: client,
          farmId: 'farm-normalized',
        );

        final summary = await repo.getWeather();

        expect(summary.temperature, 22);
        expect(summary.description, 'Güneşli ve Açık');
        client.close();
      },
    );
  });
}

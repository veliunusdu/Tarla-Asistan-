import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/weather/data/backend_weather_repository.dart';
import 'package:mobile/features/weather/data/local_weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/services/api_client.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

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
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

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
      'maps is_stale and stale_reason directly without overwriting description',
      () async {
        final client = makeClient(
          handler: (_) async => http.Response(
            jsonEncode({
              'is_stale': true,
              'stale_reason': 'Hava durumu güncellenemedi.',
              'current': {
                'temperature_c': 20,
                'condition': 'Parçalı Bulutlu',
              },
              'risks': [],
            }),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          ),
        );
        final repo = BackendWeatherRepository(
          apiClient: client,
          farmId: 'farm-3',
        );

        final summary = await repo.getWeather();

        expect(summary.isStale, isTrue);
        expect(summary.staleReason, 'Hava durumu güncellenemedi.');
        expect(summary.description, 'Parçalı Bulutlu');
        client.close();
      },
    );

    test('returns WeatherSummary with null temperature when points list and current are empty', () async {
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

      final summary = await repo.getWeather();
      expect(summary.temperature, isNull);
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

    test('returns cached weather with clean description, isStale=true, and staleReason when API returns 503 and cache exists', () async {
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
      expect(summary.description, 'Güneşli ve Açık');
      expect(summary.isStale, isTrue);
      expect(summary.staleReason, isNotNull);
      client.close();
    });

    test('returns cached weather with clean description, isStale=true, and staleReason when API throws network/timeout error and cache exists', () async {
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
      expect(summary.description, 'Parçalı Bulutlu');
      expect(summary.isStale, isTrue);
      expect(summary.staleReason, isNotNull);
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
        expect(summary.temperature, 25.4);
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

        expect(summary.temperature, 21.6);
        expect(summary.description, 'Güneşli ve Açık');
        client.close();
      },
    );
  });

  // ---------------------------------------------------------------------------
  // 11 Specific Mapping Scenarios
  // ---------------------------------------------------------------------------
  group('BackendWeatherRepository – 11 domain mapping scenarios', () {
    test('1. Full response preserves decimals and all fields without loss', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'farm_id': 'farm-full',
            'provider': 'open_meteo',
            'fetched_at': '2026-09-04T12:00:00Z',
            'is_stale': false,
            'stale_reason': null,
            'current': {
              'observed_at': '2026-09-04T12:00:00Z',
              'temperature_c': 23.6,
              'feels_like_c': 22.8,
              'humidity_percent': 45.0,
              'wind_speed_kmh': 12.4,
              'wind_gusts_kmh': 18.2,
              'condition': 'Açık',
              'weather_code': 0,
            },
            'daily': [
              {
                'date': '2026-09-04',
                'min_temperature_c': 14.2,
                'max_temperature_c': 26.1,
                'precipitation_probability': 10.0,
                'precipitation_mm': 0.0,
                'condition': 'Açık',
                'weather_code': 0,
              }
            ],
            'points': [
              {
                'observed_at': '2026-09-04T12:00:00Z',
                'temperature_c': 23.6,
                'precipitation_probability': 10.0,
                'precipitation_mm': 0.0,
                'wind_speed_kmh': 12.4,
                'humidity_percent': 45.0,
                'weather_code': 0,
              }
            ],
            'risks': [
              {
                'risk_type': 'HEAT_STRESS',
                'severity': 'LOW',
                'starts_at': '2026-09-04T13:00:00Z',
                'ends_at': '2026-09-04T16:00:00Z',
                'description': 'Düşük sıcaklık stresi',
                'suggested_action': 'Sulama planını kontrol edin',
              }
            ],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-full');

      final summary = await repo.getWeather();

      expect(summary.temperature, 23.6);
      expect(summary.feelsLike, 22.8);
      expect(summary.humidity, 45.0);
      expect(summary.windSpeed, 12.4);
      expect(summary.windGust, 18.2);
      expect(summary.description, 'Açık');
      expect(summary.condition, 'Açık');
      expect(summary.weatherCode, 0);
      expect(summary.minTemperature, 14.2);
      expect(summary.maxTemperature, 26.1);
      expect(summary.precipitationProbability, 10.0);
      expect(summary.precipitationAmount, 0.0);
      expect(summary.isStale, isFalse);
      expect(summary.staleReason, isNull);
      expect(summary.fetchedAt, DateTime.parse('2026-09-04T12:00:00Z'));
      expect(summary.observedAt, DateTime.parse('2026-09-04T12:00:00Z'));
      expect(summary.risks.length, 1);
      expect(summary.risks.first.riskType, 'HEAT_STRESS');
      expect(summary.risks.first.severity, 'LOW');
      expect(summary.risks.first.message, 'Düşük sıcaklık stresi');
      expect(summary.risks.first.suggestedAction, 'Sulama planını kontrol edin');
      client.close();
    });

    test('2. Real 0°C is preserved and not confused with null', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {
              'temperature_c': 0.0,
              'condition': 'Açık',
            },
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-zero');

      final summary = await repo.getWeather();

      expect(summary.temperature, isNotNull);
      expect(summary.temperature, 0.0);
      client.close();
    });

    test('3. Current is null: falls back safely to points.first', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': null,
            'points': [
              {
                'temperature_c': 18.5,
                'humidity_percent': 60.0,
                'wind_speed_kmh': 8.0,
                'observed_at': '2026-09-04T10:00:00Z',
                'weather_code': 2,
              }
            ],
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-fallback');

      final summary = await repo.getWeather();

      expect(summary.temperature, 18.5);
      expect(summary.humidity, 60.0);
      expect(summary.windSpeed, 8.0);
      expect(summary.weatherCode, 2);
      expect(summary.description, 'Parçalı Bulutlu');
      expect(summary.observedAt, DateTime.parse('2026-09-04T10:00:00Z'));
      client.close();
    });

    test('4. Both current and points absent: no crash, returns WeatherSummary with null temperature', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': null,
            'points': [],
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-empty');

      final summary = await repo.getWeather();

      expect(summary.temperature, isNull);
      expect(summary.humidity, isNull);
      expect(summary.windSpeed, isNull);
      expect(summary.description, '');
      client.close();
    });

    test('5. Daily is null: no crash, min/max and rain probability are null', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {
              'temperature_c': 20.0,
              'condition': 'Açık',
            },
            'daily': null,
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-daily-null');

      final summary = await repo.getWeather();

      expect(summary.temperature, 20.0);
      expect(summary.minTemperature, isNull);
      expect(summary.maxTemperature, isNull);
      expect(summary.precipitationProbability, isNull);
      expect(summary.precipitationAmount, isNull);
      client.close();
    });

    test('6. Daily is empty list: no crash, min/max are null', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {
              'temperature_c': 22.0,
              'condition': 'Açık',
            },
            'daily': [],
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-daily-empty');

      final summary = await repo.getWeather();

      expect(summary.temperature, 22.0);
      expect(summary.minTemperature, isNull);
      expect(summary.maxTemperature, isNull);
      client.close();
    });

    test('7. Risks is empty list: summary.risks is empty', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {'temperature_c': 21.0},
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-risks-empty');

      final summary = await repo.getWeather();

      expect(summary.risks, isEmpty);
      client.close();
    });

    test('8. Risks is populated: List<WeatherRisk> parsed correctly', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {'temperature_c': 5.0},
            'risks': [
              {
                'risk_type': 'FROST',
                'severity': 'CRITICAL',
                'starts_at': '2026-09-05T03:00:00Z',
                'ends_at': '2026-09-05T07:00:00Z',
                'description': 'Zirai don riski',
                'suggested_action': 'Örtü altı sistemleri devreye alın',
              }
            ],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-risks-full');

      final summary = await repo.getWeather();

      expect(summary.risks.length, 1);
      final risk = summary.risks.first;
      expect(risk.riskType, 'FROST');
      expect(risk.severity, 'CRITICAL');
      expect(risk.message, 'Zirai don riski');
      expect(risk.suggestedAction, 'Örtü altı sistemleri devreye alın');
      expect(risk.startsAt, DateTime.parse('2026-09-05T03:00:00Z'));
      expect(risk.endsAt, DateTime.parse('2026-09-05T07:00:00Z'));
      client.close();
    });

    test('9. Stale data preserves weather condition and sets isStale and staleReason', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'is_stale': true,
            'stale_reason': 'Cache used',
            'current': {
              'temperature_c': 20.0,
              'condition': 'Parçalı Bulutlu',
            },
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-stale');

      final summary = await repo.getWeather();

      expect(summary.description, 'Parçalı Bulutlu');
      expect(summary.condition, 'Parçalı Bulutlu');
      expect(summary.isStale, isTrue);
      expect(summary.staleReason, 'Cache used');
      client.close();
    });

    test('10. Numeric JSON variations: int, float, decimal safely parsed without precision loss', () async {
      final clientInt = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {'temperature_c': 23},
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repoInt = BackendWeatherRepository(apiClient: clientInt, farmId: 'farm-num-int');
      final summaryInt = await repoInt.getWeather();
      expect(summaryInt.temperature, 23);
      clientInt.close();

      final clientFloat = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {'temperature_c': 23.0},
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repoFloat = BackendWeatherRepository(apiClient: clientFloat, farmId: 'farm-num-float');
      final summaryFloat = await repoFloat.getWeather();
      expect(summaryFloat.temperature, 23.0);
      clientFloat.close();

      final clientDecimal = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'current': {'temperature_c': 23.6},
            'risks': [],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repoDecimal = BackendWeatherRepository(apiClient: clientDecimal, farmId: 'farm-num-dec');
      final summaryDecimal = await repoDecimal.getWeather();
      expect(summaryDecimal.temperature, 23.6);
      clientDecimal.close();
    });

    test('11. Malformed optional DateTime does not crash and leaves fields null', () async {
      final client = makeClient(
        handler: (_) async => http.Response(
          jsonEncode({
            'fetched_at': 'corrupt-time',
            'current': {
              'temperature_c': 19.0,
              'observed_at': 'not-a-date',
            },
            'risks': [
              {
                'risk_type': 'WIND',
                'severity': 'MEDIUM',
                'starts_at': 'invalid-date',
                'ends_at': null,
              }
            ],
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ),
      );
      final repo = BackendWeatherRepository(apiClient: client, farmId: 'farm-malformed-dates');

      final summary = await repo.getWeather();

      expect(summary.temperature, 19.0);
      expect(summary.fetchedAt, isNull);
      expect(summary.observedAt, isNull);
      expect(summary.risks.first.startsAt, isNull);
      expect(summary.risks.first.endsAt, isNull);
      client.close();
    });
  });
}

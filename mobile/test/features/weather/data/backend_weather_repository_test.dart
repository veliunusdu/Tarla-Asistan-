import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/weather/data/backend_weather_repository.dart';
import 'package:mobile/services/api_client.dart';

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
  });
}

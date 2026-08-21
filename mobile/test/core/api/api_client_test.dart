import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/api/api_config.dart';
import 'package:mobile/core/api/api_exception.dart';

void main() {
  group('ApiClient', () {
    test('2xx JSON yanıtını çözümler ve ortak headerları gönderir', () async {
      final mockClient = MockClient((request) async {
        expect(request.method, 'POST');
        expect(request.url.toString(), 'https://example.com/api/v1/items');
        expect(request.headers['Accept'], 'application/json');
        expect(request.headers['Content-Type'], 'application/json');
        expect(jsonDecode(request.body), {'name': 'Tarla'});

        return http.Response('{"id":"123"}', 201);
      });
      final apiClient = ApiClient(
        config: ApiConfig(baseUrl: 'https://example.com/api/v1'),
        httpClient: mockClient,
      );
      addTearDown(apiClient.close);

      final result = await apiClient.post('/items', body: {'name': 'Tarla'});

      expect(result, {'id': '123'});
    });

    test('204 No Content yanıtında null döndürür', () async {
      final mockClient = MockClient((_) async => http.Response('', 204));
      final apiClient = ApiClient(
        config: ApiConfig(baseUrl: 'https://example.com'),
        httpClient: mockClient,
      );
      addTearDown(apiClient.close);

      expect(await apiClient.delete('/items/123'), isNull);
    });

    test('401 yanıtını ApiException olarak dönüştürür', () async {
      final mockClient = MockClient(
        (_) async => http.Response('{"detail":"Oturum geçersiz."}', 401),
      );
      final apiClient = ApiClient(
        config: ApiConfig(baseUrl: 'https://example.com'),
        httpClient: mockClient,
      );
      addTearDown(apiClient.close);

      await expectLater(
        apiClient.get('/profile'),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', 401)
              .having((error) => error.message, 'message', 'Oturum geçersiz.'),
        ),
      );
    });

    test('timeout hatasını kullanıcıya uygun ApiException yapar', () async {
      final mockClient = MockClient((_) async {
        await Future<void>.delayed(const Duration(milliseconds: 50));
        return http.Response('{}', 200);
      });
      final apiClient = ApiClient(
        config: ApiConfig(baseUrl: 'https://example.com'),
        httpClient: mockClient,
        timeout: const Duration(milliseconds: 5),
      );
      addTearDown(apiClient.close);

      await expectLater(
        apiClient.get('/slow'),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', isNull)
              .having(
                (error) => error.message,
                'message',
                contains('zaman aşımına'),
              ),
        ),
      );
    });
  });
}

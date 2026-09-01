import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/api_client.dart';

// Encodes a JSON body as UTF-8 bytes so MockClient doesn't reject non-ASCII
// characters (its default Response() constructor uses Latin-1).
http.Response _utf8JsonResponse(Object body, int statusCode) =>
    http.Response.bytes(
      utf8.encode(jsonEncode(body)),
      statusCode,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  // ---------------------------------------------------------------------------
  // Token behaviour
  // ---------------------------------------------------------------------------

  group('token behaviour', () {
    test('normalizes the API base URL and endpoint with one slash', () async {
      Uri? capturedUri;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          capturedUri = request.url;
          return http.Response(jsonEncode({'ok': true}), 200);
        }),
        idTokenProvider: () async => 'test-token',
        forceRefreshTokenProvider: () async => 'test-token',
      );

      await api.getJson('/farms');

      expect(capturedUri?.path, '/api/v1/farms');
      api.close();
    });

    test('Firebase ID token is added to Authorization header', () async {
      String? capturedHeader;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          capturedHeader = request.headers['Authorization'];
          return http.Response(jsonEncode({'ok': true}), 200);
        }),
        idTokenProvider: () async => 'test-firebase-id-token',
        forceRefreshTokenProvider: () async => 'refreshed-token',
      );

      await api.getJson('/farms');

      expect(capturedHeader, 'Bearer test-firebase-id-token');
      api.close();
    });

    test('gets a fresh Firebase ID token for every request', () async {
      var tokenCalls = 0;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          expect(
            request.headers['Authorization'],
            'Bearer fresh-token-$tokenCalls',
          );
          return http.Response(jsonEncode({'id': 'task-1'}), 200);
        }),
        idTokenProvider: () async => 'fresh-token-${++tokenCalls}',
        forceRefreshTokenProvider: () async => 'refreshed',
      );

      await api.getJson('/tasks/task-1');
      await api.getJson('/tasks/task-1');

      expect(tokenCalls, 2);
      api.close();
    });

    test('does not send a request when Firebase ID token is absent', () async {
      var requestSent = false;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          requestSent = true;
          return http.Response('{}', 200);
        }),
        idTokenProvider: () async => null,
        forceRefreshTokenProvider: () async => null,
      );

      await expectLater(
        api.getJson('/tasks/task-1'),
        throwsA(isA<ApiException>()),
      );

      expect(requestSent, isFalse);
      api.close();
    });
  });

  // ---------------------------------------------------------------------------
  // 401 force-refresh retry
  // ---------------------------------------------------------------------------

  group('401 force-refresh retry', () {
    test('force-refreshes token and retries once after first 401', () async {
      var callCount = 0;
      var forceRefreshCalled = false;

      final api = ApiClient(
        httpClient: MockClient((request) async {
          callCount++;
          if (callCount == 1) {
            return http.Response(jsonEncode({'detail': 'token expired'}), 401);
          }
          expect(
            request.headers['Authorization'],
            'Bearer force-refreshed-token',
          );
          return http.Response(jsonEncode({'id': 'farm-1'}), 200);
        }),
        idTokenProvider: () async => 'initial-token',
        forceRefreshTokenProvider: () async {
          forceRefreshCalled = true;
          return 'force-refreshed-token';
        },
      );

      final result = await api.getJson('/farms/farm-1');

      expect(result['id'], 'farm-1');
      expect(callCount, 2);
      expect(forceRefreshCalled, isTrue);
      api.close();
    });

    test('returns response when retry after 401 succeeds', () async {
      var callCount = 0;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          callCount++;
          return callCount == 1
              ? http.Response('{"detail":"expired"}', 401)
              : _utf8JsonResponse({'name': 'Bugday tarlasi'}, 200);
        }),
        idTokenProvider: () async => 'old-token',
        forceRefreshTokenProvider: () async => 'new-token',
      );

      final result = await api.getJson('/farms/farm-1');

      expect(result['name'], 'Bugday tarlasi');
      api.close();
    });

    test('throws ApiException without further retry on second 401', () async {
      var callCount = 0;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          callCount++;
          return http.Response(jsonEncode({'detail': 'Oturum geçersiz.'}), 401);
        }),
        idTokenProvider: () async => 'token-a',
        forceRefreshTokenProvider: () async => 'token-b',
      );

      await expectLater(
        api.getJson('/farms'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 401)
              .having((e) => e.message, 'message', 'Oturum geçersiz.'),
        ),
      );

      expect(callCount, 2);
      api.close();
    });

    test(
      'throws ApiException immediately when force-refresh returns no token',
      () async {
        var callCount = 0;
        final api = ApiClient(
          httpClient: MockClient((request) async {
            callCount++;
            return http.Response('{"detail":"expired"}', 401);
          }),
          idTokenProvider: () async => 'initial-token',
          forceRefreshTokenProvider: () async => null,
        );

        await expectLater(api.getJson('/farms'), throwsA(isA<ApiException>()));

        expect(callCount, 1);
        api.close();
      },
    );
  });

  // ---------------------------------------------------------------------------
  // HTTP method coverage
  // ---------------------------------------------------------------------------

  group('HTTP methods', () {
    test('putJson sends PUT with body', () async {
      late http.Request captured;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response(jsonEncode({'updated': true}), 200);
        }),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      final result = await api.putJson('/farms/farm-1', {'name': 'Yeni isim'});

      expect(captured.method, 'PUT');
      expect(jsonDecode(captured.body), {'name': 'Yeni isim'});
      expect(result['updated'], isTrue);
      api.close();
    });

    test('patchJson sends PATCH with body', () async {
      late http.Request captured;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response(jsonEncode({'patched': true}), 200);
        }),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await api.patchJson('/farms/farm-1', {'size': 5.0});

      expect(captured.method, 'PATCH');
      api.close();
    });

    test('delete sends DELETE and completes on 204', () async {
      late http.Request captured;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response('', 204);
        }),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(api.delete('/farms/farm-1'), completes);
      expect(captured.method, 'DELETE');
      api.close();
    });
  });

  // ---------------------------------------------------------------------------
  // 3xx — must not be treated as success
  // ---------------------------------------------------------------------------

  group('3xx redirects are rejected', () {
    test('302 Found throws ApiException, not treated as success', () async {
      final api = ApiClient(
        httpClient: MockClient((_) async => http.Response('', 302)),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(api.getJson('/farms'), throwsA(isA<ApiException>()));
      api.close();
    });

    test(
      '304 Not Modified throws ApiException, not treated as success',
      () async {
        final api = ApiClient(
          httpClient: MockClient((_) async => http.Response('', 304)),
          idTokenProvider: () async => 'tok',
          forceRefreshTokenProvider: () async => 'tok2',
        );

        await expectLater(api.getJson('/farms'), throwsA(isA<ApiException>()));
        api.close();
      },
    );
  });

  // ---------------------------------------------------------------------------
  // 204 No Content
  // ---------------------------------------------------------------------------

  group('204 No Content', () {
    test('delete returns normally on 204 with empty body', () async {
      final api = ApiClient(
        httpClient: MockClient((_) async => http.Response('', 204)),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(api.delete('/items/1'), completes);
      api.close();
    });

    test('getJson returns empty map on 204', () async {
      final api = ApiClient(
        httpClient: MockClient((_) async => http.Response('', 204)),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      final result = await api.getJson('/items/1');
      expect(result, isEmpty);
      api.close();
    });
  });

  // ---------------------------------------------------------------------------
  // Error parsing
  // ---------------------------------------------------------------------------

  group('error parsing', () {
    test('detail string error is extracted from response body', () async {
      final api = ApiClient(
        httpClient: MockClient(
          (_) async => _utf8JsonResponse({'detail': 'Tarla bulunamadı.'}, 404),
        ),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(
        api.getJson('/farms/unknown'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 404)
              .having((e) => e.message, 'message', 'Tarla bulunamadı.'),
        ),
      );
      api.close();
    });

    test(
      'detail list (FastAPI validation) is mapped to a safe message',
      () async {
        final api = ApiClient(
          httpClient: MockClient(
            (_) async => http.Response(
              jsonEncode({
                'detail': [
                  {
                    'loc': ['body', 'name'],
                    'msg': 'field required',
                    'type': 'value_error.missing',
                  },
                ],
              }),
              422,
            ),
          ),
          idTokenProvider: () async => 'tok',
          forceRefreshTokenProvider: () async => 'tok2',
        );

        await expectLater(
          api.postJson('/farms', {}),
          throwsA(
            isA<ApiException>()
                .having((e) => e.statusCode, 'statusCode', 422)
                .having(
                  (e) => e.message,
                  'message',
                  'Gönderilen bilgiler doğrulanamadı.',
                ),
          ),
        );
        api.close();
      },
    );

    test('non-JSON body falls back to status-based message', () async {
      final api = ApiClient(
        httpClient: MockClient(
          (_) async => http.Response('Internal Server Error', 500),
        ),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(
        api.getJson('/farms'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 500)
              .having(
                (e) => e.message,
                'message',
                contains('Sunucuda bir sorun'),
              ),
        ),
      );
      api.close();
    });

    test('403 returns correct Turkish message', () async {
      final api = ApiClient(
        httpClient: MockClient((_) async => http.Response('{}', 403)),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(
        api.getJson('/admin'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'Bu işlem için yetkiniz bulunmuyor.',
          ),
        ),
      );
      api.close();
    });

    test('429 sets retryable=true', () async {
      final api = ApiClient(
        httpClient: MockClient((_) async => http.Response('{}', 429)),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await expectLater(
        api.getJson('/farms'),
        throwsA(
          isA<ApiException>().having((e) => e.retryable, 'retryable', isTrue),
        ),
      );
      api.close();
    });
  });

  // ---------------------------------------------------------------------------
  // sendQueued backward compatibility
  // ---------------------------------------------------------------------------

  group('sendQueued', () {
    test('sends queued operation with correct method and body', () async {
      late http.Request captured;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response('', 204);
        }),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'tok2',
      );

      await api.sendQueued(
        method: 'POST',
        endpoint: '/farms/farm-1/activities',
        body: {
          'client_operation_id': 'op-1',
          'activity_type': 'IRRIGATION',
          'description': 'Sulama yapıldı',
        },
      );

      expect(captured.method, 'POST');
      final body = jsonDecode(captured.body) as Map;
      expect(body['client_operation_id'], 'op-1');
      expect(body['activity_type'], 'IRRIGATION');
      api.close();
    });

    test('sendQueued retries once on 401 like other methods', () async {
      var callCount = 0;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          callCount++;
          return callCount == 1
              ? http.Response('{"detail":"expired"}', 401)
              : http.Response('', 204);
        }),
        idTokenProvider: () async => 'tok',
        forceRefreshTokenProvider: () async => 'new-tok',
      );

      await expectLater(
        api.sendQueued(
          method: 'POST',
          endpoint: '/farms/farm-1/activities',
          body: {'client_operation_id': 'op-2'},
        ),
        completes,
      );

      expect(callCount, 2);
      api.close();
    });
  });

  // ---------------------------------------------------------------------------
  // Multipart requests
  // ---------------------------------------------------------------------------

  group('postMultipart', () {
    test('sends fields and files with Bearer token header', () async {
      http.BaseRequest? capturedRequest;
      final api = ApiClient(
        httpClient: MockClient.streaming((request, bodyStream) async {
          capturedRequest = request;
          final responseBody = utf8.encode(jsonEncode({'reply': 'Tamam'}));
          return http.StreamedResponse(
            Stream.value(responseBody),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'test-token',
        forceRefreshTokenProvider: () async => 'refreshed-token',
      );

      final result = await api.postMultipart(
        '/ai/chat',
        fields: {'message': 'Fotoğraf analizi'},
        files: [
          http.MultipartFile.fromBytes(
            'photo',
            [1, 2, 3],
            filename: 'test.jpg',
          ),
        ],
      );

      expect(result['reply'], 'Tamam');
      expect(capturedRequest, isA<http.MultipartRequest>());
      final mp = capturedRequest as http.MultipartRequest;
      expect(mp.headers['Authorization'], 'Bearer test-token');
      expect(mp.headers['Accept'], 'application/json');
      expect(mp.fields['message'], 'Fotoğraf analizi');
      expect(mp.files, hasLength(1));
      expect(mp.files.first.field, 'photo');
      expect(mp.files.first.filename, 'test.jpg');
      api.close();
    });

    test('retries once on 401 with refreshed token', () async {
      var callCount = 0;
      final api = ApiClient(
        httpClient: MockClient.streaming((request, bodyStream) async {
          callCount++;
          if (callCount == 1) {
            return http.StreamedResponse(
              Stream.value(utf8.encode(jsonEncode({'detail': 'expired'}))),
              401,
              headers: {'content-type': 'application/json'},
            );
          }
          return http.StreamedResponse(
            Stream.value(utf8.encode(jsonEncode({'reply': 'Başarılı'}))),
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'initial-token',
        forceRefreshTokenProvider: () async => 'refreshed-token',
      );

      final result = await api.postMultipart(
        '/ai/chat',
        fields: {'message': 'Test'},
      );

      expect(result['reply'], 'Başarılı');
      expect(callCount, 2);
      api.close();
    });

    test('throws ApiException on 422 or 500 error response', () async {
      final api = ApiClient(
        httpClient: MockClient.streaming((request, bodyStream) async {
          return http.StreamedResponse(
            Stream.value(utf8.encode(jsonEncode({'detail': 'photo en fazla 5 MB olabilir.'}))),
            422,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'test-token',
        forceRefreshTokenProvider: () async => 'refreshed-token',
      );

      expect(
        () => api.postMultipart('/ai/chat', fields: {'message': 'Test'}),
        throwsA(isA<ApiException>().having((e) => e.message, 'message', 'photo en fazla 5 MB olabilir.')),
      );
      api.close();
    });
  });
}

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/features/cases/data/backend_case_repository.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
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

void main() {
  group('BackendCaseRepository', () {
    test('createCase without media calls /cases directly and returns id', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/cases')) {
            return http.Response(
              '{"id":"case-uuid-1","title":"Test","status":"Open"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final CaseRepository repository = BackendCaseRepository(apiClient: client);

      final resultId = await repository.createCase(
        const CreateCaseInput(
          farmId: 'farm-1',
          category: CaseCategory.disease,
          title: 'Pas Hastalığı',
          description: 'Yapraklarda sarı pas lekeleri görüldü.',
        ),
      );

      expect(resultId, 'case-uuid-1');
      expect(requests.length, 1);
      expect(requests.first.url.path, endsWith('/cases'));
    });

    test('createCase with media calls /media first then /cases with mediaIds', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/media')) {
            return http.Response(
              '{"id":"media-uuid-1","url":"https://example.com/media-1"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          if (req.url.path.endsWith('/cases')) {
            return http.Response(
              '{"id":"case-uuid-2","title":"Test with media"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repository = BackendCaseRepository(apiClient: client);

      final resultId = await repository.createCase(
        const CreateCaseInput(
          farmId: 'farm-1',
          category: CaseCategory.pest,
          title: 'Yeşil Kurt',
          description: 'Meyvelerde delikler var.',
          imageBytes: [1, 2, 3, 4],
          imageFileName: 'kurt.jpg',
        ),
      );

      expect(resultId, 'case-uuid-2');
      expect(requests.length, 2);
      expect(requests[0].url.path, endsWith('/media'));
      expect(requests[1].url.path, endsWith('/cases'));
    });
  });
}

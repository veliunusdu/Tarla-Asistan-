import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/features/cases/data/backend_case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
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
  group('BackendCaseRepository - List & Chat', () {
    test('getCases calls /cases with optional query parameters and parses list', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/cases')) {
            return http.Response(
              '''
              {
                "items": [
                  {
                    "id": "c-1",
                    "farm_id": "f-1",
                    "farm_name": "Domates Tarlası",
                    "category": "Disease",
                    "priority": "High",
                    "status": "InReview",
                    "title": "Yaprak Sararması",
                    "created_at_utc": "2026-09-03T10:00:00Z",
                    "updated_at_utc": "2026-09-03T11:00:00Z",
                    "message_count": 2,
                    "media_count": 1
                  }
                ],
                "total": 1
              }
              ''',
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repository = BackendCaseRepository(apiClient: client);

      final list = await repository.getCases(
        farmId: 'f-1',
        status: CaseStatus.inReview,
      );

      expect(list.length, 1);
      expect(list.first.id, 'c-1');
      expect(list.first.farmName, 'Domates Tarlası');
      expect(list.first.category, CaseCategory.disease);
      expect(list.first.status, CaseStatus.inReview);
      expect(requests.first.url.queryParameters['farmId'], 'f-1');
      expect(requests.first.url.queryParameters['status'], 'InReview');
    });

    test('getCaseById calls /cases/{id} and parses details with messages', () async {
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          if (req.url.path.endsWith('/cases/c-1')) {
            return http.Response(
              '''
              {
                "id": "c-1",
                "farm_id": "f-1",
                "farm_name": "Domates Tarlası",
                "category": "Disease",
                "priority": "High",
                "status": "WaitingFarmer",
                "title": "Yaprak Sararması",
                "description": "Alt yapraklar sarardı ve kurudu.",
                "created_at_utc": "2026-09-03T10:00:00Z",
                "media": [
                  {"id": "m-1", "url": "https://example.com/yaprak.jpg"}
                ],
                "messages": [
                  {
                    "id": "msg-1",
                    "case_id": "c-1",
                    "sender_id": "expert-99",
                    "sender_name": "Ziraat Müh. Ahmet",
                    "message_type": "AdditionalInfoRequest",
                    "body": "Sulama sıklığınız nedir?",
                    "created_at_utc": "2026-09-03T11:00:00Z",
                    "media": []
                  }
                ]
              }
              ''',
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repository = BackendCaseRepository(apiClient: client);
      final detail = await repository.getCaseById('c-1');

      expect(detail.id, 'c-1');
      expect(detail.status, CaseStatus.waitingFarmer);
      expect(detail.initialMediaUrls, ['https://example.com/yaprak.jpg']);
      expect(detail.messages.length, 1);
      expect(detail.messages.first.isFromExpert, isTrue);
      expect(detail.messages.first.messageType, CaseMessageType.additionalInfoRequest);
      expect(detail.messages.first.body, 'Sulama sıklığınız nedir?');
    });

    test('sendMessage sends message to /cases/{id}/messages and uploads media if present', () async {
      final requests = <http.BaseRequest>[];
      final client = ApiClient(
        httpClient: MockHttpClient((req) async {
          requests.add(req);
          if (req.url.path.endsWith('/media')) {
            return http.Response(
              '{"id":"m-reply-1","url":"https://example.com/reply.jpg"}',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          if (req.url.path.endsWith('/cases/c-1/messages')) {
            return http.Response(
              '''
              {
                "id": "msg-reply",
                "case_id": "c-1",
                "sender_id": "farmer-1",
                "sender_name": "Ben",
                "message_type": "Comment",
                "body": "Haftada iki kez damlama ile suluyorum.",
                "created_at_utc": "2026-09-03T11:30:00Z",
                "media": [
                  {"id": "m-reply-1", "url": "https://example.com/reply.jpg"}
                ]
              }
              ''',
              201,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('{"detail":"not found"}', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repository = BackendCaseRepository(apiClient: client);

      final msg = await repository.sendMessage(
        'c-1',
        body: 'Haftada iki kez damlama ile suluyorum.',
        imageBytes: [1, 2, 3],
        imageFileName: 'sulama.jpg',
      );

      expect(msg.id, 'msg-reply');
      expect(msg.mediaUrls, ['https://example.com/reply.jpg']);
      expect(requests.length, 2);
      expect(requests[0].url.path, endsWith('/media'));
      expect(requests[1].url.path, endsWith('/cases/c-1/messages'));
    });
  });
}

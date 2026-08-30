import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/activities/data/backend_faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/services/api_client.dart';

class _FakeTarlaRepository implements TarlaRepository {
  _FakeTarlaRepository(this.tarlalar);
  final List<Tarla> tarlalar;

  @override
  Future<List<Tarla>> getTarlalar() async => tarlalar;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

Tarla _tarla(String id, String name) => Tarla(
  id: id,
  name: name,
  latitude: 38.4,
  longitude: 27.1,
  size: 10,
  cropType: 'Buğday',
  plantingDate: DateTime(2026, 3, 1),
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('BackendFaaliyetRepository', () {
    test('addFaaliyet sends POST to /farms/{farmId}/activities with correct payload', () async {
      String? capturedPath;
      Map<String, dynamic>? capturedBody;

      final client = ApiClient(
        httpClient: MockClient((request) async {
          capturedPath = request.url.path;
          capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'backend-act-1',
              'farm_id': 'farm-123',
              'activity_type': 'IRRIGATION',
              'description': 'Sabah sulaması',
              'occurred_at_utc': '2026-08-30T08:00:00.000Z',
              'status': 'Confirmed',
              'source': 'Manual',
            }),
            201,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      final testFaaliyet = Faaliyet(
        id: 'local-1',
        tarlaId: 'farm-123',
        type: 'Sulama',
        note: 'Sabah sulaması',
        timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
        isCompleted: true,
      );

      await repo.addFaaliyet(testFaaliyet);

      expect(capturedPath, '/api/v1/farms/farm-123/activities');
      expect(capturedBody?['activity_type'], 'IRRIGATION');
      expect(capturedBody?['description'], 'Sabah sulaması');
      expect(capturedBody?['input_method'], 'Manual');
      expect(capturedBody?['occurred_at'], contains('2026-08-30'));
    });

    test('addFaaliyet maps activity types correctly to backend enum values', () async {
      final capturedTypes = <String>[];

      final client = ApiClient(
        httpClient: MockClient((request) async {
          final body = jsonDecode(request.body) as Map<String, dynamic>;
          capturedTypes.add(body['activity_type'] as String);
          return http.Response(
            jsonEncode({
              'id': 'backend-act-id',
              'farm_id': 'farm-1',
              'activity_type': body['activity_type'],
              'description': body['description'],
              'occurred_at_utc': '2026-08-30T08:00:00.000Z',
              'status': 'Confirmed',
              'source': 'Manual',
            }),
            201,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      final types = [
        ('Sulama', 'IRRIGATION'),
        ('Gübreleme', 'FERTILIZATION'),
        ('İlaçlama', 'SPRAYING'),
        ('Budama', 'PRUNING'),
        ('Hasat', 'HARVEST'),
        ('Tarla Kontrolü', 'FIELD_CHECK'),
        ('Ekim', 'OTHER'),
        ('Özel Görev', 'OTHER'),
      ];

      for (final (mobileType, backendType) in types) {
        await repo.addFaaliyet(
          Faaliyet(
            id: 'id',
            tarlaId: 'farm-1',
            type: mobileType,
            note: 'Açıklama',
            timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
          ),
        );
        expect(capturedTypes.last, backendType);
      }
    });

    test('addFaaliyet ensures description meets backend minimum length of 2 chars', () async {
      Map<String, dynamic>? capturedBody;

      final client = ApiClient(
        httpClient: MockClient((request) async {
          capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'backend-id',
              'farm_id': 'farm-1',
              'activity_type': 'IRRIGATION',
              'description': capturedBody?['description'],
              'occurred_at_utc': '2026-08-30T08:00:00.000Z',
              'status': 'Confirmed',
              'source': 'Manual',
            }),
            201,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      await repo.addFaaliyet(
        Faaliyet(
          id: 'id',
          tarlaId: 'farm-1',
          type: 'Sulama',
          note: '',
          timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
        ),
      );

      expect(capturedBody?['description'], 'Sulama');
      expect((capturedBody?['description'] as String).length, greaterThanOrEqualTo(2));
    });

    test('addFaaliyet throws when backend returns error status', () async {
      final client = ApiClient(
        httpClient: MockClient((request) async {
          return http.Response(
            jsonEncode({'message': 'Farm not found'}),
            404,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      expect(
        () => repo.addFaaliyet(
          Faaliyet(
            id: 'id',
            tarlaId: 'nonexistent-farm',
            type: 'Sulama',
            note: 'Not',
            timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
          ),
        ),
        throwsA(isA<ApiException>()),
      );
    });

    test('getFaaliyetler reads from /farms/{farmId}/activities and maps response', () async {
      final client = ApiClient(
        httpClient: MockClient((request) async {
          expect(request.url.path, '/api/v1/farms/farm-1/activities');
          return http.Response(
            jsonEncode({
              'items': [
                {
                  'id': 'act-1',
                  'farm_id': 'farm-1',
                  'activity_type': 'IRRIGATION',
                  'description': 'Sulama yapıldı',
                  'occurred_at_utc': '2026-08-29T10:00:00.000Z',
                  'status': 'Confirmed',
                  'source': 'Manual',
                },
                {
                  'id': 'act-2',
                  'farm_id': 'farm-1',
                  'activity_type': 'FERTILIZATION',
                  'description': 'Gübre atıldı',
                  'occurred_at_utc': '2026-08-28T09:00:00.000Z',
                  'status': 'Confirmed',
                  'source': 'Manual',
                },
              ],
              'total': 2,
              'limit': 50,
              'offset': 0,
            }),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      final list = await repo.getFaaliyetler('farm-1');

      expect(list, hasLength(2));
      expect(list[0].id, 'act-1');
      expect(list[0].tarlaId, 'farm-1');
      expect(list[0].type, 'Sulama');
      expect(list[0].note, 'Sulama yapıldı');
      expect(list[0].isCompleted, isTrue);
      expect(list[0].timestamp.toUtc(), DateTime.utc(2026, 8, 29, 10, 0, 0));

      expect(list[1].id, 'act-2');
      expect(list[1].type, 'Gübreleme');
      expect(list[1].note, 'Gübre atıldı');
    });

    test('getTumFaaliyetler queries all farms and merges activities sorted descending', () async {
      final client = ApiClient(
        httpClient: MockClient((request) async {
          if (request.url.path == '/api/v1/farms/farm-1/activities') {
            return http.Response(
              jsonEncode({
                'items': [
                  {
                    'id': 'act-1',
                    'farm_id': 'farm-1',
                    'activity_type': 'IRRIGATION',
                    'description': 'Eski Sulama',
                    'occurred_at_utc': '2026-08-20T10:00:00.000Z',
                    'status': 'Confirmed',
                    'source': 'Manual',
                  },
                ],
                'total': 1,
                'limit': 50,
                'offset': 0,
              }),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          } else if (request.url.path == '/api/v1/farms/farm-2/activities') {
            return http.Response(
              jsonEncode({
                'items': [
                  {
                    'id': 'act-2',
                    'farm_id': 'farm-2',
                    'activity_type': 'HARVEST',
                    'description': 'Yeni Hasat',
                    'occurred_at_utc': '2026-08-25T10:00:00.000Z',
                    'status': 'Confirmed',
                    'source': 'Manual',
                  },
                ],
                'total': 1,
                'limit': 50,
                'offset': 0,
              }),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }
          return http.Response('Not Found', 404);
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([
          _tarla('farm-1', 'Tarla 1'),
          _tarla('farm-2', 'Tarla 2'),
        ]),
      );

      final all = await repo.getTumFaaliyetler();

      expect(all, hasLength(2));
      expect(all[0].id, 'act-2');
      expect(all[0].type, 'Hasat');
      expect(all[1].id, 'act-1');
      expect(all[1].type, 'Sulama');
    });

    test('getTumFaaliyetler returns empty list if no farms exist', () async {
      final client = ApiClient(
        httpClient: MockClient((request) async => http.Response('[]', 200)),
        idTokenProvider: () async => 'dummy-token',
      );

      final repo = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      final all = await repo.getTumFaaliyetler();
      expect(all, isEmpty);
    });
  });
}

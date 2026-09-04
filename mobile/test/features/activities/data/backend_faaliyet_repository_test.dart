import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/activities/data/backend_faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/services/api_client.dart';
import 'package:uuid/uuid.dart';

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
    test('addPlanliGorev creates a manual farm task', () async {
      String? capturedPath;
      Map<String, dynamic>? capturedBody;
      final client = ApiClient(
        httpClient: MockClient((request) async {
          capturedPath = request.url.path;
          capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'id': 'task-1',
              'farm_id': 'farm-1',
              'title': 'Sulama',
              'description': 'Akşam sulaması',
              'reason': 'Çiftçi tarafından planlandı',
              'priority': 'MEDIUM',
              'status': 'NEW',
              'source': 'MANUAL',
              'confidence': 'HIGH',
              'due_date': '2026-09-03',
              'created_at_utc': '2026-09-01T10:00:00Z',
              'updated_at_utc': '2026-09-01T10:00:00Z',
              'expert_review_recommended': false,
            }),
            201,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );
      final repository = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      await repository.addPlanliGorev(
        Faaliyet(
          id: 'local-task',
          tarlaId: 'farm-1',
          type: 'Sulama',
          note: 'Akşam sulaması',
          timestamp: DateTime(2026, 9, 1),
          dueDate: DateTime(2026, 9, 3),
        ),
      );

      expect(capturedPath, '/api/v1/farms/farm-1/tasks');
      expect(capturedBody?['title'], 'Sulama');
      expect(capturedBody?['due_date'], '2026-09-03');
    });

    test('completePlanliGorev completes the task through backend', () async {
      String? capturedMethod;
      String? capturedPath;
      Map<String, dynamic>? capturedBody;
      final client = ApiClient(
        httpClient: MockClient((request) async {
          capturedMethod = request.method;
          capturedPath = request.url.path;
          capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({'id': 'task-1', 'status': 'COMPLETED'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );
      final repository = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      await repository.completePlanliGorev('task-1', note: 'Tamamlandı');

      expect(capturedMethod, 'POST');
      expect(capturedPath, '/api/v1/tasks/task-1/complete');
      expect(capturedBody?['note'], 'Tamamlandı');
    });

    test('deleteFaaliyet archives the activity through the backend', () async {
      String? capturedMethod;
      String? capturedPath;
      final client = ApiClient(
        httpClient: MockClient((request) async {
          capturedMethod = request.method;
          capturedPath = request.url.path;
          return http.Response('', 204);
        }),
        idTokenProvider: () async => 'dummy-token',
      );
      final repository = BackendFaaliyetRepository(
        apiClient: client,
        tarlaRepository: _FakeTarlaRepository([]),
      );

      await repository.deleteFaaliyet('activity-1');

      expect(capturedMethod, 'DELETE');
      expect(capturedPath, '/api/v1/activities/activity-1');
    });

    test(
      'getPlanliGorevler includes open planned tasks from backend',
      () async {
        final client = ApiClient(
          httpClient: MockClient((request) async {
            if (request.url.path == '/api/v1/farms/farm-1/activities') {
              return http.Response(
                jsonEncode({
                  'items': <Object>[],
                  'total': 0,
                  'limit': 50,
                  'offset': 0,
                }),
                200,
                headers: {'content-type': 'application/json'},
              );
            }
            if (request.url.path == '/api/v1/farms/farm-1/tasks/all') {
              return http.Response(
                jsonEncode([
                  {
                    'id': 'task-1',
                    'farm_id': 'farm-1',
                    'title': 'Sulama',
                    'description': 'Akşam sulaması',
                    'status': 'NEW',
                    'due_date': '2026-09-03',
                    'created_at_utc': '2026-09-01T10:00:00Z',
                  },
                ]),
                200,
                headers: {'content-type': 'application/json'},
              );
            }
            return http.Response('Not found', 404);
          }),
          idTokenProvider: () async => 'dummy-token',
        );
        final repository = BackendFaaliyetRepository(
          apiClient: client,
          tarlaRepository: _FakeTarlaRepository([_tarla('farm-1', 'Tarla 1')]),
        );

        final result = await repository.getPlanliGorevler();

        expect(result, hasLength(1));
        expect(result.single.id, 'task-1');
        expect(result.single.type, 'Sulama');
        expect(result.single.note, 'Akşam sulaması');
        expect(result.single.dueDate, DateTime(2026, 9, 3));
        expect(result.single.isCompleted, isFalse);
      },
    );

    test(
      'getTumFaaliyetler fetches activities per farm and returns completed records with dueDate null',
      () async {
        final client = ApiClient(
          httpClient: MockClient((request) async {
            if (request.url.path == '/api/v1/farms/farm-1/activities') {
              return http.Response(
                jsonEncode({
                  'items': [
                    {
                      'id': 'activity-1',
                      'farm_id': 'farm-1',
                      'activity_type': 'FERTILIZATION',
                      'description': 'Taban gübresi',
                      'occurred_at_utc': '2026-09-02T10:00:00Z',
                    },
                  ],
                  'total': 1,
                  'limit': 50,
                  'offset': 0,
                }),
                200,
                headers: {'content-type': 'application/json'},
              );
            }
            return http.Response('Not found', 404);
          }),
          idTokenProvider: () async => 'dummy-token',
        );
        final repository = BackendFaaliyetRepository(
          apiClient: client,
          tarlaRepository: _FakeTarlaRepository([_tarla('farm-1', 'Tarla 1')]),
        );

        final result = await repository.getTumFaaliyetler();

        expect(result, hasLength(1));
        expect(result.single.id, 'activity-1');
        expect(result.single.type, 'Gübreleme');
        expect(result.single.isCompleted, isTrue);
        expect(result.single.dueDate, isNull);
      },
    );

    test(
      'addFaaliyet sends POST to /farms/{farmId}/activities with correct payload',
      () async {
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
        expect(capturedBody?['client_operation_id'], isNotNull);
        expect(
          Uuid.isValidUUID(
            fromString: capturedBody!['client_operation_id'] as String,
          ),
          isTrue,
        );
      },
    );

    test(
      'addFaaliyet generates deterministic client_operation_id for the same faaliyet id',
      () async {
        final capturedIds = <String>[];

        final client = ApiClient(
          httpClient: MockClient((request) async {
            final body = jsonDecode(request.body) as Map<String, dynamic>;
            capturedIds.add(body['client_operation_id'] as String);
            return http.Response(
              jsonEncode({
                'id': 'backend-act-id',
                'farm_id': 'farm-1',
                'activity_type': 'IRRIGATION',
                'description': 'Sulama',
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

        final faaliyet = Faaliyet(
          id: '1725062400000',
          tarlaId: 'farm-1',
          type: 'Sulama',
          note: 'Tekrar deneme testi',
          timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
        );

        await repo.addFaaliyet(faaliyet);
        await repo.addFaaliyet(faaliyet);

        expect(capturedIds, hasLength(2));
        expect(capturedIds[0], capturedIds[1]);
        expect(Uuid.isValidUUID(fromString: capturedIds[0]), isTrue);
      },
    );

    test(
      'addFaaliyet maps activity types correctly to backend enum values',
      () async {
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
      },
    );

    test(
      'addFaaliyet preserves user description directly without forging synthetic fallback',
      () async {
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
            note: 'Damla sulama yapıldı',
            timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
          ),
        );

        expect(capturedBody?['description'], 'Damla sulama yapıldı');
      },
    );

    test(
      'addFaaliyet does not replace empty description with activity type or synthetic text',
      () async {
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
            note: '  ',
            timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
          ),
        );

        expect(capturedBody?['description'], '');
      },
    );

    test(
      'addFaaliyet preserves future or exact timestamp without clamping to DateTime.now()',
      () async {
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
                'occurred_at_utc': capturedBody?['occurred_at'],
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

        final futureDate = DateTime.utc(2028, 6, 15, 14, 30, 0);
        await repo.addFaaliyet(
          Faaliyet(
            id: 'id',
            tarlaId: 'farm-1',
            type: 'Sulama',
            note: 'Gelecek sulama',
            timestamp: futureDate,
          ),
        );

        expect(capturedBody?['occurred_at'], '2028-06-15T14:30:00.000Z');
      },
    );

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
            note: 'Sulama',
            timestamp: DateTime.utc(2026, 8, 30, 8, 0, 0),
          ),
        ),
        throwsA(isA<ApiException>()),
      );
    });

    test(
      'getFaaliyetler reads from /farms/{farmId}/activities and maps response',
      () async {
        final client = ApiClient(
          httpClient: MockClient((request) async {
            expect(request.url.path, '/api/v1/farms/farm-1/activities');
            expect(request.url.queryParameters['limit'], '50');
            expect(request.url.queryParameters['offset'], '0');
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
      },
    );

    test(
      'getFaaliyetler paginates multiple pages until all items are fetched (75 items in 2 pages)',
      () async {
        final requestedOffsets = <int>[];
        final requestedLimits = <int>[];

        final client = ApiClient(
          httpClient: MockClient((request) async {
            expect(request.url.path, '/api/v1/farms/farm-1/activities');
            final limit = int.parse(request.url.queryParameters['limit']!);
            final offset = int.parse(request.url.queryParameters['offset']!);
            requestedLimits.add(limit);
            requestedOffsets.add(offset);

            if (offset == 0) {
              final items = List.generate(
                50,
                (i) => {
                  'id': 'act-$i',
                  'farm_id': 'farm-1',
                  'activity_type': 'IRRIGATION',
                  'description': 'Sulama $i',
                  'occurred_at_utc': DateTime.utc(
                    2026,
                    8,
                    20,
                    10,
                    i,
                  ).toIso8601String(),
                  'status': 'Confirmed',
                  'source': 'Manual',
                },
              );
              return http.Response(
                jsonEncode({
                  'items': items,
                  'total': 75,
                  'limit': limit,
                  'offset': offset,
                }),
                200,
                headers: {'content-type': 'application/json; charset=utf-8'},
              );
            } else if (offset == 50) {
              final items = List.generate(
                25,
                (i) => {
                  'id': 'act-${50 + i}',
                  'farm_id': 'farm-1',
                  'activity_type': 'FERTILIZATION',
                  'description': 'Gübreleme $i',
                  'occurred_at_utc': DateTime.utc(
                    2026,
                    8,
                    21,
                    10,
                    i,
                  ).toIso8601String(),
                  'status': 'Confirmed',
                  'source': 'Manual',
                },
              );
              return http.Response(
                jsonEncode({
                  'items': items,
                  'total': 75,
                  'limit': limit,
                  'offset': offset,
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
          tarlaRepository: _FakeTarlaRepository([]),
        );

        final list = await repo.getFaaliyetler('farm-1');

        expect(list, hasLength(75));
        expect(requestedLimits, [50, 50]);
        expect(requestedOffsets, [0, 50]);
        // Verify sorted descending by timestamp
        for (int i = 0; i < list.length - 1; i++) {
          expect(
            list[i].timestamp.isAfter(list[i + 1].timestamp) ||
                list[i].timestamp.isAtSameMomentAs(list[i + 1].timestamp),
            isTrue,
          );
        }
      },
    );

    test(
      'getFaaliyetler does not make a second request when first page has all items',
      () async {
        int requestCount = 0;

        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestCount++;
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
                ],
                'total': 1,
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

        expect(list, hasLength(1));
        expect(requestCount, 1);
      },
    );

    test(
      'getFaaliyetler terminates safely when empty items list is returned even if total is large',
      () async {
        int requestCount = 0;

        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestCount++;
            return http.Response(
              jsonEncode({'items': [], 'total': 100, 'limit': 50, 'offset': 0}),
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

        expect(list, isEmpty);
        expect(requestCount, 1);
      },
    );

    test(
      'getTumFaaliyetler queries all farms and merges activities sorted descending',
      () async {
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
      },
    );

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

    test(
      'addFaaliyet sends activity_name with farmer free-text "Damla sulama yaptım" (Test 1)',
      () async {
        Map<String, dynamic>? capturedBody;

        final client = ApiClient(
          httpClient: MockClient((request) async {
            capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
            return http.Response(
              jsonEncode({
                'id': 'backend-act-1',
                'farm_id': 'farm-1',
                'activity_name': capturedBody?['activity_name'],
                'activity_type': capturedBody?['activity_type'],
                'description': capturedBody?['description'],
                'occurred_at_utc': '2026-09-04T10:00:00.000Z',
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
            id: 'id-1',
            tarlaId: 'farm-1',
            type: 'Damla sulama yaptım',
            note: '3 saat açık kaldı',
            timestamp: DateTime.utc(2026, 9, 4, 10, 0, 0),
          ),
        );

        expect(capturedBody?['activity_name'], 'Damla sulama yaptım');
      },
    );

    test(
      'fromBackendJson maps activity_name directly to Faaliyet.type for "Çapa" and "Fidan bağlama" (Test 2, 3, 16)',
      () {
        final f1 = BackendFaaliyetRepository.fromBackendJson({
          'id': 'act-capa',
          'farm_id': 'farm-1',
          'activity_name': 'Çapa',
          'activity_type': 'OTHER',
          'description': 'Yabani otlar temizlendi',
          'occurred_at_utc': '2026-09-04T10:00:00.000Z',
        }, fallbackTarlaId: 'farm-1');

        expect(f1.type, 'Çapa');
        expect(f1.note, 'Yabani otlar temizlendi');

        final f2 = BackendFaaliyetRepository.fromBackendJson({
          'id': 'act-fidan',
          'farm_id': 'farm-1',
          'activity_name': 'Fidan bağlama',
          'activity_type': 'OTHER',
          'description': '',
          'occurred_at_utc': '2026-09-04T10:00:00.000Z',
        }, fallbackTarlaId: 'farm-1');

        expect(f2.type, 'Fidan bağlama');
      },
    );

    test(
      'fromBackendJson falls back to Turkish enum name when activity_name is null or empty (Test 8)',
      () {
        final legacySulama = BackendFaaliyetRepository.fromBackendJson({
          'id': 'act-legacy-1',
          'farm_id': 'farm-1',
          'activity_type': 'IRRIGATION',
          'description': 'Eski sulama kaydı',
          'occurred_at_utc': '2026-08-01T10:00:00.000Z',
        }, fallbackTarlaId: 'farm-1');

        expect(legacySulama.type, 'Sulama');

        final legacyGubreleme = BackendFaaliyetRepository.fromBackendJson({
          'id': 'act-legacy-2',
          'farm_id': 'farm-1',
          'activity_name': '',
          'activity_type': 'FERTILIZATION',
          'description': '',
          'occurred_at_utc': '2026-08-01T10:00:00.000Z',
        }, fallbackTarlaId: 'farm-1');

        expect(legacyGubreleme.type, 'Gübreleme');
      },
    );

    test(
      'fromBackendJson handles legacy OTHER records gracefully (Test 9)',
      () {
        final legacyOther = BackendFaaliyetRepository.fromBackendJson({
          'id': 'act-legacy-other',
          'farm_id': 'farm-1',
          'activity_type': 'OTHER',
          'description': 'Özel iş',
          'occurred_at_utc': '2026-08-01T10:00:00.000Z',
        }, fallbackTarlaId: 'farm-1');

        expect(legacyOther.type, 'Diğer');
        expect(legacyOther.note, 'Özel iş');
      },
    );
  });
}

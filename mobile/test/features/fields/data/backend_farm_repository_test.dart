import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/fields/data/backend_farm_repository.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/services/api_client.dart';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// Full FarmResponse JSON from the API.
const _fullFarmJson = <String, dynamic>{
  'id': 'farm-uuid-1',
  'owner_id': 'owner-uuid-1',
  'name': 'Test Tarla',
  'latitude': 39.92,
  'longitude': 32.85,
  'size_in_hectares': 12.5,
  'irrigation_method': 'DRIP',
  'soil_type': null,
  'note': null,
  'archived_at': null,
  'created_at': '2026-08-01T10:00:00Z',
  'updated_at': '2026-08-20T15:30:00Z',
  'current_crop': {
    'id': 'crop-1',
    'farm_id': 'farm-uuid-1',
    'crop_type': 'WHEAT',
    'variety': null,
    'planted_at': '2026-03-15',
    'harvested_at': null,
    'status': 'ACTIVE',
    'created_at': '2026-03-15T00:00:00Z',
    'updated_at': '2026-03-15T00:00:00Z',
  },
};

const _farmListJson = <dynamic>[_fullFarmJson];

const _createJson = <String, dynamic>{'id': 'farm-uuid-1'};

const _mutationJson = <String, dynamic>{
  'farm': _fullFarmJson,
  'warnings': <dynamic>[],
};

/// Creates an [ApiClient] whose HTTP layer is replaced by [handler].
/// No real network calls are made.
ApiClient _clientWith(Future<http.Response> Function(http.Request) handler) =>
    ApiClient(
      httpClient: MockClient(handler),
      idTokenProvider: () async => 'test-firebase-token',
      forceRefreshTokenProvider: () async => 'refreshed-token',
    );

http.Response _json(Object body, int statusCode) => http.Response.bytes(
  utf8.encode(jsonEncode(body)),
  statusCode,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  // -------------------------------------------------------------------------
  // GET /farms
  // -------------------------------------------------------------------------

  group('getFarms', () {
    test('sends correct query parameters', () async {
      late Uri captured;
      final client = _clientWith((request) async {
        captured = request.url;
        return _json(_farmListJson, 200);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await repo.getFarms(includeArchived: true, limit: 20, offset: 40);

      expect(captured.path, endsWith('/farms'));
      expect(captured.queryParameters['include_archived'], 'true');
      expect(captured.queryParameters['limit'], '20');
      expect(captured.queryParameters['offset'], '40');
      client.close();
    });

    test('uses default parameters when none are supplied', () async {
      late Uri captured;
      final client = _clientWith((request) async {
        captured = request.url;
        return _json(_farmListJson, 200);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await repo.getFarms();

      expect(captured.queryParameters['include_archived'], 'false');
      expect(captured.queryParameters['limit'], '50');
      expect(captured.queryParameters['offset'], '0');
      client.close();
    });

    test('returns FarmListResponseDto with correct pagination', () async {
      final client = _clientWith((_) async => _json(_farmListJson, 200));
      final repo = BackendFarmRepository(apiClient: client);

      final result = await repo.getFarms();

      expect(result.total, 1);
      expect(result.limit, 50);
      expect(result.offset, 0);
      expect(result.items, hasLength(1));
      expect(result.items.first.id, 'farm-uuid-1');
      client.close();
    });
  });

  // -------------------------------------------------------------------------
  // GET /farms/{farm_id}
  // -------------------------------------------------------------------------

  group('getFarm', () {
    test('calls correct endpoint for a given farmId', () async {
      late Uri captured;
      final client = _clientWith((request) async {
        captured = request.url;
        return _json(_fullFarmJson, 200);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await repo.getFarm('farm-uuid-1');

      expect(captured.path, endsWith('/farms/farm-uuid-1'));
      client.close();
    });

    test('returns FarmResponseDto with correct fields', () async {
      final client = _clientWith((_) async => _json(_fullFarmJson, 200));
      final repo = BackendFarmRepository(apiClient: client);

      final result = await repo.getFarm('farm-uuid-1');

      expect(result.id, 'farm-uuid-1');
      expect(result.name, 'Test Tarla');
      expect(result.currentCrop?.cropType, 'WHEAT');
      client.close();
    });
  });

  // -------------------------------------------------------------------------
  // POST /farms
  // -------------------------------------------------------------------------

  group('createFarm', () {
    test('sends correct snake_case JSON body', () async {
      late Map<String, dynamic> capturedBody;
      final client = _clientWith((request) async {
        capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
        return _json(_createJson, 201);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await repo.createFarm(
        const FarmCreateRequestDto(
          name: 'Yeni Tarla',
          latitude: 38.5,
          longitude: 27.1,
          cropType: 'CORN',
          plantedAt: '2026-04-01',
          sizeInHectares: 8.0,
          irrigationMethod: 'DRIP',
        ),
      );

      expect(capturedBody['name'], 'Yeni Tarla');
      expect(capturedBody['latitude'], 38.5);
      expect(capturedBody['longitude'], 27.1);
      expect(capturedBody['initial_crop_type'], 'CORN');
      expect(capturedBody['initial_planted_at'], '2026-04-01');
      expect(capturedBody['size_in_hectares'], 8.0);
      expect(capturedBody['irrigation_method'], 'DRIP');
      client.close();
    });

    test('accepts the backend created-id response', () async {
      final client = _clientWith((_) async => _json(_createJson, 201));
      final repo = BackendFarmRepository(apiClient: client);

      final result = repo.createFarm(
        const FarmCreateRequestDto(
          name: 'Tarla',
          latitude: 38.0,
          longitude: 27.0,
          cropType: 'WHEAT',
          plantedAt: '2026-01-01',
        ),
      );

      await expectLater(result, completes);
      client.close();
    });
  });

  // -------------------------------------------------------------------------
  // PATCH /farms/{farm_id}
  // -------------------------------------------------------------------------

  group('updateFarm', () {
    test('sends only provided fields in PATCH body', () async {
      late Map<String, dynamic> capturedBody;
      late Uri captured;
      final client = _clientWith((request) async {
        captured = request.url;
        capturedBody = jsonDecode(request.body) as Map<String, dynamic>;
        return _json(_mutationJson, 200);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await repo.updateFarm(
        'farm-uuid-1',
        const FarmUpdateRequestDto(name: 'Güncel Ad', sizeInHectares: 20.0),
      );

      expect(captured.path, endsWith('/farms/farm-uuid-1'));
      expect(capturedBody['name'], 'Güncel Ad');
      expect(capturedBody['size_in_hectares'], 20.0);
      expect(capturedBody.containsKey('latitude'), isFalse);
      expect(capturedBody.containsKey('longitude'), isFalse);
      expect(capturedBody.containsKey('irrigation_method'), isFalse);
      client.close();
    });

    test('returns FarmMutationResponseDto on success', () async {
      final client = _clientWith((_) async => _json(_mutationJson, 200));
      final repo = BackendFarmRepository(apiClient: client);

      final result = await repo.updateFarm(
        'farm-uuid-1',
        const FarmUpdateRequestDto(name: 'Ad'),
      );

      expect(result.farm.name, 'Test Tarla');
      client.close();
    });
  });

  // -------------------------------------------------------------------------
  // DELETE /farms/{farm_id}
  // -------------------------------------------------------------------------

  group('archiveFarm', () {
    test('DELETE 204 is treated as success', () async {
      late String capturedMethod;
      late Uri captured;
      final client = _clientWith((request) async {
        capturedMethod = request.method;
        captured = request.url;
        return http.Response('', 204);
      });
      final repo = BackendFarmRepository(apiClient: client);

      await expectLater(repo.archiveFarm('farm-uuid-1'), completes);
      expect(capturedMethod, 'DELETE');
      expect(captured.path, endsWith('/farms/farm-uuid-1'));
      client.close();
    });
  });

  // -------------------------------------------------------------------------
  // Error propagation
  // -------------------------------------------------------------------------

  group('error propagation', () {
    test('4xx response is surfaced as ApiException', () async {
      final client = _clientWith(
        (_) async => _json({'detail': 'Tarla bulunamadı.'}, 404),
      );
      final repo = BackendFarmRepository(apiClient: client);

      await expectLater(
        repo.getFarm('nonexistent'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 404)
              .having((e) => e.message, 'message', 'Tarla bulunamadı.'),
        ),
      );
      client.close();
    });

    test('5xx response is surfaced as retryable ApiException', () async {
      final client = _clientWith(
        (_) async => http.Response('Internal Server Error', 500),
      );
      final repo = BackendFarmRepository(apiClient: client);

      await expectLater(
        repo.getFarms(),
        throwsA(
          isA<ApiException>().having((e) => e.retryable, 'retryable', isTrue),
        ),
      );
      client.close();
    });
  });
}

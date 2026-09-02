import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/fields/data/backend_farm_repository.dart';
import 'package:mobile/features/fields/data/backend_tarla_repository.dart';
import 'package:mobile/features/fields/data/farm_summary_model.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  group('FarmSummaryResponse parsing', () {
    test('parses full response with farms, nextTask, lastActivity, and upcomingTasks', () {
      final json = {
        'farms': [
          {
            'farm': {
              'id': 'f1',
              'owner_id': 'u1',
              'name': 'Kuzey Tarla',
              'latitude': 38.5,
              'longitude': 35.5,
              'size_in_hectares': 2.5,
              'irrigation_method': 'DRIP',
              'soil_type': null,
              'note': null,
              'archived_at': null,
              'created_at': '2026-09-01T10:00:00Z',
              'updated_at': '2026-09-01T10:00:00Z',
              'current_crop': {
                'id': 'cp1',
                'farm_id': 'f1',
                'crop_type': 'WHEAT',
                'variety': null,
                'planted_at': '2026-03-15T00:00:00Z',
                'harvested_at': null,
                'status': 'ACTIVE',
                'created_at': '2026-03-15T00:00:00Z',
                'updated_at': '2026-03-15T00:00:00Z',
              },
            },
            'next_task': {
              'id': 'task-1',
              'farm_id': 'f1',
              'title': 'Sulama',
              'description': 'Akşam sulaması',
              'status': 'NEW',
              'due_date': '2026-09-05',
              'created_at_utc': '2026-09-01T10:00:00Z',
            },
            'last_activity': {
              'id': 'act-1',
              'farm_id': 'f1',
              'activity_type': 'FERTILIZATION',
              'description': 'Taban gübresi',
              'occurred_at_utc': '2026-09-02T10:00:00Z',
            },
          },
        ],
        'upcoming_tasks': [
          {
            'id': 'task-1',
            'farm_id': 'f1',
            'title': 'Sulama',
            'description': 'Akşam sulaması',
            'status': 'NEW',
            'due_date': '2026-09-05',
            'created_at_utc': '2026-09-01T10:00:00Z',
          },
        ],
      };

      final response = FarmSummaryResponse.fromJson(json);

      expect(response.farms, hasLength(1));
      final farmSummary = response.farms.first;
      expect(farmSummary.tarla.id, 'f1');
      expect(farmSummary.tarla.name, 'Kuzey Tarla');
      expect(farmSummary.tarla.cropType, 'WHEAT');

      expect(farmSummary.nextTask, isNotNull);
      expect(farmSummary.nextTask!.id, 'task-1');
      expect(farmSummary.nextTask!.type, 'Sulama');
      expect(farmSummary.nextTask!.isCompleted, isFalse);

      expect(farmSummary.lastActivity, isNotNull);
      expect(farmSummary.lastActivity!.id, 'act-1');
      expect(farmSummary.lastActivity!.type, 'Gübreleme');
      expect(farmSummary.lastActivity!.isCompleted, isTrue);

      expect(response.upcomingTasks, hasLength(1));
      expect(response.upcomingTasks.first.id, 'task-1');
      expect(response.upcomingTasks.first.type, 'Sulama');
    });

    test('supports null nextTask and null lastActivity safely', () {
      final json = {
        'farms': [
          {
            'farm': {
              'id': 'f2',
              'owner_id': 'u1',
              'name': 'Boş Tarla',
              'latitude': null,
              'longitude': null,
              'size_in_hectares': null,
              'irrigation_method': null,
              'soil_type': null,
              'note': null,
              'archived_at': null,
              'created_at': '2026-09-01T10:00:00Z',
              'updated_at': '2026-09-01T10:00:00Z',
              'current_crop': null,
            },
            'next_task': null,
            'last_activity': null,
          },
        ],
        'upcoming_tasks': [],
      };

      final response = FarmSummaryResponse.fromJson(json);

      expect(response.farms, hasLength(1));
      expect(response.farms.first.tarla.name, 'Boş Tarla');
      expect(response.farms.first.nextTask, isNull);
      expect(response.farms.first.lastActivity, isNull);
      expect(response.upcomingTasks, isEmpty);
    });

    test('supports 10 farms in a single response', () {
      final farmList = List.generate(
        10,
        (i) => {
          'farm': {
            'id': 'f$i',
            'owner_id': 'u1',
            'name': 'Tarla $i',
            'latitude': null,
            'longitude': null,
            'size_in_hectares': null,
            'irrigation_method': null,
            'soil_type': null,
            'note': null,
            'archived_at': null,
            'created_at': '2026-09-01T10:00:00Z',
            'updated_at': '2026-09-01T10:00:00Z',
            'current_crop': null,
          },
          'next_task': null,
          'last_activity': null,
        },
      );

      final response = FarmSummaryResponse.fromJson({
        'farms': farmList,
        'upcoming_tasks': [],
      });

      expect(response.farms, hasLength(10));
    });
  });

  group('BackendTarlaRepository.getFarmSummary single HTTP request guarantee', () {
    test('makes exactly 1 HTTP GET request even when 10 farms are returned', () async {
      int requestCount = 0;
      String? requestedPath;

      final tenFarmsJson = List.generate(
        10,
        (i) => {
          'farm': {
            'id': 'f$i',
            'owner_id': 'u1',
            'name': 'Tarla $i',
            'latitude': null,
            'longitude': null,
            'size_in_hectares': null,
            'irrigation_method': null,
            'soil_type': null,
            'note': null,
            'archived_at': null,
            'created_at': '2026-09-01T10:00:00Z',
            'updated_at': '2026-09-01T10:00:00Z',
            'current_crop': null,
          },
          'next_task': null,
          'last_activity': null,
        },
      );

      final client = ApiClient(
        httpClient: MockClient((request) async {
          requestCount++;
          requestedPath = request.url.toString();
          return http.Response(
            jsonEncode({
              'farms': tenFarmsJson,
              'upcoming_tasks': [],
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final remote = BackendFarmRepository(apiClient: client);
      final repo = BackendTarlaRepository(remote: remote);

      final result = await repo.getFarmSummary(upcomingLimit: 5);

      // Verify 10 farms are returned
      expect(result.farms, hasLength(10));
      // CRITICAL: Request count MUST be 1
      expect(requestCount, 1);
      expect(requestedPath, contains('/api/v1/farms/summary?upcomingLimit=5'));
    });
  });
}

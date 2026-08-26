import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';

void main() {
  // ---------------------------------------------------------------------------
  // FarmResponseDto
  // ---------------------------------------------------------------------------

  group('FarmResponseDto.fromJson', () {
    const fullJson = <String, dynamic>{
      'id': 'farm-uuid-1',
      'owner_id': 'owner-uuid-1',
      'name': 'Kuzey Tarlası',
      'latitude': 39.92,
      'longitude': 32.85,
      'size_in_hectares': 12.5,
      'irrigation_method': 'DRIP',
      'soil_type': 'Killi',
      'note': 'Deneme notu',
      'archived_at': null,
      'created_at': '2026-08-01T10:00:00Z',
      'updated_at': '2026-08-20T15:30:00Z',
      'current_crop': {
        'id': 'crop-uuid-1',
        'farm_id': 'farm-uuid-1',
        'crop_type': 'WHEAT',
        'variety': 'Bezostaya',
        'planted_at': '2026-03-15',
        'harvested_at': null,
        'status': 'ACTIVE',
        'created_at': '2026-03-15T08:00:00Z',
        'updated_at': '2026-03-15T08:00:00Z',
      },
    };

    test('parses full FarmResponse JSON correctly', () {
      final dto = FarmResponseDto.fromJson(fullJson);

      expect(dto.id, 'farm-uuid-1');
      expect(dto.ownerId, 'owner-uuid-1');
      expect(dto.name, 'Kuzey Tarlası');
      expect(dto.latitude, 39.92);
      expect(dto.longitude, 32.85);
      expect(dto.sizeInHectares, 12.5);
      expect(dto.irrigationMethod, 'DRIP');
      expect(dto.soilType, 'Killi');
      expect(dto.note, 'Deneme notu');
      expect(dto.archivedAt, isNull);
      expect(dto.createdAt, DateTime.parse('2026-08-01T10:00:00Z'));
      expect(dto.updatedAt, DateTime.parse('2026-08-20T15:30:00Z'));
    });

    test('parses nested current_crop correctly', () {
      final dto = FarmResponseDto.fromJson(fullJson);
      final crop = dto.currentCrop;

      expect(crop, isNotNull);
      expect(crop!.id, 'crop-uuid-1');
      expect(crop.farmId, 'farm-uuid-1');
      expect(crop.cropType, 'WHEAT');
      expect(crop.variety, 'Bezostaya');
      expect(crop.plantedAt, DateTime.parse('2026-03-15'));
      expect(crop.harvestedAt, isNull);
      expect(crop.status, 'ACTIVE');
    });

    test('nullable/minimal response parses without throwing', () {
      const minimalJson = <String, dynamic>{
        'id': 'farm-2',
        'owner_id': 'owner-2',
        'name': 'Güney Tarlası',
        'latitude': null,
        'longitude': null,
        'size_in_hectares': null,
        'irrigation_method': null,
        'soil_type': null,
        'note': null,
        'archived_at': null,
        'created_at': '2026-08-01T00:00:00Z',
        'updated_at': '2026-08-01T00:00:00Z',
        'current_crop': null,
      };

      final dto = FarmResponseDto.fromJson(minimalJson);

      expect(dto.latitude, isNull);
      expect(dto.longitude, isNull);
      expect(dto.sizeInHectares, isNull);
      expect(dto.irrigationMethod, isNull);
      expect(dto.soilType, isNull);
      expect(dto.note, isNull);
      expect(dto.archivedAt, isNull);
      expect(dto.currentCrop, isNull);
    });

    test('archived_at is parsed when present', () {
      final json = {
        ...fullJson,
        'archived_at': '2026-09-01T12:00:00Z',
        'current_crop': null,
      };

      final dto = FarmResponseDto.fromJson(json);
      expect(dto.archivedAt, DateTime.parse('2026-09-01T12:00:00Z'));
    });

    test('unknown crop_type string is preserved without crashing', () {
      final json = {
        ...fullJson,
        'current_crop': {
          ...fullJson['current_crop'] as Map<String, dynamic>,
          'crop_type': 'FUTURE_CROP_UNKNOWN',
        },
      };

      final dto = FarmResponseDto.fromJson(json);
      expect(dto.currentCrop!.cropType, 'FUTURE_CROP_UNKNOWN');
    });
  });

  // ---------------------------------------------------------------------------
  // FarmListResponseDto
  // ---------------------------------------------------------------------------

  group('FarmListResponseDto.fromJson', () {
    test('pagination fields are parsed correctly', () {
      const json = <String, dynamic>{
        'items': [],
        'total': 42,
        'limit': 20,
        'offset': 40,
      };

      final dto = FarmListResponseDto.fromJson(json);

      expect(dto.total, 42);
      expect(dto.limit, 20);
      expect(dto.offset, 40);
      expect(dto.items, isEmpty);
    });

    test('items list is mapped to FarmResponseDto instances', () {
      final json = <String, dynamic>{
        'items': [
          {
            'id': 'f1',
            'owner_id': 'o1',
            'name': 'Tarla 1',
            'latitude': 38.0,
            'longitude': 27.0,
            'size_in_hectares': 5.0,
            'irrigation_method': null,
            'soil_type': null,
            'note': null,
            'archived_at': null,
            'created_at': '2026-01-01T00:00:00Z',
            'updated_at': '2026-01-01T00:00:00Z',
            'current_crop': null,
          },
        ],
        'total': 1,
        'limit': 50,
        'offset': 0,
      };

      final dto = FarmListResponseDto.fromJson(json);

      expect(dto.items, hasLength(1));
      expect(dto.items.first.id, 'f1');
      expect(dto.items.first.name, 'Tarla 1');
    });
  });

  // ---------------------------------------------------------------------------
  // FarmMutationResponseDto
  // ---------------------------------------------------------------------------

  group('FarmMutationResponseDto.fromJson', () {
    final farmJson = <String, dynamic>{
      'id': 'farm-3',
      'owner_id': 'owner-3',
      'name': 'Doğu Tarlası',
      'latitude': 40.0,
      'longitude': 29.0,
      'size_in_hectares': 8.0,
      'irrigation_method': 'SPRINKLER',
      'soil_type': null,
      'note': null,
      'archived_at': null,
      'created_at': '2026-08-01T00:00:00Z',
      'updated_at': '2026-08-01T00:00:00Z',
      'current_crop': null,
    };

    test('parses farm and warnings from mutation response', () {
      final json = <String, dynamic>{
        'farm': farmJson,
        'warnings': ['Aynı ada sahip aktif bir tarlanız zaten var.'],
      };

      final dto = FarmMutationResponseDto.fromJson(json);

      expect(dto.farm.id, 'farm-3');
      expect(dto.warnings, hasLength(1));
      expect(dto.warnings.first, contains('Aynı ada'));
    });

    test('warnings defaults to empty list when absent', () {
      final json = <String, dynamic>{'farm': farmJson};

      final dto = FarmMutationResponseDto.fromJson(json);
      expect(dto.warnings, isEmpty);
    });
  });

  // ---------------------------------------------------------------------------
  // FarmCreateRequestDto.toJson
  // ---------------------------------------------------------------------------

  group('FarmCreateRequestDto.toJson', () {
    test('produces snake_case JSON with all required fields', () {
      const dto = FarmCreateRequestDto(
        name: 'Batı Tarlası',
        latitude: 37.5,
        longitude: 27.3,
        cropType: 'CORN',
        plantedAt: '2026-04-01',
        sizeInHectares: 10.0,
      );

      final json = dto.toJson();

      expect(json['name'], 'Batı Tarlası');
      expect(json['latitude'], 37.5);
      expect(json['longitude'], 27.3);
      expect(json['crop_type'], 'CORN');
      expect(json['planted_at'], '2026-04-01');
      expect(json['size_in_hectares'], 10.0);
    });

    test('omits null optional fields from JSON', () {
      const dto = FarmCreateRequestDto(
        name: 'Test Tarla',
        latitude: 37.0,
        longitude: 27.0,
        cropType: 'BARLEY',
        plantedAt: '2026-03-01',
      );

      final json = dto.toJson();

      expect(json.containsKey('size_in_hectares'), isFalse);
      expect(json.containsKey('irrigation_method'), isFalse);
      expect(json.containsKey('soil_type'), isFalse);
      expect(json.containsKey('note'), isFalse);
      expect(json.containsKey('variety'), isFalse);
    });
  });

  // ---------------------------------------------------------------------------
  // FarmUpdateRequestDto.toJson
  // ---------------------------------------------------------------------------

  group('FarmUpdateRequestDto.toJson', () {
    test('only sends explicitly provided fields', () {
      const dto = FarmUpdateRequestDto(name: 'Yeni İsim');

      final json = dto.toJson();

      expect(json['name'], 'Yeni İsim');
      expect(json.containsKey('latitude'), isFalse);
      expect(json.containsKey('longitude'), isFalse);
      expect(json.containsKey('size_in_hectares'), isFalse);
      expect(json.containsKey('irrigation_method'), isFalse);
      expect(json.containsKey('soil_type'), isFalse);
      expect(json.containsKey('note'), isFalse);
    });

    test('serialises multiple provided fields with snake_case keys', () {
      const dto = FarmUpdateRequestDto(
        name: 'Güncel Ad',
        latitude: 39.5,
        longitude: 32.2,
        sizeInHectares: 15.0,
      );

      final json = dto.toJson();

      expect(json['name'], 'Güncel Ad');
      expect(json['latitude'], 39.5);
      expect(json['longitude'], 32.2);
      expect(json['size_in_hectares'], 15.0);
      expect(json.containsKey('irrigation_method'), isFalse);
    });

    test('empty update produces empty JSON object', () {
      const dto = FarmUpdateRequestDto();
      expect(dto.toJson(), isEmpty);
    });
  });
}

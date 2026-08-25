import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/dto/crop_period_dto.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/mappers/farm_mapper.dart';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

CropPeriodResponseDto _crop({
  String cropType = 'WHEAT',
  String plantedAt = '2026-03-15',
}) => CropPeriodResponseDto(
  id: 'crop-1',
  farmId: 'farm-1',
  cropType: cropType,
  variety: null,
  plantedAt: DateTime.parse(plantedAt),
  harvestedAt: null,
  status: 'ACTIVE',
  createdAt: DateTime.parse('2026-03-15T00:00:00Z'),
  updatedAt: DateTime.parse('2026-03-15T00:00:00Z'),
);

/// [hasCrop] — when false, currentCrop is null so cropType / plantedAt are absent.
FarmResponseDto _dto({
  double? latitude = 39.92,
  double? longitude = 32.85,
  double? sizeInHectares = 12.5,
  bool hasCrop = true,
}) => FarmResponseDto(
  id: 'farm-1',
  ownerId: 'owner-1',
  name: 'Test Tarla',
  latitude: latitude,
  longitude: longitude,
  sizeInHectares: sizeInHectares,
  irrigationMethod: 'DRIP',
  soilType: null,
  note: null,
  archivedAt: null,
  createdAt: DateTime.parse('2026-08-01T00:00:00Z'),
  updatedAt: DateTime.parse('2026-08-01T00:00:00Z'),
  currentCrop: hasCrop ? _crop() : null,
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('FarmMapper.fromDto', () {
    test('maps a complete DTO to Tarla correctly', () {
      final dto = _dto();
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla.id, 'farm-1');
      expect(tarla.name, 'Test Tarla');
      expect(tarla.latitude, 39.92);
      expect(tarla.longitude, 32.85);
      expect(tarla.size, 12.5);
      expect(tarla.cropType, 'WHEAT');
      expect(tarla.plantingDate, DateTime.parse('2026-03-15'));
    });

    test(
      'API DTO and SQLite model are clearly separated — Tarla.toJson uses camelCase keys',
      () {
        final dto = _dto();
        final tarla = FarmMapper.fromDto(dto);
        final tarlaJson = tarla.toJson();

        expect(tarlaJson.containsKey('cropType'), isTrue);
        expect(tarlaJson.containsKey('crop_type'), isFalse);
        expect(tarlaJson.containsKey('plantingDate'), isTrue);
        expect(tarlaJson.containsKey('planted_at'), isFalse);
      },
    );

    test('nullable latitude maps to null Tarla.latitude — no fake 0.0', () {
      final dto = _dto(latitude: null);
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla.latitude, isNull);
    });

    test('nullable longitude maps to null Tarla.longitude — no fake 0.0', () {
      final dto = _dto(longitude: null);
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla.longitude, isNull);
    });

    test('nullable sizeInHectares maps to null Tarla.size — no fake 0.0', () {
      final dto = _dto(sizeInHectares: null);
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla.size, isNull);
    });

    test('null currentCrop maps to null cropType and plantingDate', () {
      final dto = _dto(hasCrop: false);
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla.cropType, isNull);
      expect(tarla.plantingDate, isNull);
    });

    test(
      'fully null optional fields produce a valid Tarla with no fake values',
      () {
        final dto = _dto(
          latitude: null,
          longitude: null,
          sizeInHectares: null,
          hasCrop: false,
        );
        final tarla = FarmMapper.fromDto(dto);

        expect(tarla.id, 'farm-1');
        expect(tarla.name, 'Test Tarla');
        expect(tarla.latitude, isNull);
        expect(tarla.longitude, isNull);
        expect(tarla.size, isNull);
        expect(tarla.cropType, isNull);
        expect(tarla.plantingDate, isNull);
      },
    );
  });

  group('FarmMapper.fromDtoList', () {
    test(
      'converts all DTOs — records with nullable fields are not dropped',
      () {
        final withCoords = _dto();
        final withoutCoords = _dto(latitude: null, longitude: null);

        final result = FarmMapper.fromDtoList([withCoords, withoutCoords]);

        expect(result, hasLength(2));
        expect(result[0].latitude, 39.92);
        expect(result[1].latitude, isNull);
      },
    );

    test('returns empty list for empty input', () {
      expect(FarmMapper.fromDtoList([]), isEmpty);
    });

    test('list with all nullable optional fields converts without loss', () {
      final allNull = _dto(
        latitude: null,
        longitude: null,
        sizeInHectares: null,
        hasCrop: false,
      );

      final result = FarmMapper.fromDtoList([allNull]);

      expect(result, hasLength(1));
      expect(result.first.size, isNull);
    });
  });
}

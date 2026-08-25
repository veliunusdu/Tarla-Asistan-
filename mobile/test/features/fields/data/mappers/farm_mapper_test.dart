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

/// [hasCrop] — when false, [currentCrop] is set to null so the mapper
/// cannot obtain cropType / plantedAt without fabricating values.
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
      final dto = _dto(); // hasCrop defaults to true
      final tarla = FarmMapper.fromDto(dto);

      expect(tarla, isNotNull);
      expect(tarla!.id, 'farm-1');
      expect(tarla.name, 'Test Tarla');
      expect(tarla.latitude, 39.92);
      expect(tarla.longitude, 32.85);
      expect(tarla.size, 12.5);
      expect(tarla.cropType, 'WHEAT');
      expect(tarla.plantingDate, DateTime.parse('2026-03-15'));
    });

    test(
      'API DTO and SQLite model are clearly separated — DTO fields are not used directly as Tarla.toJson keys',
      () {
        final dto = _dto();
        final tarla = FarmMapper.fromDto(dto)!;
        final tarlaJson = tarla.toJson();

        // Tarla model uses camelCase keys (e.g. 'cropType', 'plantingDate')
        // DTO uses snake_case (e.g. 'crop_type', 'planted_at').
        // Confirming the mapper translates between the two without mixing them.
        expect(tarlaJson.containsKey('cropType'), isTrue);
        expect(tarlaJson.containsKey('crop_type'), isFalse);
        expect(tarlaJson.containsKey('plantingDate'), isTrue);
        expect(tarlaJson.containsKey('planted_at'), isFalse);
      },
    );

    test('returns null when latitude is null — no fake 0.0 generated', () {
      final dto = _dto(latitude: null);
      expect(FarmMapper.fromDto(dto), isNull);
    });

    test('returns null when longitude is null — no fake 0.0 generated', () {
      final dto = _dto(longitude: null);
      expect(FarmMapper.fromDto(dto), isNull);
    });

    test(
      'returns null when sizeInHectares is null — no fake 0.0 generated',
      () {
        final dto = _dto(sizeInHectares: null);
        expect(FarmMapper.fromDto(dto), isNull);
      },
    );

    test(
      'returns null when currentCrop is null — no fake cropType generated',
      () {
        final dto = _dto(hasCrop: false);
        expect(FarmMapper.fromDto(dto), isNull);
      },
    );

    test('returns null when all nullable required fields are null', () {
      final dto = _dto(
        latitude: null,
        longitude: null,
        sizeInHectares: null,
        hasCrop: false,
      );
      expect(FarmMapper.fromDto(dto), isNull);
    });
  });

  group('FarmMapper.fromDtoList', () {
    test('filters out DTOs that cannot be mapped, keeps valid ones', () {
      final valid = _dto();
      final invalid = _dto(latitude: null); // returns null from fromDto

      final result = FarmMapper.fromDtoList([valid, invalid]);

      expect(result, hasLength(1));
      expect(result.first.id, 'farm-1');
    });

    test('returns empty list when all DTOs are unmappable', () {
      final result = FarmMapper.fromDtoList([_dto(latitude: null)]);
      expect(result, isEmpty);
    });
  });
}

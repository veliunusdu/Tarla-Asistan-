import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/backend_tarla_repository.dart';
import 'package:mobile/features/fields/data/dto/crop_period_dto.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/farm_remote_repository.dart';
import 'package:mobile/models/tarla.dart';

void main() {
  test(
    'creates a backend farm with converted area and nullable coordinates',
    () async {
      final remote = _FakeFarmRemoteRepository();
      final repository = BackendTarlaRepository(remote: remote);

      await repository.addTarla(
        Tarla(
          id: 'local-id',
          name: 'Kuzey Tarla',
          latitude: null,
          longitude: null,
          size: 10,
          cropType: 'Buğday',
          plantingDate: DateTime(2026, 3, 15),
        ),
      );

      expect(remote.created?.latitude, isNull);
      expect(remote.created?.longitude, isNull);
      expect(remote.created?.sizeInHectares, 1);
      expect(remote.created?.cropType, 'WHEAT');
      expect(remote.created?.plantedAt, '2026-03-15');
    },
  );

  test('maps backend hectares to dönüm for field screens', () async {
    final remote = _FakeFarmRemoteRepository();
    final repository = BackendTarlaRepository(remote: remote);

    final fields = await repository.getTarlalar();

    expect(fields, hasLength(1));
    expect(fields.single.size, 25);
    expect(fields.single.latitude, 38.4237);
  });
}

class _FakeFarmRemoteRepository implements FarmRemoteRepository {
  FarmCreateRequestDto? created;

  final _farm = FarmResponseDto(
    id: 'farm-1',
    ownerId: 'owner-1',
    name: 'Backend Tarla',
    latitude: 38.4237,
    longitude: 27.1428,
    sizeInHectares: 2.5,
    irrigationMethod: null,
    soilType: null,
    note: null,
    archivedAt: null,
    createdAt: DateTime(2026),
    updatedAt: DateTime(2026),
    currentCrop: CropPeriodResponseDto(
      id: 'crop-1',
      farmId: 'farm-1',
      cropType: 'WHEAT',
      variety: null,
      plantedAt: DateTime(2026, 3, 15),
      harvestedAt: null,
      status: 'ACTIVE',
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026),
    ),
  );

  @override
  Future<void> createFarm(FarmCreateRequestDto request) async {
    created = request;
  }

  @override
  Future<FarmListResponseDto> getFarms({
    bool includeArchived = false,
    int limit = 50,
    int offset = 0,
  }) async => FarmListResponseDto(
    items: [_farm],
    total: 1,
    limit: limit,
    offset: offset,
  );

  @override
  Future<FarmResponseDto> getFarm(String farmId) async => _farm;

  @override
  Future<FarmMutationResponseDto> updateFarm(
    String farmId,
    FarmUpdateRequestDto request,
  ) async => FarmMutationResponseDto(farm: _farm, warnings: const []);

  @override
  Future<void> archiveFarm(String farmId) async {}
}

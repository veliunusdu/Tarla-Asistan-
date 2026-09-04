import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/backend_tarla_repository.dart';
import 'package:mobile/features/fields/data/dto/crop_period_dto.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/farm_remote_repository.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
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
      expect(remote.created?.cropName, 'Buğday');
      expect(remote.created?.cropType, 'WHEAT');
      expect(remote.created?.plantedAt, '2026-03-15');
    },
  );

  test('serializes create fields using the .NET farm contract', () {
    const request = FarmCreateRequestDto(
      name: 'Kuzey Tarla',
      latitude: 38.42,
      longitude: 27.14,
      cropName: 'Buğday',
      cropType: 'WHEAT',
      plantedAt: '2026-03-15',
    );

    expect(request.toJson()['initial_crop_name'], 'Buğday');
    expect(request.toJson()['initial_crop_type'], 'WHEAT');
    expect(request.toJson()['initial_planted_at'], '2026-03-15');
    expect(request.toJson().containsKey('crop_type'), isFalse);
    expect(request.toJson().containsKey('planted_at'), isFalse);
  });

  test('maps backend hectares to dönüm for field screens', () async {
    final remote = _FakeFarmRemoteRepository();
    final repository = BackendTarlaRepository(remote: remote);

    final fields = await repository.getTarlalar();

    expect(fields, hasLength(1));
    expect(fields.single.size, 25);
    expect(fields.single.latitude, 38.4237);
  });

  test('updates farm location with latitude and longitude', () async {
    final remote = _FakeFarmRemoteRepository();
    final repository = BackendTarlaRepository(remote: remote);

    await repository.updateTarlaLocation(
      'farm-1',
      const TarlaLocation(latitude: 38.5, longitude: 27.2),
    );

    expect(remote.updatedFarmId, 'farm-1');
    expect(remote.updatedRequest?.latitude, 38.5);
    expect(remote.updatedRequest?.longitude, 27.2);
  });

  test('archives a farm through the backend', () async {
    final remote = _FakeFarmRemoteRepository();

    await BackendTarlaRepository(remote: remote).archiveTarla('farm-1');

    expect(remote.archivedFarmId, 'farm-1');
  });

  test('updates supported farm details through the backend', () async {
    final remote = _FakeFarmRemoteRepository();
    final repository = BackendTarlaRepository(remote: remote);

    await repository.updateTarla(
      Tarla(
        id: 'farm-1',
        name: 'Güney Tarla',
        latitude: 38.42,
        longitude: 27.14,
        size: 18.5,
        cropType: 'Buğday',
        plantingDate: DateTime(2026, 3, 15),
      ),
    );

    expect(remote.updatedFarmId, 'farm-1');
    expect(remote.updatedRequest?.name, 'Güney Tarla');
    expect(remote.updatedRequest?.sizeInHectares, 1.85);
    expect(remote.updatedRequest?.latitude, 38.42);
    expect(remote.updatedRequest?.longitude, 27.14);
  });
}

class _FakeFarmRemoteRepository implements FarmRemoteRepository {
  FarmCreateRequestDto? created;
  String? updatedFarmId;
  FarmUpdateRequestDto? updatedRequest;
  String? archivedFarmId;

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
  ) async {
    updatedFarmId = farmId;
    updatedRequest = request;
    return FarmMutationResponseDto(farm: _farm, warnings: const []);
  }

  @override
  Future<void> archiveFarm(String farmId) async => archivedFarmId = farmId;

  @override
  Future<Map<String, dynamic>> getFarmSummary({int upcomingLimit = 5}) async =>
      {'farms': [], 'upcoming_tasks': []};
}

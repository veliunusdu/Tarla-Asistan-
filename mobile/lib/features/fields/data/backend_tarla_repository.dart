import '../../location/domain/tarla_location.dart';
import '../../../models/tarla.dart';
import 'dto/farm_dto.dart';
import 'farm_remote_repository.dart';
import 'farm_summary_model.dart';
import 'farm_summary_repository.dart';
import 'mappers/farm_mapper.dart';
import 'tarla_location_repository.dart';
import 'tarla_repository.dart';

class BackendTarlaRepository
    implements
        TarlaRepository,
        TarlaLocationRepository,
        TarlaArchiveRepository,
        TarlaUpdateRepository,
        FarmSummaryRepository {
  const BackendTarlaRepository({required FarmRemoteRepository remote})
    : _remote = remote;

  final FarmRemoteRepository _remote;

  @override
  Future<void> addTarla(Tarla tarla) async {
    final cropType = _cropType(tarla.cropType);
    final plantedAt = tarla.plantingDate;
    if (cropType == null || plantedAt == null) {
      throw ArgumentError('Tarla ürünü ve ekim tarihi gereklidir.');
    }
    await _remote.createFarm(
      FarmCreateRequestDto(
        name: tarla.name,
        latitude: tarla.latitude,
        longitude: tarla.longitude,
        sizeInHectares: tarla.size == null ? null : tarla.size! / 10,
        cropType: cropType,
        plantedAt: _date(plantedAt),
      ),
    );
  }

  @override
  Future<void> archiveTarla(String id) => _remote.archiveFarm(id);

  @override
  Future<void> updateTarla(Tarla tarla) {
    return _remote.updateFarm(
      tarla.id,
      FarmUpdateRequestDto(
        name: tarla.name,
        latitude: tarla.latitude,
        longitude: tarla.longitude,
        sizeInHectares: tarla.size == null ? null : tarla.size! / 10,
      ),
    );
  }

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) async {
    await _remote.updateFarm(
      id,
      FarmUpdateRequestDto(
        latitude: location.latitude,
        longitude: location.longitude,
      ),
    );
  }

  @override
  Future<List<Tarla>> getTarlalar() async {
    final response = await _remote.getFarms();
    return response.items.map(fromDto).toList();
  }

  @override
  Future<FarmSummaryResponse> getFarmSummary({int upcomingLimit = 5}) async {
    final json = await _remote.getFarmSummary(upcomingLimit: upcomingLimit);
    return FarmSummaryResponse.fromJson(json);
  }

  static Tarla fromDto(FarmResponseDto dto) {
    final tarla = FarmMapper.fromDto(dto);
    return Tarla(
      id: tarla.id,
      name: tarla.name,
      latitude: tarla.latitude,
      longitude: tarla.longitude,
      size: tarla.size == null ? null : tarla.size! * 10,
      cropType: tarla.cropType,
      plantingDate: tarla.plantingDate,
    );
  }

  static String _date(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';

  static String? _cropType(String? value) => switch (value) {
    'Buğday' || 'WHEAT' => 'WHEAT',
    'Arpa' || 'BARLEY' => 'BARLEY',
    'Mısır' || 'CORN' => 'CORN',
    'Ayçiçeği' || 'SUNFLOWER' => 'SUNFLOWER',
    'Domates' || 'TOMATO' => 'TOMATO',
    _ => null,
  };
}

import '../../location/domain/tarla_location.dart';
import '../../../models/tarla.dart';
import '../../../services/user_error_message.dart';
import 'dto/farm_dto.dart';
import 'farm_remote_repository.dart';
import 'farm_summary_model.dart';
import 'farm_summary_repository.dart';
import 'farm_summary_cache.dart';
import 'mappers/farm_mapper.dart';
import 'tarla_repository.dart';

class BackendTarlaRepository
    implements
        TarlaRepository,
        TarlaLocationRepository,
        TarlaArchiveRepository,
        TarlaUpdateRepository,
        FarmSummaryRepository {
  const BackendTarlaRepository({required this.remote, this.cache});

  final FarmRemoteRepository remote;
  final FarmSummaryCache? cache;

  @override
  Future<void> addTarla(Tarla tarla) async {
    final cropName = tarla.cropType?.trim();
    final plantedAt = tarla.plantingDate;
    if (cropName == null || cropName.isEmpty || plantedAt == null) {
      throw ArgumentError('Tarla ürünü ve ekim tarihi gereklidir.');
    }
    final cropType = _cropType(cropName);
    await remote.createFarm(
      FarmCreateRequestDto(
        name: tarla.name,
        latitude: tarla.latitude,
        longitude: tarla.longitude,
        sizeInHectares: tarla.size == null ? null : tarla.size! / 10,
        cropName: cropName,
        cropType: cropType,
        plantedAt: _date(plantedAt),
      ),
    );
  }

  @override
  Future<void> archiveTarla(String id) => remote.archiveFarm(id);

  @override
  Future<void> updateTarla(Tarla tarla) {
    return remote.updateFarm(
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
    await remote.updateFarm(
      id,
      FarmUpdateRequestDto(
        latitude: location.latitude,
        longitude: location.longitude,
      ),
    );
  }

  @override
  Future<List<Tarla>> getTarlalar() async {
    try {
      final response = await remote.getFarms();
      return response.items.map(fromDto).toList();
    } catch (error) {
      if (!isTemporaryConnectionFailure(error)) rethrow;
      final cached = await _readCacheSafely();
      if (cached == null) rethrow;
      return FarmSummaryResponse.fromJson(
        cached.payload,
      ).farms.map((item) => item.tarla).toList();
    }
  }

  @override
  Future<FarmSummaryResponse> getFarmSummary({int upcomingLimit = 5}) async {
    try {
      final json = await remote.getFarmSummary(upcomingLimit: upcomingLimit);
      try {
        await cache?.write(json);
      } catch (_) {
        // Cache persistence must never turn a valid API response into an error.
      }
      return FarmSummaryResponse.fromJson(json);
    } catch (error) {
      if (!isTemporaryConnectionFailure(error)) rethrow;
      final cached = await _readCacheSafely();
      if (cached == null) rethrow;
      return FarmSummaryResponse.fromJson(
        cached.payload,
        isFromCache: true,
        cachedAt: cached.cachedAt,
      );
    }
  }

  Future<FarmSummaryCacheSnapshot?> _readCacheSafely() async {
    try {
      return await cache?.read();
    } catch (_) {
      return null;
    }
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
      currentCropPeriodId: tarla.currentCropPeriodId,
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

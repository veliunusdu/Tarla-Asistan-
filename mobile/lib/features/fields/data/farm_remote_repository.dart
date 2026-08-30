import 'dto/farm_dto.dart';

/// Abstract interface for the remote farm data source.
///
/// All methods return DTO types.  Mapping to the [Tarla] SQLite model is the
/// responsibility of the calling layer (see [FarmMapper]).
///
/// [ApiException] from the canonical API client propagates unchanged so the
/// caller retains full error context.
abstract interface class FarmRemoteRepository {
  /// GET /api/v1/farms
  ///
  /// [includeArchived] — when true the response includes archived farms.
  /// [limit] — maximum number of items (1–100, default 50).
  /// [offset] — zero-based pagination offset (default 0).
  Future<FarmListResponseDto> getFarms({
    bool includeArchived = false,
    int limit = 50,
    int offset = 0,
  });

  /// GET /api/v1/farms/{farm_id}
  Future<FarmResponseDto> getFarm(String farmId);

  /// POST /api/v1/farms — returns HTTP 201 with the created farm identifier.
  /// The field form does not need that identifier until it reloads the list.
  Future<void> createFarm(FarmCreateRequestDto request);

  /// PATCH /api/v1/farms/{farm_id}
  ///
  /// Only the non-null fields in [request] are sent to the server.
  Future<FarmMutationResponseDto> updateFarm(
    String farmId,
    FarmUpdateRequestDto request,
  );

  /// DELETE /api/v1/farms/{farm_id} — server responds with HTTP 204.
  Future<void> archiveFarm(String farmId);
}

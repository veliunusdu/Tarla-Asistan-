import '../../../services/api_client.dart';
import 'dto/farm_dto.dart';
import 'farm_remote_repository.dart';

/// [FarmRemoteRepository] implementation backed by the production REST API.
///
/// Uses the canonical [ApiClient] from `services/api_client.dart` which:
///   - attaches a Firebase ID token on every request,
///   - retries once with a force-refreshed token on HTTP 401, and
///   - translates network and HTTP errors into [ApiException].
///
/// Endpoint base: `/api/v1/farms` (resolved against [AppConfig.apiBaseUrl]).
/// Token and user data are never logged here.
class BackendFarmRepository implements FarmRemoteRepository {
  const BackendFarmRepository({required ApiClient apiClient})
    : _client = apiClient;

  final ApiClient _client;

  static const _base = '/farms';

  @override
  Future<FarmListResponseDto> getFarms({
    bool includeArchived = false,
    int limit = 50,
    int offset = 0,
  }) async {
    final endpoint =
        '$_base?include_archived=$includeArchived&limit=$limit&offset=$offset';
    final json = await _client.getJson(endpoint);
    return FarmListResponseDto.fromJson(json);
  }

  @override
  Future<FarmResponseDto> getFarm(String farmId) async {
    final json = await _client.getJson('$_base/$farmId');
    return FarmResponseDto.fromJson(json);
  }

  @override
  Future<FarmMutationResponseDto> createFarm(
    FarmCreateRequestDto request,
  ) async {
    final json = await _client.postJson(_base, request.toJson());
    return FarmMutationResponseDto.fromJson(json);
  }

  @override
  Future<FarmMutationResponseDto> updateFarm(
    String farmId,
    FarmUpdateRequestDto request,
  ) async {
    final json = await _client.patchJson('$_base/$farmId', request.toJson());
    return FarmMutationResponseDto.fromJson(json);
  }

  @override
  Future<void> archiveFarm(String farmId) async {
    await _client.delete('$_base/$farmId');
  }
}

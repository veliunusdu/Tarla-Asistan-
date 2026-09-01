import 'package:uuid/uuid.dart';

import '../../../models/faaliyet.dart';
import '../../../services/api_client.dart';
import '../../fields/data/tarla_repository.dart';
import 'faaliyet_repository.dart';

class BackendFaaliyetRepository implements FaaliyetRepository {
  const BackendFaaliyetRepository({
    required ApiClient apiClient,
    required TarlaRepository tarlaRepository,
    Uuid uuid = const Uuid(),
  })  : _api = apiClient,
        _tarlaRepo = tarlaRepository,
        _uuid = uuid;

  final ApiClient _api;
  final TarlaRepository _tarlaRepo;
  final Uuid _uuid;

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {
    final activityType = _mapActivityTypeToBackend(faaliyet.type);
    final description = _buildDescription(faaliyet);
    final occurredAt = _formatOccurredAt(faaliyet);
    final clientOperationId = _resolveClientOperationId(faaliyet.id);

    final payload = <String, dynamic>{
      'activity_type': activityType,
      'description': description,
      'occurred_at': occurredAt,
      'input_method': 'Manual',
      'client_operation_id': clientOperationId,
    };

    await _api.postJson('/farms/${faaliyet.tarlaId}/activities', payload);
  }

  @override
  Future<List<Faaliyet>> getFaaliyetler(
    String tarlaId, {
    int pageSize = 50,
  }) async {
    final clampedLimit = pageSize.clamp(1, 100);
    final allActivities = <Faaliyet>[];
    int offset = 0;
    int total = 0;

    do {
      final response = await _api.getJson(
        '/farms/$tarlaId/activities?limit=$clampedLimit&offset=$offset',
      );
      final items = response['items'] as List<dynamic>? ?? [];
      total = (response['total'] as num?)?.toInt() ?? 0;

      if (items.isEmpty) {
        break;
      }

      for (final item in items) {
        allActivities.add(
          _fromBackendJson(item as Map<String, dynamic>, fallbackTarlaId: tarlaId),
        );
      }

      offset += items.length;

      if (items.length < clampedLimit) {
        break;
      }
    } while (allActivities.length < total);

    allActivities.sort((a, b) => b.timestamp.compareTo(a.timestamp));
    return allActivities;
  }

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async {
    final tarlalar = await _tarlaRepo.getTarlalar();
    if (tarlalar.isEmpty) {
      return [];
    }

    final activitiesPerFarm = await Future.wait(
      tarlalar.map((tarla) => getFaaliyetler(tarla.id)),
    );

    final allActivities = activitiesPerFarm.expand((list) => list).toList();
    allActivities.sort((a, b) => b.timestamp.compareTo(a.timestamp));
    return allActivities;
  }

  static String _mapActivityTypeToBackend(String mobileType) {
    switch (mobileType.trim()) {
      case 'Sulama':
        return 'IRRIGATION';
      case 'Gübreleme':
        return 'FERTILIZATION';
      case 'İlaçlama':
        return 'SPRAYING';
      case 'Budama':
        return 'PRUNING';
      case 'Hasat':
        return 'HARVEST';
      case 'Tarla Kontrolü':
        return 'FIELD_CHECK';
      default:
        return 'OTHER';
    }
  }

  static String _mapActivityTypeToMobile(String backendType) {
    switch (backendType.toUpperCase()) {
      case 'IRRIGATION':
        return 'Sulama';
      case 'FERTILIZATION':
        return 'Gübreleme';
      case 'SPRAYING':
        return 'İlaçlama';
      case 'PRUNING':
        return 'Budama';
      case 'HARVEST':
        return 'Hasat';
      case 'FIELD_CHECK':
        return 'Tarla Kontrolü';
      default:
        return 'Diğer';
    }
  }

  static String _buildDescription(Faaliyet faaliyet) {
    return faaliyet.note.trim();
  }

  static String _formatOccurredAt(Faaliyet faaliyet) {
    return faaliyet.timestamp.toUtc().toIso8601String();
  }

  String _resolveClientOperationId(String candidateId) {
    final trimmed = candidateId.trim();
    if (Uuid.isValidUUID(fromString: trimmed)) {
      return trimmed;
    }
    // Deterministic UUID v5 derived from candidate ID if it is a local string/epoch, or v4
    if (trimmed.isNotEmpty) {
      return _uuid.v5(Namespace.url.value, trimmed);
    }
    return _uuid.v4();
  }

  static Faaliyet _fromBackendJson(
    Map<String, dynamic> json, {
    required String fallbackTarlaId,
  }) {
    final id = json['id']?.toString() ?? '';
    final farmId = json['farm_id']?.toString() ?? fallbackTarlaId;
    final backendType = json['activity_type']?.toString() ?? 'OTHER';
    final mobileType = _mapActivityTypeToMobile(backendType);
    final description = json['description']?.toString() ?? '';
    final occurredAtUtcStr = json['occurred_at_utc']?.toString();
    final timestamp = occurredAtUtcStr != null
        ? (DateTime.tryParse(occurredAtUtcStr)?.toLocal() ?? DateTime.now())
        : DateTime.now();

    return Faaliyet(
      id: id,
      tarlaId: farmId,
      type: mobileType,
      note: description,
      timestamp: timestamp,
      dueDate: null,
      isCompleted: true,
    );
  }
}

import 'dart:async';

import '../../../models/faaliyet.dart';
import '../../../models/tarla.dart';
import '../../../services/api_client.dart';
import '../../fields/data/tarla_repository.dart';
import 'faaliyet_repository.dart';

class BackendFaaliyetRepository implements FaaliyetRepository {
  const BackendFaaliyetRepository({
    required ApiClient apiClient,
    required TarlaRepository tarlaRepository,
  })  : _api = apiClient,
        _tarlaRepo = tarlaRepository;

  final ApiClient _api;
  final TarlaRepository _tarlaRepo;

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {
    final activityType = _mapActivityTypeToBackend(faaliyet.type);
    final description = _buildDescription(faaliyet);
    final occurredAt = _formatOccurredAt(faaliyet);

    final payload = <String, dynamic>{
      'activity_type': activityType,
      'description': description,
      'occurred_at': occurredAt,
      'input_method': 'Manual',
    };

    await _api.postJson('/farms/${faaliyet.tarlaId}/activities', payload);
  }

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async {
    final response = await _api.getJson('/farms/$tarlaId/activities');
    final items = response['items'] as List<dynamic>? ?? [];

    final list = items.map((item) {
      final json = item as Map<String, dynamic>;
      return _fromBackendJson(json, fallbackTarlaId: tarlaId);
    }).toList();

    list.sort((a, b) => b.timestamp.compareTo(a.timestamp));
    return list;
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
    final note = faaliyet.note.trim();
    if (note.isNotEmpty) {
      return note;
    }
    final type = faaliyet.type.trim();
    if (type.length >= 2) {
      return type;
    }
    return ' faaliyeti yapıldı';
  }

  static String _formatOccurredAt(Faaliyet faaliyet) {
    final date = faaliyet.timestamp;
    final now = DateTime.now();
    // Backend validator: date <= DateTime.UtcNow.AddMinutes(5)
    final resolvedDate = date.isAfter(now.add(const Duration(minutes: 4)))
        ? now
        : date;
    return resolvedDate.toUtc().toIso8601String();
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

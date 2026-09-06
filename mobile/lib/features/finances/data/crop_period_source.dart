import '../../../services/api_client.dart';

class FinanceCropPeriod {
  const FinanceCropPeriod({
    required this.id,
    required this.cropName,
    required this.plantedAt,
    required this.status,
  });
  final String id, cropName, status;
  final DateTime plantedAt;
  String get label => '$cropName • ${plantedAt.year}';
}

abstract interface class FinanceCropPeriodSource {
  Future<List<FinanceCropPeriod>> listCropPeriods(String farmId);
}

class BackendFinanceCropPeriodSource implements FinanceCropPeriodSource {
  const BackendFinanceCropPeriodSource({required ApiClient apiClient})
    : _api = apiClient;
  final ApiClient _api;
  @override
  Future<List<FinanceCropPeriod>> listCropPeriods(String farmId) async {
    final json = await _api.getJson('/farms/$farmId/production-periods');
    final items = json['items'] as List? ?? const [];
    return items.whereType<Map>().map((raw) {
      final item = Map<String, dynamic>.from(raw);
      return FinanceCropPeriod(
        id: item['id'].toString(),
        cropName: item['crop_name']?.toString() ?? '',
        plantedAt: DateTime.parse(item['planted_at'].toString()),
        status: item['status']?.toString() ?? '',
      );
    }).toList();
  }
}

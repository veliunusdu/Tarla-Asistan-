// ignore_for_file: prefer_initializing_formals
import '../../../services/api_client.dart';
import '../domain/weather_summary.dart';
import 'weather_repository.dart';

class WeatherLocationRequiredException implements Exception {
  const WeatherLocationRequiredException();
}

/// Backend `/api/v1/farms/{farmId}/weather` endpoint'ini kullanan implementasyon.
///
/// Endpoint: GET /api/v1/farms/{farm_id}/weather
/// Auth: Bearer token (Firebase ID token)
/// Response: FarmWeatherResponse
///   - farm_id, provider, fetched_at, is_stale, stale_reason
///   - points[]: WeatherPointResponse (observed_at, temperature_c,
///               precipitation_probability, precipitation_mm, wind_speed_kmh)
///   - risks[]: WeatherRiskResponse (risk_type, severity, starts_at,
///              ends_at, message, suggested_action)
class BackendWeatherRepository implements WeatherRepository {
  const BackendWeatherRepository({required ApiClient apiClient, String? farmId})
    : _client = apiClient,
      _farmId = farmId;

  final ApiClient _client;
  final String? _farmId;

  @override
  Future<WeatherSummary> getWeather() async {
    final Map<String, dynamic> raw;
    try {
      final farmId = _farmId ?? await _findFirstFarmId();
      raw = await _client.getJson('farms/$farmId/weather');
    } on ApiException {
      rethrow;
    }

    final points = raw['points'];
    if (points is! List || points.isEmpty) {
      throw const ApiException(
        'Hava durumu noktası bulunamadı.',
        statusCode: null,
      );
    }

    final first = points.first;
    if (first is! Map<String, dynamic>) {
      throw const ApiException(
        'Hava durumu verisi geçersiz.',
        statusCode: null,
      );
    }

    final tempC = first['temperature_c'];
    final temperature = tempC != null ? (tempC as num).round() : 0;

    final precipProb = first['precipitation_probability'];
    final description = _descriptionFromWeather(
      precipitationProbability: precipProb is num
          ? precipProb.toDouble()
          : null,
      temperature: temperature,
      isStale: raw['is_stale'] as bool? ?? false,
      staleReason: raw['stale_reason'] as String?,
    );

    return WeatherSummary(temperature: temperature, description: description);
  }

  Future<String> _findFirstFarmId() async {
    final items = await _client.getJsonList('farms?limit=50&offset=0');
    if (items.isEmpty) {
      throw const WeatherLocationRequiredException();
    }
    for (final item in items) {
      if (item is! Map) continue;
      final id = item['id'];
      if (id is String &&
          id.isNotEmpty &&
          item['latitude'] != null &&
          item['longitude'] != null) {
        return id;
      }
    }
    throw const WeatherLocationRequiredException();
  }

  static String _descriptionFromWeather({
    double? precipitationProbability,
    required int temperature,
    required bool isStale,
    String? staleReason,
  }) {
    if (isStale && staleReason != null) return staleReason;
    if (precipitationProbability != null && precipitationProbability >= 60) {
      return 'Yağmur ihtimali yüksek';
    }
    if (temperature <= 0) return 'Dondurucu soğuk';
    if (temperature <= 10) return 'Soğuk hava';
    if (temperature >= 35) return 'Çok sıcak hava';
    return 'Hava durumu güncellendi';
  }
}

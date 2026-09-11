// ignore_for_file: prefer_initializing_formals
import '../../../services/api_client.dart';
import '../domain/weather_summary.dart';
import 'local_weather_repository.dart';
import 'weather_repository.dart';

class WeatherLocationRequiredException implements Exception {
  const WeatherLocationRequiredException();
}

class WeatherNoFarmsException implements Exception {
  const WeatherNoFarmsException();
}

class WeatherUnavailableException implements Exception {
  const WeatherUnavailableException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Backend `/api/v1/farms/{farmId}/weather` endpoint'ini kullanan implementasyon.
class BackendWeatherRepository implements WeatherRepository {
  const BackendWeatherRepository({
    required ApiClient apiClient,
    String? farmId,
    LocalWeatherRepository localRepo = const LocalWeatherRepository(),
  }) : _client = apiClient,
       _farmId = farmId,
       _localRepo = localRepo;

  final ApiClient _client;
  final String? _farmId;
  final LocalWeatherRepository _localRepo;

  @override
  Future<WeatherSummary> getWeather({
    String? farmId,
    double? latitude,
    double? longitude,
  }) async {
    String? targetFarmId = farmId ?? _farmId;
    var targetLatitude = latitude;
    var targetLongitude = longitude;
    final Map<String, dynamic> raw;
    try {
      if (targetFarmId == null) {
        final target = await _findFirstFarm();
        targetFarmId = target.id;
        targetLatitude = target.latitude;
        targetLongitude = target.longitude;
      }
      raw = await _client.getJson('farms/$targetFarmId/weather');
    } on ApiException catch (error) {
      if (error.statusCode == 422) {
        throw const WeatherLocationRequiredException();
      }
      if (error.statusCode == 503 || error.retryable) {
        final fallback = await _getCachedFallback(
          targetFarmId,
          latitude: targetLatitude,
          longitude: targetLongitude,
        );
        if (fallback != null) return fallback;
      }
      if (error.statusCode == 503) {
        throw WeatherUnavailableException(error.message);
      }
      rethrow;
    } catch (e) {
      if (e is WeatherLocationRequiredException) {
        rethrow;
      }
      final fallback = await _getCachedFallback(
        targetFarmId,
        latitude: targetLatitude,
        longitude: targetLongitude,
      );
      if (fallback != null) return fallback;
      rethrow;
    }

    final current = raw['current'] is Map ? raw['current'] as Map : null;
    final points = raw['points'] is List ? raw['points'] as List : null;
    final daily = raw['daily'] is List ? raw['daily'] as List : null;

    final Map? nearestPoint = _nearestForecastPoint(
      points,
      DateTime.now().toUtc(),
    );

    final Map? firstDaily =
        (daily != null && daily.isNotEmpty && daily.first is Map)
        ? daily.first as Map
        : null;

    // 1. Current mapping
    final num? tempFromCurrent = current != null
        ? _toNum(current['temperature_c'])
        : null;
    final double? feelsLike = current != null
        ? _toDouble(current['feels_like_c'])
        : null;
    final double? humidityFromCurrent = current != null
        ? _toDouble(current['humidity_percent'])
        : null;
    final double? windSpeedFromCurrent = current != null
        ? _toDouble(current['wind_speed_kmh'])
        : null;
    final double? windGust = current != null
        ? _toDouble(current['wind_gusts_kmh'])
        : null;
    final String? condFromCurrent = current?['condition']?.toString();
    final int? codeFromCurrent = current != null
        ? _toInt(current['weather_code'])
        : null;
    final DateTime? observedFromCurrent = current != null
        ? _toDateTime(current['observed_at'])
        : null;

    // 2. Daily mapping (daily is an array; safe index 0)
    final double? minTemperature = firstDaily != null
        ? _toDouble(firstDaily['min_temperature_c'])
        : null;
    final double? maxTemperature = firstDaily != null
        ? _toDouble(firstDaily['max_temperature_c'])
        : null;
    final double? precipProbFromDaily = firstDaily != null
        ? _toDouble(firstDaily['precipitation_probability'])
        : null;
    final double? precipAmountFromDaily = firstDaily != null
        ? _toDouble(firstDaily['precipitation_mm'])
        : null;
    final String? condFromDaily = firstDaily?['condition']?.toString();
    final int? codeFromDaily = firstDaily != null
        ? _toInt(firstDaily['weather_code'])
        : null;

    // 3. Points fallback (only when current/daily data is absent)
    final num? tempFromPoint = nearestPoint != null
        ? _toNum(nearestPoint['temperature_c'])
        : null;
    final double? humidityFromPoint = nearestPoint != null
        ? _toDouble(nearestPoint['humidity_percent'])
        : null;
    final double? windSpeedFromPoint = nearestPoint != null
        ? _toDouble(nearestPoint['wind_speed_kmh'])
        : null;
    final double? precipProbFromPoint = nearestPoint != null
        ? _toDouble(nearestPoint['precipitation_probability'])
        : null;
    final double? precipAmountFromPoint = nearestPoint != null
        ? _toDouble(nearestPoint['precipitation_mm'])
        : null;
    final int? codeFromPoint = nearestPoint != null
        ? _toInt(nearestPoint['weather_code'])
        : null;
    final DateTime? observedFromPoint = nearestPoint != null
        ? _toDateTime(nearestPoint['observed_at'])
        : null;

    // Resolved values
    final num? temperature = tempFromCurrent ?? tempFromPoint;
    final double? humidity = humidityFromCurrent ?? humidityFromPoint;
    final double? windSpeed = windSpeedFromCurrent ?? windSpeedFromPoint;
    final double? precipitationProbability =
        precipProbFromDaily ?? precipProbFromPoint;
    final double? precipitationAmount =
        precipAmountFromDaily ?? precipAmountFromPoint;
    final int? weatherCode = codeFromCurrent ?? codeFromDaily ?? codeFromPoint;
    final DateTime? observedAt = observedFromCurrent ?? observedFromPoint;
    final DateTime? fetchedAt = _toDateTime(raw['fetched_at']);

    // Condition resolution
    final String? condition =
        (condFromCurrent != null && condFromCurrent.isNotEmpty)
        ? condFromCurrent
        : ((condFromDaily != null && condFromDaily.isNotEmpty)
              ? condFromDaily
              : null);

    // Description resolution (independent from is_stale)
    final String description;
    if (condition != null && condition.isNotEmpty) {
      description = condition;
    } else if (weatherCode != null) {
      final fromCode = _descriptionFromWeatherCode(weatherCode);
      description = fromCode.isNotEmpty
          ? fromCode
          : _fallbackDescription(
              temperature: temperature,
              precipitationProbability: precipitationProbability,
            );
    } else {
      description = _fallbackDescription(
        temperature: temperature,
        precipitationProbability: precipitationProbability,
      );
    }

    // Root status
    final bool isStale = raw['is_stale'] == true;
    final String? staleReason = raw['stale_reason']?.toString();
    final List<WeatherRisk> risks = _parseRisks(raw['risks']);

    final result = WeatherSummary(
      temperature: temperature,
      description: description,
      condition: condition,
      feelsLike: feelsLike,
      humidity: humidity,
      windSpeed: windSpeed,
      windGust: windGust,
      minTemperature: minTemperature,
      maxTemperature: maxTemperature,
      precipitationProbability: precipitationProbability,
      precipitationAmount: precipitationAmount,
      risks: risks,
      isStale: isStale,
      staleReason: staleReason,
      weatherCode: weatherCode,
      observedAt: observedAt,
      fetchedAt: fetchedAt,
    );

    await _localRepo.cacheWeather(
      farmId: targetFarmId,
      latitude: targetLatitude,
      longitude: targetLongitude,
      weather: result,
    );
    return result;
  }

  Future<({String id, double latitude, double longitude})>
  _findFirstFarm() async {
    final items = await _client.getJsonList('farms?limit=50&offset=0');
    if (items.isEmpty) {
      throw const WeatherLocationRequiredException();
    }
    for (final item in items) {
      if (item is! Map) continue;
      final id = item['id'];
      final latitude = _toDouble(item['latitude']);
      final longitude = _toDouble(item['longitude']);
      if (id is String &&
          id.isNotEmpty &&
          latitude != null &&
          longitude != null) {
        return (id: id, latitude: latitude, longitude: longitude);
      }
    }
    throw const WeatherLocationRequiredException();
  }

  static Map? _nearestForecastPoint(List? points, DateTime now) {
    Map? selected;
    DateTime? selectedTime;
    int? closestDistance;
    for (final point in points ?? const []) {
      if (point is! Map) continue;
      final observedAt = _toDateTime(point['observed_at'])?.toUtc();
      if (observedAt == null) continue;
      final distance = observedAt.difference(now).inMicroseconds.abs();
      // Compare instants, not list order; on a tie prefer the earlier forecast.
      if (closestDistance == null ||
          distance < closestDistance ||
          (distance == closestDistance && observedAt.isBefore(selectedTime!))) {
        selected = point;
        selectedTime = observedAt;
        closestDistance = distance;
      }
    }
    return selected;
  }

  static num? _toNum(dynamic value) {
    if (value is num) return value;
    if (value is String) return num.tryParse(value);
    return null;
  }

  static double? _toDouble(dynamic value) {
    if (value is num) return value.toDouble();
    if (value is String) return double.tryParse(value);
    return null;
  }

  static int? _toInt(dynamic value) {
    if (value is num) return value.toInt();
    if (value is String) return int.tryParse(value);
    return null;
  }

  static DateTime? _toDateTime(dynamic value) {
    if (value == null) return null;
    if (value is DateTime) return value;
    return DateTime.tryParse(value.toString());
  }

  static List<WeatherRisk> _parseRisks(dynamic rawRisks) {
    if (rawRisks is! List) return const [];
    final risks = <WeatherRisk>[];
    for (final item in rawRisks) {
      if (item is! Map) continue;
      try {
        final map = Map<String, dynamic>.from(item);
        risks.add(
          WeatherRisk(
            riskType: (map['risk_type'] ?? map['riskType'] ?? '').toString(),
            severity: (map['severity'] ?? 'LOW').toString(),
            startsAt: _toDateTime(map['starts_at'] ?? map['startsAt']),
            endsAt: _toDateTime(map['ends_at'] ?? map['endsAt']),
            message: (map['message'] ?? map['description'] ?? '').toString(),
            suggestedAction: (map['suggested_action'] ?? map['suggestedAction'])
                ?.toString(),
          ),
        );
      } catch (_) {
        // Malformed single risk does not crash entire weather response
      }
    }
    return risks;
  }

  static String _descriptionFromWeatherCode(int? code) {
    if (code == null) return '';
    switch (code) {
      case 0:
        return 'Açık';
      case 1:
        return 'Çoğunlukla Açık';
      case 2:
        return 'Parçalı Bulutlu';
      case 3:
        return 'Bulutlu';
      case 45:
      case 48:
        return 'Sisli';
      case 51:
      case 53:
      case 55:
        return 'Çiseleyen Yağmur';
      case 56:
      case 57:
        return 'Dondurucu Çiseleme';
      case 61:
        return 'Hafif Yağmurlu';
      case 63:
        return 'Yağmurlu';
      case 65:
        return 'Şiddetli Yağmurlu';
      case 66:
      case 67:
        return 'Dondurucu Yağmur';
      case 71:
        return 'Hafif Karlı';
      case 73:
        return 'Karlı';
      case 75:
        return 'Yoğun Karlı';
      case 77:
        return 'Kar Taneli';
      case 80:
        return 'Hafif Sağanak';
      case 81:
        return 'Sağanak Yağışlı';
      case 82:
        return 'Şiddetli Sağanak';
      case 85:
      case 86:
        return 'Kar Sağanağı';
      case 95:
        return 'Gök Gürültülü Fırtına';
      case 96:
      case 99:
        return 'Dolu ile Karışık Fırtına';
      default:
        return 'Bulutlu';
    }
  }

  static String _fallbackDescription({
    num? temperature,
    double? precipitationProbability,
  }) {
    if (precipitationProbability != null && precipitationProbability >= 60) {
      return 'Yağmur ihtimali yüksek';
    }
    if (temperature != null) {
      if (temperature <= 0) return 'Dondurucu soğuk';
      if (temperature <= 10) return 'Soğuk hava';
      if (temperature >= 35) return 'Çok sıcak hava';
    }
    return '';
  }

  Future<WeatherSummary?> _getCachedFallback(
    String? farmId, {
    double? latitude,
    double? longitude,
  }) async {
    final cached = await _localRepo.getCachedWeather(
      farmId: farmId,
      latitude: latitude,
      longitude: longitude,
    );
    if (cached == null) return null;
    return WeatherSummary(
      temperature: cached.temperature,
      description: cached.description,
      condition: cached.condition,
      feelsLike: cached.feelsLike,
      humidity: cached.humidity,
      windSpeed: cached.windSpeed,
      windGust: cached.windGust,
      minTemperature: cached.minTemperature,
      maxTemperature: cached.maxTemperature,
      precipitationProbability: cached.precipitationProbability,
      precipitationAmount: cached.precipitationAmount,
      risks: cached.risks,
      isStale: true,
      staleReason: cached.staleReason ?? 'Çevrimdışı önbellek verisi',
      weatherCode: cached.weatherCode,
      observedAt: cached.observedAt,
      fetchedAt: cached.fetchedAt,
    );
  }
}

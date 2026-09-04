class WeatherRisk {
  const WeatherRisk({
    required this.riskType,
    required this.severity,
    this.startsAt,
    this.endsAt,
    required this.message,
    this.suggestedAction,
  });

  /// Risk type identifier (e.g. 'FROST', 'STRONG_WIND', 'HEAVY_RAIN').
  final String riskType;

  /// Severity level (e.g. 'CRITICAL', 'HIGH', 'MEDIUM', 'LOW').
  final String severity;

  /// Window start timestamp.
  final DateTime? startsAt;

  /// Window end timestamp.
  final DateTime? endsAt;

  /// Explanatory message for farmer.
  final String message;

  /// Actionable suggestion.
  final String? suggestedAction;

  bool get isCritical => severity.toUpperCase() == 'CRITICAL';
  bool get isHigh => severity.toUpperCase() == 'HIGH';

  /// Alias for message to support legacy/alternative terminology.
  String get description => message;

  factory WeatherRisk.fromJson(Map<String, dynamic> json) {
    return WeatherRisk(
      riskType: (json['riskType'] ?? json['risk_type'] ?? '').toString(),
      severity: (json['severity'] ?? 'LOW').toString(),
      startsAt: json['startsAt'] != null || json['starts_at'] != null
          ? DateTime.tryParse((json['startsAt'] ?? json['starts_at']).toString())
          : null,
      endsAt: json['endsAt'] != null || json['ends_at'] != null
          ? DateTime.tryParse((json['endsAt'] ?? json['ends_at']).toString())
          : null,
      message: (json['message'] ?? json['description'] ?? '').toString(),
      suggestedAction:
          (json['suggestedAction'] ?? json['suggested_action'])?.toString(),
    );
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'riskType': riskType,
      'severity': severity,
      if (startsAt != null) 'startsAt': startsAt!.toIso8601String(),
      if (endsAt != null) 'endsAt': endsAt!.toIso8601String(),
      'message': message,
      if (suggestedAction != null) 'suggestedAction': suggestedAction,
    };
  }

  WeatherRisk copyWith({
    String? riskType,
    String? severity,
    DateTime? startsAt,
    DateTime? endsAt,
    String? message,
    String? suggestedAction,
  }) {
    return WeatherRisk(
      riskType: riskType ?? this.riskType,
      severity: severity ?? this.severity,
      startsAt: startsAt ?? this.startsAt,
      endsAt: endsAt ?? this.endsAt,
      message: message ?? this.message,
      suggestedAction: suggestedAction ?? this.suggestedAction,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is WeatherRisk &&
          runtimeType == other.runtimeType &&
          riskType == other.riskType &&
          severity == other.severity &&
          startsAt == other.startsAt &&
          endsAt == other.endsAt &&
          message == other.message &&
          suggestedAction == other.suggestedAction;

  @override
  int get hashCode => Object.hash(
        riskType,
        severity,
        startsAt,
        endsAt,
        message,
        suggestedAction,
      );

  @override
  String toString() =>
      'WeatherRisk(riskType: $riskType, severity: $severity, startsAt: $startsAt, endsAt: $endsAt, message: $message, suggestedAction: $suggestedAction)';
}

/// Domain model representing current weather conditions, forecast summaries,
/// risk evaluations, and freshness status for a farm.
class WeatherSummary {
  const WeatherSummary({
    this.temperature,
    required this.description,
    this.condition,
    this.feelsLike,
    this.humidity,
    this.windSpeed,
    this.windGust,
    this.minTemperature,
    this.maxTemperature,
    this.precipitationProbability,
    this.precipitationAmount,
    this.risks = const [],
    this.isStale = false,
    this.staleReason,
    this.weatherCode,
    this.observedAt,
    this.fetchedAt,
  });

  /// Current temperature in Celsius (°C). Null if data is missing/unavailable.
  final num? temperature;

  /// Textual summary or status message (e.g. 'Güneşli', 'Soğuk hava').
  final String description;

  /// Weather condition text (e.g. 'Açık', 'Parçalı Bulutlu', 'Yağmurlu').
  final String? condition;

  /// Apparent / feels like temperature in Celsius (°C).
  final double? feelsLike;

  /// Relative humidity percentage (0 - 100).
  final double? humidity;

  /// Wind speed in km/h.
  final double? windSpeed;

  /// Wind gust speed in km/h.
  final double? windGust;

  /// Daily minimum forecast temperature in Celsius (°C).
  final double? minTemperature;

  /// Daily maximum forecast temperature in Celsius (°C).
  final double? maxTemperature;

  /// Precipitation probability percentage (0 - 100).
  final double? precipitationProbability;

  /// Expected precipitation amount in millimeters (mm).
  final double? precipitationAmount;

  /// Active weather risks evaluated for the farm.
  final List<WeatherRisk> risks;

  /// True if data is stale or served from fallback cache.
  final bool isStale;

  /// Explanation for why data is stale, if applicable.
  final String? staleReason;

  /// WMO weather interpretation code.
  final int? weatherCode;

  /// Timestamp when observation was recorded.
  final DateTime? observedAt;

  /// Timestamp when forecast was fetched.
  final DateTime? fetchedAt;

  /// True if temperature data is available.
  bool get hasTemperature => temperature != null;

  bool get hasRisks => risks.isNotEmpty;
  bool get hasCriticalRisks => risks.any((r) => r.isCritical);

  /// Helper to get condition if present, otherwise description if not empty.
  String? get displayCondition =>
      (condition != null && condition!.isNotEmpty)
          ? condition
          : (description.isNotEmpty ? description : null);

  /// Simple deserialization of WeatherSummary's own flat fields.
  factory WeatherSummary.fromJson(Map<String, dynamic> json) {
    final rawRisks = json['risks'];
    final List<WeatherRisk> risks;
    if (rawRisks is List) {
      risks = rawRisks
          .whereType<Map>()
          .map((e) => WeatherRisk.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    } else {
      risks = const [];
    }

    final rawObservedAt = json['observedAt'] ?? json['observed_at'];
    final rawFetchedAt = json['fetchedAt'] ?? json['fetched_at'];

    return WeatherSummary(
      temperature: (json['temperature'] ?? json['temperature_c']) as num?,
      description: (json['description'] as String?) ?? '',
      condition: json['condition'] as String?,
      feelsLike: ((json['feelsLike'] ?? json['feels_like'] ?? json['feels_like_c']) as num?)?.toDouble(),
      humidity: ((json['humidity'] ?? json['humidity_percent']) as num?)?.toDouble(),
      windSpeed: ((json['windSpeed'] ?? json['wind_speed'] ?? json['wind_speed_kmh']) as num?)?.toDouble(),
      windGust: ((json['windGust'] ?? json['wind_gust'] ?? json['wind_gusts_kmh']) as num?)?.toDouble(),
      minTemperature: ((json['minTemperature'] ?? json['min_temperature'] ?? json['min_temperature_c']) as num?)?.toDouble(),
      maxTemperature: ((json['maxTemperature'] ?? json['max_temperature'] ?? json['max_temperature_c']) as num?)?.toDouble(),
      precipitationProbability: ((json['precipitationProbability'] ?? json['precipitation_probability']) as num?)?.toDouble(),
      precipitationAmount: ((json['precipitationAmount'] ?? json['precipitation_amount'] ?? json['precipitation_mm']) as num?)?.toDouble(),
      risks: risks,
      isStale: ((json['isStale'] ?? json['is_stale']) as bool?) ?? false,
      staleReason: (json['staleReason'] ?? json['stale_reason']) as String?,
      weatherCode: ((json['weatherCode'] ?? json['weather_code']) as num?)?.toInt(),
      observedAt: rawObservedAt != null
          ? DateTime.tryParse(rawObservedAt.toString())
          : null,
      fetchedAt: rawFetchedAt != null
          ? DateTime.tryParse(rawFetchedAt.toString())
          : null,
    );
  }

  /// Simple serialization of WeatherSummary's own flat fields.
  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      if (temperature != null) 'temperature': temperature,
      'description': description,
      if (condition != null) 'condition': condition,
      if (feelsLike != null) 'feelsLike': feelsLike,
      if (humidity != null) 'humidity': humidity,
      if (windSpeed != null) 'windSpeed': windSpeed,
      if (windGust != null) 'windGust': windGust,
      if (minTemperature != null) 'minTemperature': minTemperature,
      if (maxTemperature != null) 'maxTemperature': maxTemperature,
      if (precipitationProbability != null)
        'precipitationProbability': precipitationProbability,
      if (precipitationAmount != null)
        'precipitationAmount': precipitationAmount,
      'risks': risks.map((r) => r.toJson()).toList(),
      'isStale': isStale,
      if (staleReason != null) 'staleReason': staleReason,
      if (weatherCode != null) 'weatherCode': weatherCode,
      if (observedAt != null) 'observedAt': observedAt!.toIso8601String(),
      if (fetchedAt != null) 'fetchedAt': fetchedAt!.toIso8601String(),
    };
  }

  WeatherSummary copyWith({
    num? temperature,
    String? description,
    String? condition,
    double? feelsLike,
    double? humidity,
    double? windSpeed,
    double? windGust,
    double? minTemperature,
    double? maxTemperature,
    double? precipitationProbability,
    double? precipitationAmount,
    List<WeatherRisk>? risks,
    bool? isStale,
    String? staleReason,
    int? weatherCode,
    DateTime? observedAt,
    DateTime? fetchedAt,
  }) {
    return WeatherSummary(
      temperature: temperature ?? this.temperature,
      description: description ?? this.description,
      condition: condition ?? this.condition,
      feelsLike: feelsLike ?? this.feelsLike,
      humidity: humidity ?? this.humidity,
      windSpeed: windSpeed ?? this.windSpeed,
      windGust: windGust ?? this.windGust,
      minTemperature: minTemperature ?? this.minTemperature,
      maxTemperature: maxTemperature ?? this.maxTemperature,
      precipitationProbability:
          precipitationProbability ?? this.precipitationProbability,
      precipitationAmount: precipitationAmount ?? this.precipitationAmount,
      risks: risks ?? this.risks,
      isStale: isStale ?? this.isStale,
      staleReason: staleReason ?? this.staleReason,
      weatherCode: weatherCode ?? this.weatherCode,
      observedAt: observedAt ?? this.observedAt,
      fetchedAt: fetchedAt ?? this.fetchedAt,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is WeatherSummary &&
          runtimeType == other.runtimeType &&
          temperature == other.temperature &&
          description == other.description &&
          condition == other.condition &&
          feelsLike == other.feelsLike &&
          humidity == other.humidity &&
          windSpeed == other.windSpeed &&
          windGust == other.windGust &&
          minTemperature == other.minTemperature &&
          maxTemperature == other.maxTemperature &&
          precipitationProbability == other.precipitationProbability &&
          precipitationAmount == other.precipitationAmount &&
          isStale == other.isStale &&
          staleReason == other.staleReason &&
          weatherCode == other.weatherCode &&
          observedAt == other.observedAt &&
          fetchedAt == other.fetchedAt &&
          _listEquals(risks, other.risks);

  @override
  int get hashCode => Object.hashAll([
        temperature,
        description,
        condition,
        feelsLike,
        humidity,
        windSpeed,
        windGust,
        minTemperature,
        maxTemperature,
        precipitationProbability,
        precipitationAmount,
        isStale,
        staleReason,
        weatherCode,
        observedAt,
        fetchedAt,
        Object.hashAll(risks),
      ]);

  @override
  String toString() =>
      'WeatherSummary(temperature: $temperature, description: $description, condition: $condition, feelsLike: $feelsLike, humidity: $humidity, windSpeed: $windSpeed, windGust: $windGust, minTemp: $minTemperature, maxTemp: $maxTemperature, rainProb: $precipitationProbability, rainMm: $precipitationAmount, risks: ${risks.length}, isStale: $isStale)';

  static bool _listEquals<T>(List<T>? a, List<T>? b) {
    if (a == null) return b == null;
    if (b == null || a.length != b.length) return false;
    if (identical(a, b)) return true;
    for (int i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }
}


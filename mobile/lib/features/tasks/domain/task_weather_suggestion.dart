import 'package:flutter/foundation.dart';

/// Domain model representing a weather-based postponement recommendation
/// attached to a [FarmTask].
///
/// Returned optionally by `GET /api/v1/farms/{farmId}/tasks` under the
/// `weather_postpone_suggestion` field.
@immutable
class TaskWeatherSuggestion {
  const TaskWeatherSuggestion({
    required this.riskLevel,
    required this.reason,
    required this.reasons,
    required this.suggestedAction,
    required this.evaluatedAtUtc,
    required this.isWeatherStale,
    required this.canApply,
    this.advisoryId,
    this.recommendedDate,
    this.weatherFetchedAtUtc,
    this.staleReason,
  });

  /// The ID of the proactive advisory to be applied via
  /// `POST /api/v1/ai/advisories/{advisoryId}/apply`.
  /// Null if no advisory was persisted on backend.
  final String? advisoryId;

  /// Risk level description (e.g. "Low", "Medium", "High", "Critical").
  final String riskLevel;

  /// Primary agronomic risk reason.
  final String reason;

  /// Detailed list of risk factors / reasons.
  final List<String> reasons;

  /// Recommended action for the farmer (e.g. "İlaçlamayı erteleyin").
  final String suggestedAction;

  /// Recommended alternative date for the task (calendar date only).
  final DateTime? recommendedDate;

  /// Evaluation timestamp in UTC.
  final DateTime evaluatedAtUtc;

  /// Timestamp when the underlying weather data was fetched from provider/snapshot.
  final DateTime? weatherFetchedAtUtc;

  /// True if the weather snapshot is older than the configured freshness threshold.
  final bool isWeatherStale;

  /// Human-readable explanation when weather is stale or missing.
  final String? staleReason;

  /// True if this suggestion can be actively applied by the farmer.
  final bool canApply;

  /// Constructs a [TaskWeatherSuggestion] from backend JSON.
  ///
  /// Supports both snake_case (standard backend contract) and camelCase (cache roundtrip).
  factory TaskWeatherSuggestion.fromJson(Map<String, dynamic> json) {
    return TaskWeatherSuggestion(
      advisoryId: (json['advisory_id'] ?? json['advisoryId'])?.toString(),
      riskLevel: (json['risk_level'] ?? json['riskLevel'] ?? '').toString(),
      reason: (json['reason'] ?? '').toString(),
      reasons: _parseReasons(json['reasons']),
      suggestedAction:
          (json['suggested_action'] ?? json['suggestedAction'] ?? '').toString(),
      recommendedDate: _parseDateOnly(
        json['recommended_date'] ?? json['recommendedDate'],
      ),
      evaluatedAtUtc: _parseUtc(
            json['evaluated_at_utc'] ?? json['evaluatedAtUtc'],
          ) ??
          DateTime.now().toUtc(),
      weatherFetchedAtUtc: _parseUtc(
        json['weather_fetched_at_utc'] ?? json['weatherFetchedAtUtc'],
      ),
      isWeatherStale:
          (json['is_weather_stale'] ?? json['isWeatherStale']) as bool? ??
              false,
      staleReason: (json['stale_reason'] ?? json['staleReason'])?.toString(),
      canApply: (json['can_apply'] ?? json['canApply']) as bool? ?? false,
    );
  }

  /// Converts this suggestion to a JSON-compatible map for caching.
  Map<String, dynamic> toJson() => {
        'advisory_id': advisoryId,
        'risk_level': riskLevel,
        'reason': reason,
        'reasons': reasons,
        'suggested_action': suggestedAction,
        'recommended_date': recommendedDate != null
            ? '${recommendedDate!.year.toString().padLeft(4, '0')}-${recommendedDate!.month.toString().padLeft(2, '0')}-${recommendedDate!.day.toString().padLeft(2, '0')}'
            : null,
        'evaluated_at_utc': evaluatedAtUtc.toUtc().toIso8601String(),
        'weather_fetched_at_utc': weatherFetchedAtUtc?.toUtc().toIso8601String(),
        'is_weather_stale': isWeatherStale,
        'stale_reason': staleReason,
        'can_apply': canApply,
      };

  TaskWeatherSuggestion copyWith({
    String? advisoryId,
    bool clearAdvisoryId = false,
    String? riskLevel,
    String? reason,
    List<String>? reasons,
    String? suggestedAction,
    DateTime? recommendedDate,
    bool clearRecommendedDate = false,
    DateTime? evaluatedAtUtc,
    DateTime? weatherFetchedAtUtc,
    bool clearWeatherFetchedAtUtc = false,
    bool? isWeatherStale,
    String? staleReason,
    bool clearStaleReason = false,
    bool? canApply,
  }) {
    return TaskWeatherSuggestion(
      advisoryId: clearAdvisoryId ? null : (advisoryId ?? this.advisoryId),
      riskLevel: riskLevel ?? this.riskLevel,
      reason: reason ?? this.reason,
      reasons: reasons ?? this.reasons,
      suggestedAction: suggestedAction ?? this.suggestedAction,
      recommendedDate:
          clearRecommendedDate ? null : (recommendedDate ?? this.recommendedDate),
      evaluatedAtUtc: evaluatedAtUtc ?? this.evaluatedAtUtc,
      weatherFetchedAtUtc: clearWeatherFetchedAtUtc
          ? null
          : (weatherFetchedAtUtc ?? this.weatherFetchedAtUtc),
      isWeatherStale: isWeatherStale ?? this.isWeatherStale,
      staleReason:
          clearStaleReason ? null : (staleReason ?? this.staleReason),
      canApply: canApply ?? this.canApply,
    );
  }

  static List<String> _parseReasons(Object? raw) {
    if (raw == null) return const [];
    if (raw is List) {
      return raw.map((e) => e.toString()).toList();
    }
    return const [];
  }

  /// Parses a date-only string (e.g. "2026-09-08") preserving the exact calendar day
  /// regardless of device timezone (avoids day shifts caused by UTC/local conversions).
  static DateTime? _parseDateOnly(Object? raw) {
    if (raw == null) return null;
    final str = raw.toString().trim();
    if (str.isEmpty) return null;

    final match = RegExp(r'^(\d{4})-(\d{2})-(\d{2})').firstMatch(str);
    if (match != null) {
      final year = int.tryParse(match.group(1)!);
      final month = int.tryParse(match.group(2)!);
      final day = int.tryParse(match.group(3)!);
      if (year != null && month != null && day != null) {
        return DateTime(year, month, day);
      }
    }

    final parsed = DateTime.tryParse(str);
    if (parsed != null) {
      return DateTime(parsed.year, parsed.month, parsed.day);
    }
    return null;
  }

  static DateTime? _parseUtc(Object? raw) {
    if (raw == null) return null;
    final parsed = DateTime.tryParse(raw.toString());
    return parsed?.toUtc();
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is TaskWeatherSuggestion &&
          runtimeType == other.runtimeType &&
          advisoryId == other.advisoryId &&
          riskLevel == other.riskLevel &&
          reason == other.reason &&
          listEquals(reasons, other.reasons) &&
          suggestedAction == other.suggestedAction &&
          recommendedDate == other.recommendedDate &&
          evaluatedAtUtc == other.evaluatedAtUtc &&
          weatherFetchedAtUtc == other.weatherFetchedAtUtc &&
          isWeatherStale == other.isWeatherStale &&
          staleReason == other.staleReason &&
          canApply == other.canApply;

  @override
  int get hashCode => Object.hash(
        advisoryId,
        riskLevel,
        reason,
        Object.hashAll(reasons),
        suggestedAction,
        recommendedDate,
        evaluatedAtUtc,
        weatherFetchedAtUtc,
        isWeatherStale,
        staleReason,
        canApply,
      );
}

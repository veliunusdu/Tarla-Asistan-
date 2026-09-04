import 'package:flutter/material.dart';
import '../../../app/theme/app_colors.dart';
import '../domain/weather_summary.dart';

/// Presentation helper for formatting weather metrics and mapping weather codes to icons.
abstract final class WeatherPresentationHelper {
  /// Formats temperature without redundant '.0' (e.g. 23.0 -> "23°C", 23.6 -> "23.6°C", 0 -> "0°C").
  /// Returns null if [temp] is null.
  static String? formatTemperature(num? temp) {
    if (temp == null) return null;
    final d = temp.toDouble();
    if (d % 1 == 0) {
      return '${d.toInt()}°C';
    }
    final s = d.toStringAsFixed(1);
    if (s.endsWith('.0')) {
      return '${s.substring(0, s.length - 2)}°C';
    }
    return '$s°C';
  }

  /// Formats apparent / feels like temperature (e.g. 22.8 -> "Hissedilen 22.8°C").
  static String? formatFeelsLike(num? feelsLike) {
    if (feelsLike == null) return null;
    final d = feelsLike.toDouble();
    final valStr = (d % 1 == 0) ? '${d.toInt()}' : d.toStringAsFixed(1);
    return 'Hissedilen $valStr°C';
  }

  /// Formats humidity percentage (e.g. 45.0 -> "Nem %45").
  static String? formatHumidity(num? humidity) {
    if (humidity == null) return null;
    final d = humidity.toDouble();
    final valStr = (d % 1 == 0) ? '${d.toInt()}' : d.toStringAsFixed(0);
    return 'Nem %$valStr';
  }

  /// Formats wind speed and optional gust (e.g. "Rüzgâr 12.4 km/sa" or "Rüzgâr 12.4 km/sa · Hamle 18.2").
  static String? formatWind(num? speed, [num? gust]) {
    if (speed == null) return null;
    final s = speed.toDouble();
    final speedStr = (s % 1 == 0) ? '${s.toInt()}' : s.toStringAsFixed(1);
    if (gust != null && gust > speed) {
      final g = gust.toDouble();
      final gustStr = (g % 1 == 0) ? '${g.toInt()}' : g.toStringAsFixed(1);
      return 'Rüzgâr $speedStr km/sa · Hamle $gustStr';
    }
    return 'Rüzgâr $speedStr km/sa';
  }

  /// Formats daily min/max forecast (e.g. "Bugün 14.2° / 28.5°", "Maks. 28.5°C", "Min. 14.2°C").
  static String? formatMinMax(num? min, num? max) {
    if (min == null && max == null) return null;
    if (min != null && max != null) {
      final minD = min.toDouble();
      final maxD = max.toDouble();
      final minStr = (minD % 1 == 0) ? '${minD.toInt()}°' : '${minD.toStringAsFixed(1)}°';
      final maxStr = (maxD % 1 == 0) ? '${maxD.toInt()}°' : '${maxD.toStringAsFixed(1)}°';
      return 'Bugün $minStr / $maxStr';
    }
    if (max != null) {
      final maxD = max.toDouble();
      final maxStr = (maxD % 1 == 0) ? '${maxD.toInt()}' : maxD.toStringAsFixed(1);
      return 'Maks. $maxStr°C';
    }
    final minD = min!.toDouble();
    final minStr = (minD % 1 == 0) ? '${minD.toInt()}' : minD.toStringAsFixed(1);
    return 'Min. $minStr°C';
  }

  /// Formats precipitation probability (e.g. 20 -> "Yağış %20", 0 -> "Yağış %0").
  /// Preserves true 0% meteorological value.
  static String? formatPrecipitation(num? prob) {
    if (prob == null) return null;
    final d = prob.toDouble();
    final valStr = (d % 1 == 0) ? '${d.toInt()}' : d.toStringAsFixed(0);
    return 'Yağış %$valStr';
  }

  /// Capitalizes first letter of weather description.
  static String capitalizeDescription(String text) {
    if (text.isEmpty) return '';
    return text[0].toUpperCase() + text.substring(1);
  }

  /// Returns appropriate Material icon for the weather condition.
  /// Priority:
  /// 1. weatherCode (WMO)
  /// 2. condition / description text fallback
  /// 3. default icon (Icons.cloud_queue - neutral unknown weather icon)
  static IconData getWeatherIcon(int? weatherCode, [String? conditionText]) {
    if (weatherCode != null) {
      switch (weatherCode) {
        case 0:
        case 1:
          return Icons.wb_sunny_outlined;
        case 2:
        case 3:
          return Icons.cloud_outlined;
        case 45:
        case 48:
          return Icons.foggy;
        case 51:
        case 53:
        case 55:
        case 61:
        case 63:
        case 65:
        case 80:
        case 81:
        case 82:
          return Icons.water_drop_outlined;
        case 56:
        case 57:
        case 66:
        case 67:
        case 71:
        case 73:
        case 75:
        case 77:
        case 85:
        case 86:
          return Icons.ac_unit;
        case 95:
        case 96:
        case 99:
          return Icons.thunderstorm_outlined;
        default:
          break;
      }
    }

    if (conditionText != null && conditionText.isNotEmpty) {
      final text = conditionText.toLowerCase();
      if (text.contains('fırtına') ||
          text.contains('şimşek') ||
          text.contains('gök gürült')) {
        return Icons.thunderstorm_outlined;
      }
      if (text.contains('kar') ||
          text.contains('don') ||
          text.contains('buz')) {
        return Icons.ac_unit;
      }
      if (text.contains('yağmur') ||
          text.contains('sağanak') ||
          text.contains('çisele')) {
        return Icons.water_drop_outlined;
      }
      if (text.contains('sis') || text.contains('pus')) {
        return Icons.foggy;
      }
      if (text.contains('bulut')) {
        return Icons.cloud_outlined;
      }
      if (text.contains('güneş') || text.contains('açık')) {
        return Icons.wb_sunny_outlined;
      }
    }

    return Icons.cloud_queue;
  }

  /// Returns semantic color for the weather icon based on condition.
  static Color getWeatherIconColor(int? weatherCode, [String? conditionText]) {
    final icon = getWeatherIcon(weatherCode, conditionText);
    if (icon == Icons.wb_sunny_outlined) {
      return const Color(0xFFF57C00); // Warm sunlight amber
    }
    if (icon == Icons.water_drop_outlined) {
      return const Color(0xFF1976D2); // Rain blue
    }
    if (icon == Icons.thunderstorm_outlined) {
      return const Color(0xFF5E35B1); // Storm deep purple
    }
    if (icon == Icons.ac_unit) {
      return const Color(0xFF0288D1); // Frost icy blue
    }
    return AppColors.textSecondary;
  }

  // ── Risk presentation helpers ──────────────────────────────────────────

  static int _severityRank(String? severity) {
    switch (severity?.trim().toUpperCase()) {
      case 'CRITICAL':
        return 4;
      case 'HIGH':
        return 3;
      case 'MEDIUM':
        return 2;
      case 'LOW':
        return 1;
      default:
        return 0;
    }
  }

  /// Returns a new list of [WeatherRisk] sorted by severity descending
  /// (CRITICAL > HIGH > MEDIUM > LOW > UNKNOWN), without mutating [risks].
  static List<WeatherRisk> sortRisksBySeverity(List<WeatherRisk> risks) {
    if (risks.isEmpty) return const [];
    final indexed = risks.asMap().entries.toList();
    indexed.sort((a, b) {
      final diff = _severityRank(b.value.severity).compareTo(_severityRank(a.value.severity));
      if (diff != 0) return diff;
      return a.key.compareTo(b.key);
    });
    return indexed.map((e) => e.value).toList();
  }

  /// Returns user-friendly Turkish title for a risk type.
  static String getRiskTitle(String? riskType) {
    if (riskType == null || riskType.trim().isEmpty) {
      return 'Hava Uyarısı';
    }
    switch (riskType.trim().toUpperCase()) {
      case 'FROST':
        return 'Don Riski';
      case 'STRONG_WIND':
        return 'Kuvvetli Rüzgâr';
      case 'HEAVY_RAIN':
        return 'Yoğun Yağış';
      case 'EXTREME_HEAT':
      case 'HEAT':
        return 'Aşırı Sıcaklık';
      case 'THUNDERSTORM':
      case 'STORM':
        return 'Fırtına';
      case 'HAIL':
        return 'Dolu Riski';
      case 'SNOW':
        return 'Kar Yağışı';
      default:
        return 'Hava Uyarısı';
    }
  }

  /// Returns semantic Material icon for a risk type.
  static IconData getRiskIcon(String? riskType) {
    if (riskType == null || riskType.trim().isEmpty) {
      return Icons.warning_amber_rounded;
    }
    switch (riskType.trim().toUpperCase()) {
      case 'FROST':
        return Icons.ac_unit;
      case 'STRONG_WIND':
        return Icons.air;
      case 'HEAVY_RAIN':
        return Icons.water_drop_outlined;
      case 'EXTREME_HEAT':
      case 'HEAT':
        return Icons.wb_sunny_outlined;
      case 'THUNDERSTORM':
      case 'STORM':
        return Icons.thunderstorm_outlined;
      case 'HAIL':
      case 'SNOW':
        return Icons.ac_unit;
      default:
        return Icons.warning_amber_rounded;
    }
  }

  /// Returns semantic color for a severity level.
  static Color getRiskSeverityColor(String? severity) {
    switch (severity?.trim().toUpperCase()) {
      case 'CRITICAL':
        return AppColors.error;
      case 'HIGH':
        return AppColors.warning;
      case 'MEDIUM':
        return const Color(0xFFED6C02);
      case 'LOW':
      default:
        return AppColors.textSecondary;
    }
  }

  /// Returns Turkish label for a severity level.
  static String getRiskSeverityLabel(String? severity) {
    switch (severity?.trim().toUpperCase()) {
      case 'CRITICAL':
        return 'Kritik';
      case 'HIGH':
        return 'Yüksek';
      case 'MEDIUM':
        return 'Orta';
      case 'LOW':
        return 'Düşük';
      default:
        return 'Bilgi';
    }
  }

  /// Formats risk timing window in local time (e.g. "03:00–06:00" or "5 Eyl 03:00 – 6 Eyl 06:00").
  /// Returns null if both [startsAt] and [endsAt] are null.
  static String? formatRiskTiming(DateTime? startsAt, DateTime? endsAt) {
    if (startsAt == null && endsAt == null) return null;

    const turkishMonths = [
      '',
      'Oca',
      'Şub',
      'Mar',
      'Nis',
      'May',
      'Haz',
      'Tem',
      'Ağu',
      'Eyl',
      'Eki',
      'Kas',
      'Ara',
    ];

    String pad2(int n) => n.toString().padLeft(2, '0');
    String timeStr(DateTime dt) => '${pad2(dt.hour)}:${pad2(dt.minute)}';
    String dateStr(DateTime dt) => '${dt.day} ${turkishMonths[dt.month]}';

    if (startsAt != null && endsAt != null) {
      final start = startsAt.toLocal();
      final end = endsAt.toLocal();

      final isSameDay = start.year == end.year &&
          start.month == end.month &&
          start.day == end.day;

      if (isSameDay) {
        return '${timeStr(start)}–${timeStr(end)}';
      } else {
        return '${dateStr(start)} ${timeStr(start)} – ${dateStr(end)} ${timeStr(end)}';
      }
    }

    if (startsAt != null) {
      final start = startsAt.toLocal();
      return '${timeStr(start)} itibarıyla';
    }

    final end = endsAt!.toLocal();
    return '${timeStr(end)} saatine kadar';
  }
}

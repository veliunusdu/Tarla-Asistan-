import 'package:flutter/material.dart';
import '../../../../app/theme/app_colors.dart';
import '../../../../app/theme/app_spacing.dart';
import '../../domain/weather_summary.dart';
import '../weather_presentation_helper.dart';

/// Modern, responsive, and clear weather card presenting current conditions,
/// forecast highlights, and farm association for farmers.
class WeatherCard extends StatelessWidget {
  const WeatherCard({
    super.key,
    required this.weather,
    this.tarlaName,
    this.canSelectFarm = false,
    this.onFarmTap,
  });

  /// The domain weather summary to display.
  final WeatherSummary weather;

  /// Optional name of the farm associated with this weather forecast.
  final String? tarlaName;

  /// Whether the farm name is interactive and allows selecting a different farm.
  final bool canSelectFarm;

  /// Callback when user taps the farm selection header.
  final VoidCallback? onFarmTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final icon = WeatherPresentationHelper.getWeatherIcon(
      weather.weatherCode,
      weather.condition ?? weather.description,
    );
    final iconColor = WeatherPresentationHelper.getWeatherIconColor(
      weather.weatherCode,
      weather.condition ?? weather.description,
    );
    final tempStr = WeatherPresentationHelper.formatTemperature(weather.temperature);
    final desc = WeatherPresentationHelper.capitalizeDescription(weather.description);

    final feelsLikeStr = WeatherPresentationHelper.formatFeelsLike(weather.feelsLike);
    final humidityStr = WeatherPresentationHelper.formatHumidity(weather.humidity);
    final windStr = WeatherPresentationHelper.formatWind(weather.windSpeed, weather.windGust);
    final minMaxStr = WeatherPresentationHelper.formatMinMax(weather.minTemperature, weather.maxTemperature);
    final precipStr = WeatherPresentationHelper.formatPrecipitation(weather.precipitationProbability);

    final metrics = <Widget>[];
    if (feelsLikeStr != null) {
      metrics.add(_MetricItem(
        icon: Icons.thermostat_outlined,
        text: feelsLikeStr,
      ));
    }
    if (humidityStr != null) {
      metrics.add(_MetricItem(
        icon: Icons.water_drop_outlined,
        text: humidityStr,
      ));
    }
    if (windStr != null) {
      metrics.add(_MetricItem(
        icon: Icons.air,
        text: windStr,
      ));
    }
    if (minMaxStr != null) {
      metrics.add(_MetricItem(
        icon: Icons.swap_vert,
        text: minMaxStr,
      ));
    }
    if (precipStr != null) {
      metrics.add(_MetricItem(
        icon: Icons.grain,
        text: precipStr,
      ));
    }

    final sortedRisks = WeatherPresentationHelper.sortRisksBySeverity(weather.risks);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // ── Tarla Adı (mevcutsa) ──────────────────────────────────────
            if (tarlaName != null && tarlaName!.isNotEmpty) ...[
              InkWell(
                onTap: canSelectFarm ? onFarmTap : null,
                borderRadius: BorderRadius.circular(4),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      const Icon(
                        Icons.location_on_outlined,
                        size: 15,
                        color: AppColors.primary,
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Text(
                          tarlaName!,
                          style: theme.textTheme.labelMedium?.copyWith(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w600,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      if (canSelectFarm) ...[
                        const SizedBox(width: AppSpacing.xs),
                        const Icon(
                          Icons.arrow_drop_down,
                          size: 18,
                          color: AppColors.primary,
                        ),
                      ],
                    ],
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
            ],

            // ── Ana Görünüm: İkon + Sıcaklık & Durum ────────────────────────
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Icon(
                  icon,
                  color: iconColor,
                  size: 42,
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      if (tempStr != null)
                        Text(
                          tempStr,
                          style: theme.textTheme.headlineMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: AppColors.textPrimary,
                          ),
                        )
                      else
                        Text(
                          'Hava verisi yok',
                          style: theme.textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      if (desc.isNotEmpty) ...[
                        const SizedBox(height: AppSpacing.xs),
                        Text(
                          desc,
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: AppColors.textSecondary,
                            fontWeight: FontWeight.w500,
                          ),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),

            // ── Genel Durum Bilgileri ──────────────────────────────────────
            if (metrics.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.md),
              const Divider(height: 1),
              const SizedBox(height: AppSpacing.sm),
              Wrap(
                spacing: AppSpacing.md,
                runSpacing: AppSpacing.sm,
                children: metrics,
              ),
            ],

            // ── Hava Durumu Uyarıları (Risks) ──────────────────────────────
            if (sortedRisks.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              const Divider(height: 1),
              const SizedBox(height: AppSpacing.sm),
              ...sortedRisks.take(2).map(
                    (risk) => Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                      child: _WeatherRiskItem(risk: risk),
                    ),
                  ),
              if (sortedRisks.length > 2) ...[
                Align(
                  alignment: Alignment.centerRight,
                  child: Text(
                    '+${sortedRisks.length - 2} diğer uyarı',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                      fontWeight: FontWeight.w600,
                      fontSize: 11,
                    ),
                  ),
                ),
              ],
            ],

            // ── Stale Durum Bilgisi ───────────────────────────────────────
            if (weather.isStale) ...[
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  const Icon(
                    Icons.info_outline,
                    size: 13,
                    color: AppColors.textDisabled,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      'Son güncel hava verisi gösteriliyor',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.textDisabled,
                        fontSize: 11,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _WeatherRiskItem extends StatelessWidget {
  const _WeatherRiskItem({required this.risk});

  final WeatherRisk risk;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = WeatherPresentationHelper.getRiskTitle(risk.riskType);
    final icon = WeatherPresentationHelper.getRiskIcon(risk.riskType);
    final color = WeatherPresentationHelper.getRiskSeverityColor(risk.severity);
    final label = WeatherPresentationHelper.getRiskSeverityLabel(risk.severity);
    final timing = WeatherPresentationHelper.formatRiskTiming(risk.startsAt, risk.endsAt);
    final hasAction =
        risk.suggestedAction != null && risk.suggestedAction!.trim().isNotEmpty;

    return Container(
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
          color: color.withValues(alpha: 0.25),
          width: 1,
        ),
      ),
      padding: const EdgeInsets.all(AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                icon,
                size: 18,
                color: color,
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Text(
                  title,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              _RiskSeverityBadge(label: label, color: color),
            ],
          ),
          if (timing != null) ...[
            const SizedBox(height: 3),
            Row(
              children: [
                const Icon(
                  Icons.access_time,
                  size: 12,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    timing,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                      fontSize: 11,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ],
          if (risk.message.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              risk.message,
              style: theme.textTheme.bodySmall?.copyWith(
                color: AppColors.textPrimary,
                height: 1.3,
              ),
            ),
          ],
          if (hasAction) ...[
            const SizedBox(height: 4),
            Text(
              'Öneri: ${risk.suggestedAction!.trim()}',
              style: theme.textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w500,
                height: 1.3,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _RiskSeverityBadge extends StatelessWidget {
  const _RiskSeverityBadge({
    required this.label,
    required this.color,
  });

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: 10,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.3,
        ),
      ),
    );
  }
}

class _MetricItem extends StatelessWidget {
  const _MetricItem({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 15, color: AppColors.textSecondary),
        const SizedBox(width: AppSpacing.xs),
        Flexible(
          child: Text(
            text,
            style: theme.textTheme.bodySmall?.copyWith(
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w500,
            ),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

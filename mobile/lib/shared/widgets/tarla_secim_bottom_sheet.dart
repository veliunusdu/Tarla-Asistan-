import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_spacing.dart';
import '../../models/tarla.dart';

/// Kullanıcının birden fazla tarlası olduğunda (2+) iş ekleme veya hava durumu
/// öncesinde hangi tarla için işlem yapılacağını seçtiren ortak bottom sheet bileşeni.
class TarlaSecimBottomSheet extends StatelessWidget {
  const TarlaSecimBottomSheet({
    super.key,
    required this.tarlalar,
    this.title = 'Hangi tarla için?',
    this.selectedTarlaId,
    this.requireLocation = false,
  });

  final List<Tarla> tarlalar;
  final String title;
  final String? selectedTarlaId;
  final bool requireLocation;

  /// Bottom sheet modalını açar ve seçilen [Tarla] nesnesini döndürür.
  static Future<Tarla?> show(
    BuildContext context, {
    required List<Tarla> tarlalar,
    String title = 'Hangi tarla için?',
    String? selectedTarlaId,
    bool requireLocation = false,
  }) {
    return showModalBottomSheet<Tarla>(
      context: context,
      builder: (ctx) => TarlaSecimBottomSheet(
        tarlalar: tarlalar,
        title: title,
        selectedTarlaId: selectedTarlaId,
        requireLocation: requireLocation,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Text(
              title,
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ),
          const Divider(height: 1),
          ...tarlalar.map(
            (t) {
              final bool hasLocation = t.latitude != null && t.longitude != null;
              final bool isEligible = !requireLocation || hasLocation;
              final bool isSelected = t.id == selectedTarlaId;

              String subtitleText =
                  '${t.cropType ?? 'Ürün bilgisi yok'} · ${t.size != null ? '${t.size!.toInt()} dönüm' : 'Alan bilinmiyor'}';
              if (requireLocation && !hasLocation) {
                subtitleText = '$subtitleText · Konum bilgisi yok';
              }

              return ListTile(
                leading: Icon(
                  Icons.terrain,
                  color: isEligible ? AppColors.primary : AppColors.textDisabled,
                ),
                title: Text(
                  t.name,
                  style: TextStyle(
                    color: isEligible ? AppColors.textPrimary : AppColors.textDisabled,
                  ),
                ),
                subtitle: Text(
                  subtitleText,
                  style: TextStyle(
                    color: isEligible ? AppColors.textSecondary : AppColors.textDisabled,
                  ),
                ),
                trailing: isSelected
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : (!isEligible
                        ? const Text(
                            'Konum yok',
                            style: TextStyle(
                              color: AppColors.textDisabled,
                              fontSize: 12,
                            ),
                          )
                        : null),
                enabled: isEligible,
                onTap: isEligible ? () => Navigator.pop(context, t) : null,
              );
            },
          ),
          const SizedBox(height: AppSpacing.sm),
        ],
      ),
    );
  }
}

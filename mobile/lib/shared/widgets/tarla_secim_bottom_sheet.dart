import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_spacing.dart';
import '../../models/tarla.dart';

/// Kullanıcının birden fazla tarlası olduğunda (2+) iş ekleme öncesinde
/// hangi tarla için işlem yapılacağını seçtiren ortak bottom sheet bileşeni.
class TarlaSecimBottomSheet extends StatelessWidget {
  const TarlaSecimBottomSheet({super.key, required this.tarlalar});

  final List<Tarla> tarlalar;

  /// Bottom sheet modalını açar ve seçilen [Tarla] nesnesini döndürür.
  static Future<Tarla?> show(
    BuildContext context, {
    required List<Tarla> tarlalar,
  }) {
    return showModalBottomSheet<Tarla>(
      context: context,
      builder: (ctx) => TarlaSecimBottomSheet(tarlalar: tarlalar),
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
              'Hangi tarla için?',
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ),
          const Divider(height: 1),
          ...tarlalar.map(
            (t) => ListTile(
              leading: const Icon(Icons.terrain, color: AppColors.primary),
              title: Text(t.name),
              subtitle: Text(
                '${t.cropType ?? 'Ürün bilgisi yok'} · ${t.size != null ? '${t.size!.toInt()} dönüm' : 'Alan bilinmiyor'}',
              ),
              onTap: () => Navigator.pop(context, t),
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';
import '../../../../app/theme/app_colors.dart';
import '../../../../app/theme/app_spacing.dart';
import '../../domain/models/market_item.dart';

/// Tek bir piyasa kaleminin (akaryakıt, gübre, mahsul veya döviz) güncel fiyatını,
/// değişim yönünü ve oranını gösteren yatay kart bileşeni.
class MarketItemCard extends StatelessWidget {
  const MarketItemCard({
    super.key,
    required this.item,
    this.onTap,
  });

  /// Görüntülenecek piyasa kalemi.
  final MarketItem item;

  /// Karta tıklandığında tetiklenecek opsiyonel geri çağırım.
  final VoidCallback? onTap;

  /// Ürünün simge anahtarına göre ilgili emojiyi belirler.
  static String iconForItem(String iconKey) {
    return switch (iconKey) {
      'fuel_diesel' => '⛽',
      'fuel_gasoline' => '🛢️',
      'fertilizer_urea' => '🧪',
      'fertilizer_dap' => '🌿',
      'crop_wheat' => '🌾',
      'crop_corn' => '🌽',
      'fx_usd_try' => '💵',
      'fx_eur_try' => '💶',
      _ => '📊',
    };
  }

  @override
  Widget build(BuildContext context) {
    final Color changeColor;
    final String changeSymbol;

    if (item.isUp) {
      // Fiyat artışı çiftçinin maliyeti açısından olumsuz kabul edilir
      changeColor = AppColors.error;
      changeSymbol = '▲';
    } else if (item.isDown) {
      // Fiyat düşüşü maliyet açısından olumlu kabul edilir
      changeColor = Colors.green.shade700;
      changeSymbol = '▼';
    } else {
      changeColor = AppColors.textSecondary;
      changeSymbol = '●';
    }

    return SizedBox(
      width: 170,
      height: 120,
      child: Card(
        clipBehavior: Clip.antiAlias,
        elevation: 1,
        color: AppColors.surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: Colors.grey.shade200,
            width: 1,
          ),
        ),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(16),
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: 12,
              vertical: 10,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                // ── Üst Satır: Emoji Simge & Ürün Adı ────────────────────────
                Row(
                  children: [
                    Text(
                      iconForItem(item.iconKey),
                      style: const TextStyle(fontSize: 18),
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Expanded(
                      child: Text(
                        item.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ],
                ),

                // ── Orta Satır: Dinamik Animasyonlu Birim Fiyat ─────────────
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 300),
                  transitionBuilder: (child, animation) {
                    return FadeTransition(opacity: animation, child: child);
                  },
                  child: Text(
                    item.formattedPrice,
                    key: ValueKey('${item.code}_${item.price}'),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),

                // ── Alt Satır: Günlük Değişim Rozeti ─────────────────────────
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 6,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    color: changeColor.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        changeSymbol,
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.bold,
                          color: changeColor,
                        ),
                      ),
                      const SizedBox(width: 3),
                      Text(
                        item.formattedChange,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: changeColor,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

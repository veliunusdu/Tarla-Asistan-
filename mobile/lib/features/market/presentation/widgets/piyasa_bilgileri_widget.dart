import 'package:flutter/material.dart';
import '../../../../app/theme/app_colors.dart';
import '../../../../app/theme/app_spacing.dart';
import '../../../../shared/widgets/app_empty_view.dart';
import '../../../../shared/widgets/app_error_view.dart';
import '../../../../shared/widgets/app_loading_view.dart';
import '../../data/backend_market_repository.dart';
import '../../domain/models/market_category.dart';
import '../../domain/models/market_item.dart';
import 'market_item_card.dart';

/// Ana sayfaya gömülen, çevrimdışı öncelikli (stale-while-revalidate) piyasa bilgileri bölüm bileşeni.
class PiyasaBilgileriWidget extends StatefulWidget {
  const PiyasaBilgileriWidget({
    super.key,
    required this.marketRepository,
    this.onSeeAll,
    this.onRetry,
  });

  /// Piyasa verilerini sağlayan ve reaktif durum bildirimini yöneten backend repository.
  final BackendMarketRepository marketRepository;

  /// "Tümü" butonuna tıklandığında tetiklenecek geri çağırım.
  final VoidCallback? onSeeAll;

  /// Hata durumunda yeniden deneme için opsiyonel geri çağırım.
  final VoidCallback? onRetry;

  @override
  State<PiyasaBilgileriWidget> createState() => _PiyasaBilgileriWidgetState();
}

class _PiyasaBilgileriWidgetState extends State<PiyasaBilgileriWidget> {
  MarketCategory _selectedCategory = MarketCategory.all;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<MarketDataState>(
      valueListenable: widget.marketRepository.stateNotifier,
      builder: (context, state, _) {
        return switch (state) {
          MarketDataLoading(:final cachedItems) when cachedItems.isEmpty => Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _buildHeader(context, null),
                const SizedBox(height: AppSpacing.sm),
                const SizedBox(
                  height: 140,
                  child: AppLoadingView(message: 'Piyasa verileri yükleniyor…'),
                ),
              ],
            ),
          MarketDataLoading(:final cachedItems) => _buildSection(
              context,
              items: cachedItems,
              isStale: false,
              isError: false,
              lastUpdated: null,
            ),
          MarketDataLoaded(:final items, :final lastUpdated)
              when items.isEmpty =>
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _buildHeader(context, lastUpdated),
                const SizedBox(height: AppSpacing.sm),
                const AppEmptyView(
                  icon: Icons.storefront_outlined,
                  title: 'Piyasa Verisi Bulunamadı',
                  description: 'Şu anda gösterilecek piyasa fiyatı bulunmuyor.',
                ),
              ],
            ),
          MarketDataLoaded(:final items, :final isStale, :final lastUpdated) =>
            _buildSection(
              context,
              items: items,
              isStale: isStale,
              isError: false,
              lastUpdated: lastUpdated,
            ),
          MarketDataError(:final cachedItems, :final message)
              when cachedItems.isEmpty =>
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _buildHeader(context, null),
                const SizedBox(height: AppSpacing.sm),
                AppErrorView(
                  title: 'Piyasa Verileri Alınamadı',
                  description: message,
                  onRetry: widget.onRetry ??
                      () => widget.marketRepository.refreshMarketData(),
                ),
              ],
            ),
          MarketDataError(:final cachedItems, :final isStale) => _buildSection(
              context,
              items: cachedItems,
              isStale: isStale,
              isError: true,
              lastUpdated: null,
            ),
        };
      },
    );
  }

  /// Başlık, kategori filtreleme çipleri ve yatay kart listesini bir arada sunar.
  Widget _buildSection(
    BuildContext context, {
    required List<MarketItem> items,
    required bool isStale,
    required bool isError,
    required DateTime? lastUpdated,
  }) {
    final theme = Theme.of(context);
    final filteredItems = _filterItems(items);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // ── Başlık & "Tümü" Butonu ──────────────────────────────────────────
        _buildHeader(context, lastUpdated),
        const SizedBox(height: AppSpacing.xs),

        // ── Bayat veya Hata Uyarısı Rozeti ──────────────────────────────────
        if (isError) ...[
          _buildWarningBanner(
            message: 'Veri güncellenemedi, önbellek gösteriliyor',
            icon: Icons.cloud_off_outlined,
          ),
          const SizedBox(height: AppSpacing.xs),
        ] else if (isStale) ...[
          _buildWarningBanner(
            message: 'Veriler güncel olmayabilir (çevrimdışı önbellek)',
            icon: Icons.history_toggle_off,
          ),
          const SizedBox(height: AppSpacing.xs),
        ],

        // ── Kategori Filtreleme Çipleri ─────────────────────────────────────
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: MarketCategory.values.map((category) {
              final isSelected = _selectedCategory == category;
              return Padding(
                padding: const EdgeInsets.only(right: AppSpacing.xs),
                child: ChoiceChip(
                  label: Text(category.displayName),
                  selected: isSelected,
                  selectedColor: theme.colorScheme.primary.withValues(alpha: 0.15),
                  labelStyle: TextStyle(
                    color: isSelected
                        ? theme.colorScheme.primary
                        : AppColors.textSecondary,
                    fontWeight:
                        isSelected ? FontWeight.bold : FontWeight.w500,
                    fontSize: 13,
                  ),
                  onSelected: (selected) {
                    if (selected) {
                      setState(() => _selectedCategory = category);
                    }
                  },
                ),
              );
            }).toList(),
          ),
        ),
        const SizedBox(height: AppSpacing.sm),

        // ── Yatay Fiyat Kartları Listesi ────────────────────────────────────
        if (filteredItems.isEmpty)
          const SizedBox(
            height: 120,
            child: Center(
              child: Text(
                'Bu kategoride veri bulunamadı.',
                style: TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 13,
                ),
              ),
            ),
          )
        else
          SizedBox(
            height: 120,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              physics: const BouncingScrollPhysics(),
              itemCount: filteredItems.length,
              separatorBuilder: (_, _) =>
                  const SizedBox(width: AppSpacing.sm),
              itemBuilder: (context, index) {
                return MarketItemCard(
                  item: filteredItems[index],
                  onTap: widget.onSeeAll,
                );
              },
            ),
          ),
      ],
    );
  }

  /// Bölüm başlığı ve son güncellenme süresi.
  Widget _buildHeader(BuildContext context, DateTime? lastUpdated) {
    final theme = Theme.of(context);

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Piyasa Bilgileri',
              style: theme.textTheme.titleMedium,
            ),
            if (lastUpdated != null) ...[
              const SizedBox(width: AppSpacing.xs),
              Text(
                '• ${_formatTimeAgo(lastUpdated)}',
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ],
        ),
        if (widget.onSeeAll != null)
          TextButton(
            onPressed: widget.onSeeAll,
            child: const Text('Tümü'),
          ),
      ],
    );
  }

  /// Ağ hatası veya bayat önbellek durumunda gösterilen hafif bilgilendirme bandı.
  Widget _buildWarningBanner({
    required String message,
    required IconData icon,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 6,
      ),
      decoration: BoxDecoration(
        color: Colors.amber.shade50,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: Colors.amber.shade300),
      ),
      child: Row(
        children: [
          Icon(icon, size: 16, color: Colors.amber.shade900),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                fontSize: 12,
                color: Colors.amber.shade900,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  /// Seçili kategoriye göre yerel filtreleme yapar.
  List<MarketItem> _filterItems(List<MarketItem> items) {
    if (_selectedCategory == MarketCategory.all) {
      return items;
    }
    return items.where((i) => i.category == _selectedCategory).toList();
  }

  /// Tarih nesnesini Türkçe göreceli zamana dönüştürür (örn: "5 dk önce", "2 saat önce").
  static String _formatTimeAgo(DateTime date) {
    final now = DateTime.now();
    final localDate = date.isUtc ? date.toLocal() : date;
    final diff = now.difference(localDate);

    if (diff.inSeconds < 60) {
      return 'Az önce';
    } else if (diff.inMinutes < 60) {
      return '${diff.inMinutes} dk önce';
    } else if (diff.inHours < 24) {
      return '${diff.inHours} saat önce';
    } else if (diff.inDays < 7) {
      return '${diff.inDays} gün önce';
    } else {
      return '${localDate.day}.${localDate.month}.${localDate.year}';
    }
  }
}

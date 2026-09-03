import 'package:flutter/material.dart';

import '../../../app/theme/app_colors.dart';
import '../../../app/theme/app_spacing.dart';
import '../../fields/data/tarla_repository.dart';
import '../data/case_repository.dart';
import '../domain/models/case_category.dart';
import '../domain/models/case_status.dart';
import '../domain/models/case_summary.dart';
import 'sorun_bildir_ekrani.dart';
import 'vaka_detay_ekrani.dart';

enum CaseFilterType {
  all,
  active,
  closed,
}

class VakaListesiEkrani extends StatefulWidget {
  const VakaListesiEkrani({
    super.key,
    required this.caseRepository,
    required this.tarlaRepository,
    this.farmId,
    this.farmName,
    this.detailBuilder,
  });

  final CaseRepository caseRepository;
  final TarlaRepository tarlaRepository;
  final String? farmId;
  final String? farmName;
  final Widget Function(BuildContext context, CaseSummary caseItem)? detailBuilder;

  @override
  State<VakaListesiEkrani> createState() => _VakaListesiEkraniState();
}

class _VakaListesiEkraniState extends State<VakaListesiEkrani> {
  CaseFilterType _selectedFilter = CaseFilterType.all;
  List<CaseSummary> _cases = [];
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _loadCases();
  }

  Future<void> _loadCases() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final results = await widget.caseRepository.getCases(
        farmId: widget.farmId,
      );
      if (!mounted) return;
      setState(() {
        _cases = results;
        _isLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.toString();
        _isLoading = false;
      });
    }
  }

  List<CaseSummary> get _filteredCases {
    return _cases.where((item) {
      return switch (_selectedFilter) {
        CaseFilterType.all => true,
        CaseFilterType.active => item.status != CaseStatus.closed,
        CaseFilterType.closed => item.status == CaseStatus.closed,
      };
    }).toList();
  }

  Color _getStatusColor(CaseStatus status) => switch (status) {
        CaseStatus.waitingFarmer => AppColors.error,
        CaseStatus.inReview => AppColors.warning,
        CaseStatus.open => AppColors.primary,
        CaseStatus.answered => AppColors.success,
        CaseStatus.closed => Colors.grey,
      };

  IconData _getCategoryIcon(CaseCategory category) => switch (category) {
        CaseCategory.disease => Icons.coronavirus_outlined,
        CaseCategory.pest => Icons.pest_control_outlined,
        CaseCategory.irrigation => Icons.water_drop_outlined,
        CaseCategory.nutrition => Icons.eco_outlined,
        CaseCategory.weather => Icons.cloud_outlined,
        CaseCategory.other => Icons.help_outline,
      };

  String _formatDate(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');
    final year = date.year.toString();
    return '$day.$month.$year';
  }

  Future<void> _navigateToDetail(CaseSummary item) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) =>
            widget.detailBuilder?.call(context, item) ??
            VakaDetayEkrani(
              caseId: item.id,
              caseRepository: widget.caseRepository,
            ),
      ),
    );
    if (mounted) {
      _loadCases();
    }
  }

  Future<void> _navigateToSorunBildir() async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => SorunBildirEkrani(
          caseRepository: widget.caseRepository,
          tarlaRepository: widget.tarlaRepository,
          initialTarlaId: widget.farmId,
        ),
      ),
    );
    if (mounted) {
      _loadCases();
    }
  }

  Widget _buildContent() {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline, size: 48, color: AppColors.error),
              const SizedBox(height: AppSpacing.sm),
              const Text(
                'Bildirimler yüklenemedi',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                _errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              ElevatedButton(
                onPressed: _loadCases,
                child: const Text('Tekrar Dene'),
              ),
            ],
          ),
        ),
      );
    }

    if (_cases.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                Icons.assignment_outlined,
                size: 64,
                color: Colors.grey.shade400,
              ),
              const SizedBox(height: AppSpacing.md),
              const Text(
                'Kayıtlı bir sorun bildiriminiz bulunmuyor.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Tarlanızı etkileyen bir problem olduğunda sağ alttaki butondan ziraat mühendisine danışabilirsiniz.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 13,
                  color: Colors.grey.shade600,
                ),
              ),
            ],
          ),
        ),
      );
    }

    final filtered = _filteredCases;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.md,
            AppSpacing.sm,
            AppSpacing.md,
            AppSpacing.sm,
          ),
          child: SizedBox(
            width: double.infinity,
            child: SegmentedButton<CaseFilterType>(
              segments: const [
                ButtonSegment<CaseFilterType>(
                  value: CaseFilterType.all,
                  label: Text('Tümü'),
                ),
                ButtonSegment<CaseFilterType>(
                  value: CaseFilterType.active,
                  label: Text('Aktifler'),
                ),
                ButtonSegment<CaseFilterType>(
                  value: CaseFilterType.closed,
                  label: Text('Çözülenler'),
                ),
              ],
              selected: {_selectedFilter},
              onSelectionChanged: (selection) {
                setState(() {
                  _selectedFilter = selection.first;
                });
              },
            ),
          ),
        ),
        Expanded(
          child: filtered.isEmpty
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.lg),
                    child: Text(
                      'Bu filtrede bildirim bulunmuyor.',
                      style: TextStyle(color: Colors.grey.shade600),
                    ),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadCases,
                  child: ListView.builder(
                    padding: const EdgeInsets.fromLTRB(
                      AppSpacing.md,
                      0,
                      AppSpacing.md,
                      80,
                    ),
                    itemCount: filtered.length,
                    itemBuilder: (context, index) {
                      final item = filtered[index];
                      return _buildCaseCard(item);
                    },
                  ),
                ),
        ),
      ],
    );
  }

  Widget _buildCaseCard(CaseSummary item) {
    final statusColor = _getStatusColor(item.status);

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: Colors.grey.shade300),
      ),
      elevation: 0,
      color: AppColors.surface,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => _navigateToDetail(item),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  // Category badge
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withAlpha(20),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          _getCategoryIcon(item.category),
                          size: 14,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          item.category.displayName,
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.primary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  // Farm name chip
                  Flexible(
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.grey.shade100,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        item.farmName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  // Status badge
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: statusColor.withAlpha(25),
                      borderRadius: BorderRadius.circular(6),
                      border: Border.all(color: statusColor.withAlpha(80)),
                    ),
                    child: Text(
                      item.status.displayName,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                        color: statusColor,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              // Title
              Text(
                item.title,
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  const Icon(
                    Icons.chat_bubble_outline,
                    size: 14,
                    color: AppColors.textSecondary,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    '${item.messageCount} mesaj',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  if (item.mediaCount > 0) ...[
                    const SizedBox(width: 12),
                    const Icon(
                      Icons.image_outlined,
                      size: 14,
                      color: AppColors.textSecondary,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      '${item.mediaCount} görsel',
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                  const Spacer(),
                  const Icon(
                    Icons.access_time,
                    size: 14,
                    color: AppColors.textDisabled,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    _formatDate(item.updatedAt),
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textDisabled,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final titleText =
        (widget.farmName != null && widget.farmName!.trim().isNotEmpty)
            ? '${widget.farmName!.trim()} Bildirimleri'
            : 'Sorun Bildirimlerim';

    return Scaffold(
      appBar: AppBar(
        title: Text(titleText),
      ),
      body: _buildContent(),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _navigateToSorunBildir,
        icon: const Icon(Icons.add),
        label: const Text('Sorun Bildir'),
      ),
    );
  }
}

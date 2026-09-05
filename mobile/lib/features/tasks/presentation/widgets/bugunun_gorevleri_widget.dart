import 'package:flutter/material.dart';
import 'package:uuid/uuid.dart';

import '../../../../app/theme/app_colors.dart';
import '../../../../app/theme/app_spacing.dart';
import '../../../../services/api_client.dart';
import '../../../../services/database_helper.dart';
import '../../../../shared/widgets/app_empty_view.dart';
import '../../../../shared/widgets/app_error_view.dart';
import '../../../../shared/widgets/app_loading_view.dart';
import '../../data/daily_task_repository.dart';
import '../../domain/farm_task.dart';
import '../../domain/pending_task_action.dart';
import '../../domain/task_enums.dart';
import '../../services/daily_task_notification_service.dart';

/// Ana sayfada günün en önemli işlerini (maksimum 3 adet) ve kritik hava
/// uyarılarını gösteren bileşen.
///
/// Backend `GET /farms/{farmId}/tasks` endpoint'inden dönen verileri görüntüler.
/// Görevleri backend'in sağladığı sırada tutar; yeniden sıralamaz veya filtrelemez.
class BugununGorevleriWidget extends StatefulWidget {
  const BugununGorevleriWidget({
    super.key,
    required this.dailyTaskRepository,
    required this.farmId,
    this.tarlaAdi,
    this.canSelectFarm = false,
    this.onFarmTap,
    this.onTarlaEkle,
    this.refreshNotifier,
    this.dailyTaskNotificationService,
  });

  /// Günlük görev verilerini sağlayan repository.
  final DailyTaskRepository dailyTaskRepository;

  /// Bildirim ve kritik hava uyarılarını yöneten servis.
  final DailyTaskNotificationService? dailyTaskNotificationService;

  /// Görevlerin yükleneceği tarla kimliği.
  final String? farmId;

  /// Görevlerin ait olduğu tarlanın adı.
  final String? tarlaAdi;

  /// Birden fazla tarla varsa tarlanın seçilebilir olup olmadığı.
  final bool canSelectFarm;

  /// Tarla değiştirme isteği tıklandığında tetiklenen callback.
  final VoidCallback? onFarmTap;

  /// Hiç tarla yoksa 'Tarla Ekle' butonu callback'i.
  final VoidCallback? onTarlaEkle;

  /// Dışarıdan tetiklenen yenileme dinleyicisi.
  final ValueNotifier<int>? refreshNotifier;

  @override
  State<BugununGorevleriWidget> createState() => _BugununGorevleriWidgetState();
}

class _BugununGorevleriWidgetState extends State<BugununGorevleriWidget> {
  bool _isLoading = false;
  Object? _error;
  DailyTaskList? _taskList;
  final Set<String> _expandedReasonTaskIds = <String>{};
  final Set<String> _processingTaskIds = <String>{};

  @override
  void initState() {
    super.initState();
    widget.refreshNotifier?.addListener(_handleRefreshSignal);
    _fetchTasks();
  }

  @override
  void didUpdateWidget(covariant BugununGorevleriWidget oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.refreshNotifier != widget.refreshNotifier) {
      oldWidget.refreshNotifier?.removeListener(_handleRefreshSignal);
      widget.refreshNotifier?.addListener(_handleRefreshSignal);
    }

    if (oldWidget.farmId != widget.farmId ||
        oldWidget.dailyTaskRepository != widget.dailyTaskRepository) {
      _expandedReasonTaskIds.clear();
      _processingTaskIds.clear();
      setState(() {
        _taskList = null;
        _error = null;
      });
      _fetchTasks();
    }
  }

  @override
  void dispose() {
    widget.refreshNotifier?.removeListener(_handleRefreshSignal);
    super.dispose();
  }

  void _handleRefreshSignal() {
    if (!mounted) return;
    _fetchTasks();
  }

  Future<void> _fetchTasks() async {
    final farmId = widget.farmId;
    if (farmId == null || farmId.isEmpty) {
      if (mounted) {
        setState(() {
          _taskList = null;
          _isLoading = false;
          _error = null;
        });
      }
      return;
    }

    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      try {
        final syncResult = await widget.dailyTaskRepository.syncPendingTaskActions(farmId: farmId);
        if (syncResult.conflictCount > 0 && mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Bekleyen bir veya daha fazla işlem sunucudaki güncel durum ile çakıştı ve kaldırıldı.'),
              duration: Duration(seconds: 4),
            ),
          );
        }
      } catch (e) {
        debugPrint('BugununGorevleriWidget: sync before fetch error: $e');
      }

      final list = await widget.dailyTaskRepository.getDailyTasks(farmId);
      if (!mounted || widget.farmId != farmId) return;
      setState(() {
        _taskList = list;
        _isLoading = false;
      });

      widget.dailyTaskNotificationService?.evaluateAndNotifyCriticalAlerts(
        taskList: list,
        farmId: farmId,
        farmName: widget.tarlaAdi,
      );
    } catch (e) {
      if (!mounted || widget.farmId != farmId) return;
      setState(() {
        _error = e;
        _isLoading = false;
      });
    }
  }

  void _toggleReason(String taskId) {
    setState(() {
      if (_expandedReasonTaskIds.contains(taskId)) {
        _expandedReasonTaskIds.remove(taskId);
      } else {
        _expandedReasonTaskIds.add(taskId);
      }
    });
  }

  Future<void> _handleCompleteTask(FarmTask task) async {
    final currentFarmId = widget.farmId;
    if (currentFarmId == null || currentFarmId.isEmpty) return;
    if (task.hasPendingAction) return;
    if (_processingTaskIds.contains(task.id)) return;

    final isOffline = _taskList?.isFromCache ?? false;

    setState(() {
      _processingTaskIds.add(task.id);
    });

    try {
      if (isOffline) {
        final action = PendingTaskAction(
          id: const Uuid().v4(),
          farmId: currentFarmId,
          taskId: task.id,
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.now().toUtc(),
          userId: DatabaseHelper.instance.currentUserId,
        );
        await widget.dailyTaskRepository.enqueueTaskAction(action);

        if (!mounted || widget.farmId != currentFarmId) return;

        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('İşlem kaydedildi. İnternete bağlanıldığında senkronize edilecek.'),
            duration: Duration(seconds: 3),
          ),
        );

        await _fetchTasks();
        return;
      }

      await widget.dailyTaskRepository.completeTask(
        farmId: currentFarmId,
        taskId: task.id,
      );

      if (!mounted || widget.farmId != currentFarmId) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Görev tamamlandı.'),
          duration: Duration(seconds: 3),
        ),
      );

      await _fetchTasks();
    } catch (e) {
      if (!mounted || widget.farmId != currentFarmId) return;

      if (e is ApiException && e.statusCode == 409) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Bu görev daha önce güncellenmiş. Liste yenileniyor.'),
            duration: Duration(seconds: 4),
          ),
        );
        await _fetchTasks();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('İşlem gerçekleştirilemedi. Lütfen tekrar deneyin.'),
            duration: Duration(seconds: 4),
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _processingTaskIds.remove(task.id);
        });
      }
    }
  }

  Future<void> _showNotAppliedSheet(FarmTask task) async {
    if (task.hasPendingAction) return;
    if (_processingTaskIds.contains(task.id)) return;

    final selectedReason = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (sheetContext) => _NotAppliedReasonSheet(taskTitle: task.title),
    );

    if (selectedReason == null) {
      return;
    }

    final trimmedReason = selectedReason.trim();
    if (trimmedReason.isEmpty) {
      return;
    }

    await _handleNotAppliedTask(task, trimmedReason);
  }

  Future<void> _handleNotAppliedTask(FarmTask task, String reason) async {
    final currentFarmId = widget.farmId;
    if (currentFarmId == null || currentFarmId.isEmpty) return;
    if (task.hasPendingAction) return;
    if (_processingTaskIds.contains(task.id)) return;

    final isOffline = _taskList?.isFromCache ?? false;

    setState(() {
      _processingTaskIds.add(task.id);
    });

    try {
      if (isOffline) {
        final action = PendingTaskAction(
          id: const Uuid().v4(),
          farmId: currentFarmId,
          taskId: task.id,
          actionType: TaskActionType.notApplied,
          reason: reason,
          createdAtUtc: DateTime.now().toUtc(),
          userId: DatabaseHelper.instance.currentUserId,
        );
        await widget.dailyTaskRepository.enqueueTaskAction(action);

        if (!mounted || widget.farmId != currentFarmId) return;

        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Görev uygulanmadı olarak kaydedildi. İnternete bağlanıldığında senkronize edilecek.'),
            duration: Duration(seconds: 3),
          ),
        );

        await _fetchTasks();
        return;
      }

      await widget.dailyTaskRepository.markTaskNotApplied(
        farmId: currentFarmId,
        taskId: task.id,
        reason: reason,
      );

      if (!mounted || widget.farmId != currentFarmId) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Görev uygulanmadı olarak kaydedildi.'),
          duration: Duration(seconds: 3),
        ),
      );

      await _fetchTasks();
    } catch (e) {
      if (!mounted || widget.farmId != currentFarmId) return;

      if (e is ApiException && e.statusCode == 409) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Bu görev daha önce güncellenmiş. Liste yenileniyor.'),
            duration: Duration(seconds: 4),
          ),
        );
        await _fetchTasks();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('İşlem gerçekleştirilemedi. Lütfen tekrar deneyin.'),
            duration: Duration(seconds: 4),
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _processingTaskIds.remove(task.id);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _buildHeader(context),
        const SizedBox(height: AppSpacing.sm),
        _buildContent(theme),
      ],
    );
  }

  Widget _buildHeader(BuildContext context) {
    final theme = Theme.of(context);
    final tarlaAdi = widget.tarlaAdi;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          'Bugünün İşleri',
          style: theme.textTheme.titleMedium,
        ),
        if (tarlaAdi != null)
          InkWell(
            onTap: widget.canSelectFarm ? widget.onFarmTap : null,
            borderRadius: BorderRadius.circular(12),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    tarlaAdi,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: widget.canSelectFarm
                          ? theme.colorScheme.primary
                          : AppColors.textSecondary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  if (widget.canSelectFarm) ...[
                    const SizedBox(width: 2),
                    Icon(
                      Icons.arrow_drop_down,
                      size: 18,
                      color: theme.colorScheme.primary,
                    ),
                  ],
                ],
              ),
            ),
          ),
      ],
    );
  }

  Widget _buildContent(ThemeData theme) {
    if (widget.farmId == null || widget.farmId!.isEmpty) {
      return AppEmptyView(
        icon: Icons.terrain,
        title: 'Henüz tarla eklenmedi.',
        description: 'Bugünün işlerini görmek için tarla ekleyin.',
        actionLabel: widget.onTarlaEkle != null ? 'Tarla Ekle' : null,
        onAction: widget.onTarlaEkle,
      );
    }

    if (_isLoading) {
      return const AppLoadingView(message: 'Bugünün işleri yükleniyor…');
    }

    if (_error != null) {
      return AppErrorView(
        title: 'Bugünün işleri alınamadı',
        description: 'Lütfen internet bağlantınızı kontrol edip tekrar deneyin.',
        retryLabel: 'Tekrar Dene',
        onRetry: _fetchTasks,
      );
    }

    final taskList = _taskList;
    if (taskList == null || taskList.isEmpty) {
      return const AppEmptyView(
        icon: Icons.check_circle_outline,
        title: 'Bugün için önemli bir iş görünmüyor.',
        description: 'Tarlanız için acil bir görev bulunmamaktadır.',
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (taskList.isFromCache) ...[
          _buildOfflineBanner(theme, taskList),
        ],

        // ── Kritik hava uyarıları (varsa ayrı gösterilir) ──────────────────
        if (taskList.criticalWeatherAlerts.isNotEmpty) ...[
          _buildCriticalWeatherAlerts(theme, taskList.criticalWeatherAlerts),
          const SizedBox(height: AppSpacing.md),
        ],

        // ── Normal günlük görevler (maksimum 3 adet, backend sırasıyla) ────
        ...taskList.items.asMap().entries.map((entry) {
          final index = entry.key;
          final task = entry.value;
          return _buildTaskCard(theme, task, order: index + 1);
        }),

        if (taskList.isFromCache) ...[
          const SizedBox(height: AppSpacing.xs),
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                const Icon(
                  Icons.cloud_queue,
                  size: 16,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    'Çevrimdışı kaydedilen işlemler internete bağlanıldığında senkronize edilir.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }

  static const List<String> _trAylar = [
    'Ocak',
    'Şubat',
    'Mart',
    'Nisan',
    'Mayıs',
    'Haziran',
    'Temmuz',
    'Ağustos',
    'Eylül',
    'Ekim',
    'Kasım',
    'Aralık',
  ];

  static String _formatTarih(DateTime dt) {
    final local = dt.toLocal();
    return '${local.day} ${_trAylar[local.month - 1]}';
  }

  static String _formatSaat(DateTime dt) {
    final local = dt.toLocal();
    final h = local.hour.toString().padLeft(2, '0');
    final m = local.minute.toString().padLeft(2, '0');
    return '$h:$m';
  }

  Widget _buildOfflineBanner(ThemeData theme, DailyTaskList taskList) {
    final now = DateTime.now();
    final taskDate = taskList.date.toLocal();
    final isToday = now.year == taskDate.year &&
        now.month == taskDate.month &&
        now.day == taskDate.day;

    final String bannerText = isToday
        ? (taskList.cachedAt != null
            ? 'Çevrimdışı • Son güncelleme ${_formatSaat(taskList.cachedAt!)}'
            : 'Çevrimdışı • Son kayıtlı veriler')
        : 'Çevrimdışı — bu görevler ${_formatTarih(taskList.date)} tarihinden.';

    return Container(
      key: const Key('tasks_offline_banner'),
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.7),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
          color: theme.colorScheme.outlineVariant.withValues(alpha: 0.5),
        ),
      ),
      child: Row(
        children: [
          Icon(
            Icons.cloud_off_outlined,
            size: 18,
            color: theme.colorScheme.onSurfaceVariant,
          ),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Text(
              bannerText,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          InkWell(
            key: const Key('tasks_offline_retry_button'),
            onTap: _isLoading ? null : _fetchTasks,
            borderRadius: BorderRadius.circular(4),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    Icons.refresh,
                    size: 14,
                    color: theme.colorScheme.primary,
                  ),
                  const SizedBox(width: 2),
                  Text(
                    'Yenile',
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: theme.colorScheme.primary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCriticalWeatherAlerts(ThemeData theme, List<FarmTask> alerts) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            const Icon(
              Icons.warning_amber_rounded,
              color: AppColors.error,
              size: 20,
            ),
            const SizedBox(width: AppSpacing.xs),
            Text(
              'Kritik Uyarı',
              style: theme.textTheme.titleSmall?.copyWith(
                color: AppColors.error,
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.xs),
        ...alerts.map((alert) => _buildWeatherAlertCard(theme, alert)),
      ],
    );
  }

  Widget _buildWeatherAlertCard(ThemeData theme, FarmTask alert) {
    final hasReason = alert.reason.trim().isNotEmpty;
    final isExpanded = _expandedReasonTaskIds.contains(alert.id);
    final isOffline = _taskList?.isFromCache ?? false;

    return Card(
      color: AppColors.error.withValues(alpha: 0.08),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: AppColors.error.withValues(alpha: 0.3),
        ),
      ),
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(
                  Icons.thunderstorm_outlined,
                  color: AppColors.error,
                  size: 22,
                ),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    alert.title,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.error,
                    ),
                  ),
                ),
                if (isOffline) ...[
                  const SizedBox(width: AppSpacing.xs),
                  Container(
                    key: Key('weather_alert_offline_badge_${alert.id}'),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.error.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      'Çevrimdışı kayıt',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: AppColors.error,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ],
            ),
            if (hasReason) ...[
              const SizedBox(height: AppSpacing.xs),
              InkWell(
                onTap: () => _toggleReason(alert.id),
                borderRadius: BorderRadius.circular(4),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        'Neden?',
                        style: theme.textTheme.labelMedium?.copyWith(
                          color: AppColors.error,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      Icon(
                        isExpanded
                            ? Icons.keyboard_arrow_up
                            : Icons.keyboard_arrow_down,
                        size: 16,
                        color: AppColors.error,
                      ),
                    ],
                  ),
                ),
              ),
              if (isExpanded) ...[
                const SizedBox(height: AppSpacing.xs),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.8),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    alert.reason,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildTaskCard(ThemeData theme, FarmTask task, {required int order}) {
    final hasReason = task.reason.trim().isNotEmpty;
    final isExpanded = _expandedReasonTaskIds.contains(task.id);

    return Card(
      elevation: 0.5,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: theme.colorScheme.outlineVariant.withValues(alpha: 0.4),
        ),
      ),
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // ── Üst satır: Sıra numarası + Başlık + Öncelik badge'i ───────
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '$order. ',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.primary,
                  ),
                ),
                Expanded(
                  child: Text(
                    task.title,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                _buildPriorityBadge(theme, task.priority),
              ],
            ),

            // ── Uzman değerlendirmesi önerisi ────────────────────────────
            if (task.expertReviewRecommended) ...[
              const SizedBox(height: AppSpacing.xs),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(
                    Icons.verified_outlined,
                    size: 15,
                    color: AppColors.secondary,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'Uzman görüşü öneriliyor',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.secondary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ],

            // ── Neden? etkileşimi (sadece reason doluysa gösterilir) ──────
            if (hasReason) ...[
              const SizedBox(height: AppSpacing.xs),
              InkWell(
                onTap: () => _toggleReason(task.id),
                borderRadius: BorderRadius.circular(4),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        'Neden?',
                        style: theme.textTheme.labelMedium?.copyWith(
                          color: theme.colorScheme.primary,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      Icon(
                        isExpanded
                            ? Icons.keyboard_arrow_up
                            : Icons.keyboard_arrow_down,
                        size: 16,
                        color: theme.colorScheme.primary,
                      ),
                    ],
                  ),
                ),
              ),
              if (isExpanded) ...[
                const SizedBox(height: AppSpacing.xs),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.surfaceContainerHighest
                        .withValues(alpha: 0.4),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    task.reason,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ],

            // ── Aksiyon Butonları (Yaptım / Uygulamadım) ─────────────────
            const SizedBox(height: AppSpacing.sm),
            _buildActionButtons(theme, task),
          ],
        ),
      ),
    );
  }

  Widget _buildActionButtons(ThemeData theme, FarmTask task) {
    if (task.hasPendingAction) {
      return _buildPendingActionState(theme, task);
    }

    final isProcessing = _processingTaskIds.contains(task.id);

    return Row(
      children: [
        Expanded(
          child: OutlinedButton.icon(
            key: Key('task_not_applied_${task.id}'),
            onPressed: isProcessing ? null : () => _showNotAppliedSheet(task),
            icon: const Icon(Icons.close, size: 16),
            label: const Text('Uygulamadım'),
            style: OutlinedButton.styleFrom(
              visualDensity: VisualDensity.compact,
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
              side: BorderSide(
                color: isProcessing
                    ? theme.colorScheme.outlineVariant.withValues(alpha: 0.3)
                    : theme.colorScheme.outlineVariant,
              ),
              foregroundColor: AppColors.textSecondary,
            ),
          ),
        ),
        const SizedBox(width: AppSpacing.sm),
        Expanded(
          child: FilledButton.icon(
            key: Key('task_complete_${task.id}'),
            onPressed: isProcessing ? null : () => _handleCompleteTask(task),
            icon: isProcessing
                ? const SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.check, size: 16),
            label: const Text('Yaptım'),
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.primary,
              visualDensity: VisualDensity.compact,
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildPendingActionState(ThemeData theme, FarmTask task) {
    final action = task.pendingAction;
    final isComplete = action?.actionType == TaskActionType.complete;

    final bgColor = isComplete
        ? AppColors.primary.withValues(alpha: 0.1)
        : AppColors.warning.withValues(alpha: 0.1);
    final borderColor = isComplete
        ? AppColors.primary.withValues(alpha: 0.3)
        : AppColors.warning.withValues(alpha: 0.3);
    final iconColor = isComplete ? AppColors.primary : AppColors.warning;
    final icon = isComplete ? Icons.check_circle_outline : Icons.pause_circle_outline;
    final label = isComplete
        ? '✓ Yaptım — Senkronizasyon bekliyor'
        : 'Uygulanmadı — Senkronizasyon bekliyor';

    return Container(
      key: Key('task_pending_badge_${task.id}'),
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: borderColor),
      ),
      child: Row(
        children: [
          Icon(
            icon,
            size: 16,
            color: iconColor,
          ),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(
                color: iconColor,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPriorityBadge(ThemeData theme, TaskPriority priority) {
    final (label, backgroundColor, textColor) = switch (priority) {
      TaskPriority.critical => (
          'Kritik',
          AppColors.error.withValues(alpha: 0.12),
          AppColors.error,
        ),
      TaskPriority.high => (
          'Yüksek',
          AppColors.warning.withValues(alpha: 0.15),
          AppColors.warning,
        ),
      TaskPriority.medium => (
          'Orta',
          AppColors.secondary.withValues(alpha: 0.15),
          AppColors.secondary,
        ),
      TaskPriority.low => (
          'Düşük',
          AppColors.textDisabled.withValues(alpha: 0.2),
          AppColors.textSecondary,
        ),
      TaskPriority.unknown => (
          'Belirsiz',
          AppColors.textDisabled.withValues(alpha: 0.1),
          AppColors.textDisabled,
        ),
    };

    return Semantics(
      label: '$label öncelik',
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          label,
          style: theme.textTheme.labelSmall?.copyWith(
            color: textColor,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    );
  }
}

/// Uygulamama nedeni seçimini sağlayan bottom sheet.
class _NotAppliedReasonSheet extends StatefulWidget {
  const _NotAppliedReasonSheet({required this.taskTitle});

  final String taskTitle;

  static const List<String> presetReasons = [
    'Hava şartları uygun değildi',
    'Zaman yetersizliği',
    'Malzeme veya ekipman eksik',
    'Gerekli görülmedi',
    'Diğer',
  ];

  @override
  State<_NotAppliedReasonSheet> createState() => _NotAppliedReasonSheetState();
}

class _NotAppliedReasonSheetState extends State<_NotAppliedReasonSheet> {
  String? _selectedReason;
  final TextEditingController _customController = TextEditingController();

  @override
  void dispose() {
    _customController.dispose();
    super.dispose();
  }

  String? get _effectiveReason {
    if (_selectedReason == 'Diğer') {
      final custom = _customController.text.trim();
      return custom.isNotEmpty ? custom : null;
    }
    final selected = _selectedReason?.trim();
    return (selected != null && selected.isNotEmpty) ? selected : null;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isDiger = _selectedReason == 'Diğer';
    final effective = _effectiveReason;
    final canSave = effective != null && effective.isNotEmpty;

    return Container(
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: EdgeInsets.only(
        left: AppSpacing.md,
        right: AppSpacing.md,
        top: AppSpacing.sm,
        bottom: MediaQuery.of(context).viewInsets.bottom + AppSpacing.md,
      ),
      child: SafeArea(
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Center(
                child: Container(
                  width: 36,
                  height: 4,
                  margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.outlineVariant,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              Text(
                'Görevi Uygulamama Nedeni',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                widget.taskTitle,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: AppColors.textSecondary,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: AppSpacing.md),
              Wrap(
                spacing: AppSpacing.xs,
                runSpacing: AppSpacing.xs,
                children: _NotAppliedReasonSheet.presetReasons.map((preset) {
                  final isSelected = _selectedReason == preset;
                  return ChoiceChip(
                    key: Key('reason_chip_$preset'),
                    label: Text(preset),
                    selected: isSelected,
                    onSelected: (selected) {
                      setState(() {
                        _selectedReason = selected ? preset : null;
                      });
                    },
                    selectedColor: AppColors.primary.withValues(alpha: 0.15),
                    labelStyle: TextStyle(
                      color: isSelected ? AppColors.primary : AppColors.textPrimary,
                      fontWeight: isSelected ? FontWeight.w600 : FontWeight.normal,
                    ),
                  );
                }).toList(),
              ),
              if (isDiger) ...[
                const SizedBox(height: AppSpacing.sm),
                TextField(
                  key: const Key('custom_reason_field'),
                  controller: _customController,
                  autofocus: true,
                  maxLines: 2,
                  maxLength: 500,
                  decoration: const InputDecoration(
                    hintText: 'Uygulamama nedenini yazın...',
                    border: OutlineInputBorder(),
                    isDense: true,
                    counterText: '',
                  ),
                  onChanged: (_) => setState(() {}),
                ),
              ],
              const SizedBox(height: AppSpacing.lg),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      key: const Key('cancel_not_applied_btn'),
                      onPressed: () => Navigator.of(context).pop(null),
                      child: const Text('Vazgeç'),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: FilledButton(
                      key: const Key('confirm_not_applied_btn'),
                      onPressed: canSave
                          ? () => Navigator.of(context).pop(effective)
                          : null,
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.primary,
                      ),
                      child: const Text('Kaydet'),
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
}
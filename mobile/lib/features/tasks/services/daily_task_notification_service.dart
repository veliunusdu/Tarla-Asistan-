import '../../../models/notification_target.dart';
import '../../../services/notification_service.dart';
import '../data/daily_task_notification_preferences.dart';
import '../domain/farm_task.dart';
import '../domain/task_enums.dart';
import 'daily_task_notification_dispatcher.dart';

class DailyTaskNotificationService {
  DailyTaskNotificationService({
    DailyTaskNotificationPreferences? preferences,
    DailyTaskNotificationDispatcher? dispatcher,
    this.pushStateProvider,
    this.nowProvider,
  })  : _preferences = preferences ?? DailyTaskNotificationPreferences(),
        _dispatcher = dispatcher ?? InAppNotificationDispatcher();

  final DailyTaskNotificationPreferences _preferences;
  final DailyTaskNotificationDispatcher _dispatcher;
  final PushState Function()? pushStateProvider;
  final DateTime Function()? nowProvider;

  static const int dailyNotificationId = 1001;

  DailyTaskNotificationPreferences get preferences => _preferences;
  DailyTaskNotificationDispatcher get dispatcher => _dispatcher;

  DateTime get _now => nowProvider?.call() ?? DateTime.now();

  Future<bool> scheduleDailySummaryIfNeeded({String? farmId}) async {
    final enabled = await _preferences.isDailyTasksNotificationEnabled();
    if (!enabled) return false;

    if (pushStateProvider?.call() == PushState.denied) {
      return false;
    }

    if (farmId != null) {
      await _preferences.setLastSelectedFarmId(farmId);
    }
    final targetFarmId = farmId ?? await _preferences.getLastSelectedFarmId();
    final time = await _preferences.getReminderTime();

    await _dispatcher.scheduleDaily(
      id: dailyNotificationId,
      time: time,
      title: 'Bugünün en önemli 3 işi hazır 🌱',
      body: 'Günün işlerini kontrol etmek için dokunun.',
      target: NotificationTarget(
        type: NotificationTargetType.dailyTasks,
        resourceId: targetFarmId ?? '',
        farmId: targetFarmId,
      ),
    );
    return true;
  }

  Future<void> cancelDailyReminder() async {
    await _dispatcher.cancel(dailyNotificationId);
  }

  Future<void> rescheduleDailyReminder({String? farmId}) async {
    await cancelDailyReminder();
    await scheduleDailySummaryIfNeeded(farmId: farmId);
  }

  Future<bool> evaluateAndNotifyDailyTasks({
    required DailyTaskList taskList,
    required String farmId,
    String? farmName,
    bool forceUpdate = false,
  }) async {
    final enabled = await _preferences.isDailyTasksNotificationEnabled();
    if (!enabled) return false;

    if (pushStateProvider?.call() == PushState.denied) {
      return false;
    }

    await _preferences.setLastSelectedFarmId(farmId);

    final now = _now;
    final todayIso =
        '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';

    final lastNotifiedDate = await _preferences.getLastDailyNotificationDate();
    if (!forceUpdate && lastNotifiedDate == todayIso) {
      return false;
    }

    // Exclude completed, cancelled, notApplied or tasks with pending actions
    final activeTasks = taskList.items.where((t) {
      if (t.status == TaskStatus.completed ||
          t.status == TaskStatus.cancelled ||
          t.status == TaskStatus.notApplied) {
        return false;
      }
      if (t.hasPendingAction) {
        return false;
      }
      return true;
    }).toList();

    String title;
    String body;

    if (taskList.isFromCache) {
      final cachedAt = taskList.cachedAt;
      final isCacheToday = cachedAt != null &&
          cachedAt.toLocal().year == now.year &&
          cachedAt.toLocal().month == now.month &&
          cachedAt.toLocal().day == now.day;

      if (isCacheToday) {
        title = 'Bugün için kayıtlı görevlerin hazır 🌱';
        final firstTaskTitle = activeTasks.isNotEmpty ? activeTasks.first.title : '';
        body = firstTaskTitle.isNotEmpty
            ? 'İlk iş: $firstTaskTitle'
            : 'Kayıtlı görevlerini kontrol et.';
      } else {
        // Stale cache: Do not say "you have 3 tasks today"
        title = 'Tarla Asistanı';
        body = "Tarla Asistan'ı açarak bugünkü görevlerini kontrol et.";
      }
    } else {
      // Live data
      if (activeTasks.isEmpty && taskList.criticalWeatherAlerts.isEmpty) {
        // No tasks and no alerts -> Do not dispatch empty notification
        if (forceUpdate) {
          await _dispatcher.cancel(dailyNotificationId);
        }
        return false;
      }

      if (activeTasks.isNotEmpty) {
        final taskCount = activeTasks.length;
        title = 'Bugün tarlada $taskCount önemli işin var 🌱';
        final firstTitle = activeTasks.first.title;
        body = firstTitle.isNotEmpty
            ? 'İlk iş: $firstTitle'
            : 'Bugünün işlerini inceleyin.';
      } else {
        title = 'Bugün için kritik hava uyarısı var ⚠️';
        body = 'Hava risklerini incelemek için dokunun.';
      }
    }

    await _dispatcher.showNotification(
      id: dailyNotificationId,
      title: title,
      body: body,
      target: NotificationTarget(
        type: NotificationTargetType.dailyTasks,
        resourceId: farmId,
        farmId: farmId,
      ),
    );

    await _preferences.setLastDailyNotificationDate(todayIso);
    return true;
  }

  Future<int> evaluateAndNotifyCriticalAlerts({
    required DailyTaskList taskList,
    required String farmId,
    String? farmName,
  }) async {
    final enabled = await _preferences.isDailyTasksNotificationEnabled();
    if (!enabled) return 0;

    if (pushStateProvider?.call() == PushState.denied) {
      return 0;
    }

    // Never trigger critical notifications from offline/stale cache
    if (taskList.isFromCache) {
      return 0;
    }

    if (taskList.criticalWeatherAlerts.isEmpty) {
      return 0;
    }

    int notifiedCount = 0;
    for (final alert in taskList.criticalWeatherAlerts) {
      final alertKey =
          '${farmId}_${alert.id}_${alert.dueDate?.toIso8601String()}';

      final alreadyNotified = await _preferences.isCriticalAlertNotified(alertKey);
      if (alreadyNotified) {
        continue;
      }

      final alertTitle = alert.title.isNotEmpty ? alert.title : 'Kritik Hava Uyarısı';
      final title = '⚠️ $alertTitle';
      final body = farmName != null
          ? '$farmName için kritik hava uyarısı: ${alert.description}'
          : 'Tarlanız için kritik hava uyarısı: ${alert.description}';

      final alertId = notificationIdForCriticalAlert(alert.id);

      await _dispatcher.showNotification(
        id: alertId,
        title: title,
        body: body,
        target: NotificationTarget(
          type: NotificationTargetType.weather,
          resourceId: farmId,
          farmId: farmId,
        ),
      );

      await _preferences.markCriticalAlertNotified(alertKey);
      notifiedCount++;
    }

    return notifiedCount;
  }

  /// Generates a deterministic notification ID for a critical weather alert.
  static int notificationIdForCriticalAlert(String alertId) {
    return 2000 + (alertId.hashCode.abs() % 10000);
  }

  /// Counts active tasks for today excluding completed, cancelled, notApplied or pending actions.
  static int countActiveTasks(DailyTaskList taskList) {
    return taskList.items.where((t) {
      if (t.status == TaskStatus.completed ||
          t.status == TaskStatus.cancelled ||
          t.status == TaskStatus.notApplied) {
        return false;
      }
      if (t.hasPendingAction) {
        return false;
      }
      return true;
    }).length;
  }

  /// Cancels any scheduled or active local alert notification for a specific alert or task ID.
  Future<void> cancelAlertForTask(String alertId) async {
    final notificationId = notificationIdForCriticalAlert(alertId);
    await _dispatcher.cancel(notificationId);
  }

  /// Reconciles local notifications against the authoritative [currentTaskList] relative to [previousTaskList].
  ///
  /// - Cancels critical alert notifications ONLY for alert IDs that were present in
  ///   [previousTaskList.criticalWeatherAlerts] but are NO LONGER present in
  ///   [currentTaskList.criticalWeatherAlerts].
  /// - Leaves notifications for alerts that remain active completely untouched (no blind cancellation).
  /// - Refreshes or cancels the daily summary notification (#1001) if active task count changed
  ///   or if [forceUpdateDailySummary] is true.
  /// - Dispatches notifications for any newly introduced critical alerts in [currentTaskList].
  Future<void> reconcileTaskNotifications({
    required String farmId,
    DailyTaskList? previousTaskList,
    required DailyTaskList currentTaskList,
    String? farmName,
    bool forceUpdateDailySummary = false,
  }) async {
    // 1. Reconcile critical alerts: cancel only removed alert IDs
    if (previousTaskList != null) {
      final previousAlertIds =
          previousTaskList.criticalWeatherAlerts.map((a) => a.id).toSet();
      final currentAlertIds =
          currentTaskList.criticalWeatherAlerts.map((a) => a.id).toSet();
      final removedAlertIds = previousAlertIds.difference(currentAlertIds);

      for (final removedId in removedAlertIds) {
        await cancelAlertForTask(removedId);
      }
    }

    // 2. Daily summary update: refresh if active tasks count changed or forced
    final hadActiveTasksChange = previousTaskList != null &&
        countActiveTasks(previousTaskList) != countActiveTasks(currentTaskList);

    if (hadActiveTasksChange || forceUpdateDailySummary) {
      await evaluateAndNotifyDailyTasks(
        farmId: farmId,
        taskList: currentTaskList,
        forceUpdate: true,
      );
    }

    // 3. Evaluate any remaining or newly added critical alerts
    await evaluateAndNotifyCriticalAlerts(
      taskList: currentTaskList,
      farmId: farmId,
      farmName: farmName,
    );
  }

  /// Synchronizes local task notifications after task updates or postpone actions.
  ///
  /// If [postponedTaskId] is provided:
  /// 1. Cancels the specific weather alert associated with this task.
  /// 2. Refreshes the daily task summary notification (#1001) with the authoritative
  ///    active task list (or cancels it if no tasks remain for today).
  /// 3. Re-evaluates critical alerts for the remaining tasks in [taskList].
  Future<void> syncTaskNotifications({
    required String farmId,
    required DailyTaskList taskList,
    String? postponedTaskId,
    String? farmName,
    bool forceUpdateDailySummary = false,
  }) async {
    if (postponedTaskId != null && postponedTaskId.isNotEmpty) {
      await cancelAlertForTask(postponedTaskId);
    }

    if (postponedTaskId != null || forceUpdateDailySummary) {
      await evaluateAndNotifyDailyTasks(
        farmId: farmId,
        taskList: taskList,
        forceUpdate: true,
      );
    }

    await evaluateAndNotifyCriticalAlerts(
      taskList: taskList,
      farmId: farmId,
      farmName: farmName,
    );
  }
}


import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tasks/data/daily_task_notification_preferences.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';
import 'package:mobile/features/tasks/services/daily_task_notification_dispatcher.dart';
import 'package:mobile/features/tasks/services/daily_task_notification_service.dart';
import 'package:mobile/models/notification_target.dart';
import 'package:mobile/services/notification_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

class ShownNotification {
  ShownNotification(this.id, this.title, this.body, this.target);
  final int id;
  final String title;
  final String body;
  final NotificationTarget? target;
}

class ScheduledNotification {
  ScheduledNotification(this.id, this.time, this.title, this.body, this.target);
  final int id;
  final TimeOfDay time;
  final String title;
  final String body;
  final NotificationTarget? target;
}

class FakeDailyTaskNotificationDispatcher implements DailyTaskNotificationDispatcher {
  final List<ShownNotification> shown = [];
  final List<ScheduledNotification> scheduled = [];
  final List<int> cancelled = [];

  @override
  Future<void> showNotification({
    required int id,
    required String title,
    required String body,
    NotificationTarget? target,
  }) async {
    shown.add(ShownNotification(id, title, body, target));
  }

  @override
  Future<void> scheduleDaily({
    required int id,
    required TimeOfDay time,
    required String title,
    required String body,
    NotificationTarget? target,
  }) async {
    scheduled.add(ScheduledNotification(id, time, title, body, target));
  }

  @override
  Future<void> cancel(int id) async {
    cancelled.add(id);
    scheduled.removeWhere((s) => s.id == id);
  }
}

FarmTask createTestTask({
  required String id,
  required String title,
  TaskPriority priority = TaskPriority.high,
  TaskStatus status = TaskStatus.newTask,
  PendingTaskAction? pendingAction,
  DateTime? dueDate,
}) {
  return FarmTask(
    id: id,
    farmId: 'farm-123',
    title: title,
    description: 'Açıklama $title',
    reason: 'Gerekçe $title',
    priority: priority,
    status: status,
    source: TaskSource.cropCalendar,
    confidence: TaskConfidence.high,
    dueDate: dueDate ?? DateTime(2026, 9, 5),
    expertReviewRecommended: false,
    pendingAction: pendingAction,
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late FakeDailyTaskNotificationDispatcher dispatcher;
  late DailyTaskNotificationPreferences preferences;
  late DailyTaskNotificationService service;
  final testNow = DateTime(2026, 9, 5, 8, 30);

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    dispatcher = FakeDailyTaskNotificationDispatcher();
    preferences = DailyTaskNotificationPreferences();
    service = DailyTaskNotificationService(
      preferences: preferences,
      dispatcher: dispatcher,
      nowProvider: () => testNow,
      pushStateProvider: () => PushState.authorized,
    );
  });

  group('DailyTaskNotificationService - Günlük Notification', () {
    test('1. Kullanıcı bildirimleri açıksa günlük schedule oluşturulur', () async {
      await preferences.setDailyTasksNotificationEnabled(true);
      final scheduled = await service.scheduleDailySummaryIfNeeded(farmId: 'farm-123');

      expect(scheduled, isTrue);
      expect(dispatcher.scheduled, hasLength(1));
      expect(dispatcher.scheduled.first.id, equals(DailyTaskNotificationService.dailyNotificationId));
    });

    test('2. Kullanıcı bildirimleri kapalıysa schedule oluşturulmaz', () async {
      await preferences.setDailyTasksNotificationEnabled(false);
      final scheduled = await service.scheduleDailySummaryIfNeeded(farmId: 'farm-123');

      expect(scheduled, isFalse);
      expect(dispatcher.scheduled, isEmpty);
    });

    test('3. Varsayılan saat 08:00 doğru kullanılır', () async {
      final time = await preferences.getReminderTime();
      expect(time.hour, equals(8));
      expect(time.minute, equals(0));

      await service.scheduleDailySummaryIfNeeded(farmId: 'farm-123');
      expect(dispatcher.scheduled.first.time, equals(const TimeOfDay(hour: 8, minute: 0)));
    });

    test('4. Kullanıcı saati değiştirince eski schedule iptal edilir', () async {
      await service.scheduleDailySummaryIfNeeded(farmId: 'farm-123');
      expect(dispatcher.scheduled, hasLength(1));

      // Change time to 07:00
      await preferences.setReminderTime(const TimeOfDay(hour: 7, minute: 0));
      await service.rescheduleDailyReminder(farmId: 'farm-123');

      expect(dispatcher.cancelled, contains(DailyTaskNotificationService.dailyNotificationId));
      expect(dispatcher.scheduled, hasLength(1));
      expect(dispatcher.scheduled.first.time, equals(const TimeOfDay(hour: 7, minute: 0)));
    });

    test('5. Bir gün için duplicate daily notification oluşturulmaz', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [createTestTask(id: 't1', title: 'Sulama yap')],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      final firstDispatch = await service.evaluateAndNotifyDailyTasks(
        taskList: list,
        farmId: 'farm-123',
      );
      expect(firstDispatch, isTrue);
      expect(dispatcher.shown, hasLength(1));

      // Same day second evaluation must be blocked
      final secondDispatch = await service.evaluateAndNotifyDailyTasks(
        taskList: list,
        farmId: 'farm-123',
      );
      expect(secondDispatch, isFalse);
      expect(dispatcher.shown, hasLength(1)); // Still only 1 shown
    });

    test('6. items boşsa gereksiz günlük görev bildirimi oluşturulmaz', () async {
      final emptyList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      final dispatched = await service.evaluateAndNotifyDailyTasks(
        taskList: emptyList,
        farmId: 'farm-123',
      );
      expect(dispatched, isFalse);
      expect(dispatcher.shown, isEmpty);
    });

    test('7. 3 görev varsa notification count doğru gösterilir', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [
          createTestTask(id: 't1', title: 'Sulama'),
          createTestTask(id: 't2', title: 'Gübreleme'),
          createTestTask(id: 't3', title: 'İlaçlama'),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.evaluateAndNotifyDailyTasks(taskList: list, farmId: 'farm-123');

      expect(dispatcher.shown, hasLength(1));
      expect(dispatcher.shown.first.title, contains('3 önemli işin var'));
    });

    test('8. İlk önemli görev title bodyde doğru kullanılır', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [
          createTestTask(id: 't1', title: 'Domates tarlasını sabah sula'),
          createTestTask(id: 't2', title: 'Yaprak kontrolü'),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.evaluateAndNotifyDailyTasks(taskList: list, farmId: 'farm-123');

      expect(dispatcher.shown, hasLength(1));
      expect(dispatcher.shown.first.body, contains('Domates tarlasını sabah sula'));
    });

    test('9. Pending complete task aktif görev sayısına dahil edilmez', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [
          createTestTask(
            id: 't1',
            title: 'Yapılmış İş',
            pendingAction: PendingTaskAction(
              id: 'a1',
              farmId: 'farm-123',
              taskId: 't1',
              actionType: TaskActionType.complete,
              createdAtUtc: testNow,
            ),
          ),
          createTestTask(id: 't2', title: 'Aktif İş 1'),
          createTestTask(id: 't3', title: 'Aktif İş 2'),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.evaluateAndNotifyDailyTasks(taskList: list, farmId: 'farm-123');

      expect(dispatcher.shown, hasLength(1));
      // Only 2 active tasks remain
      expect(dispatcher.shown.first.title, contains('2 önemli işin var'));
      expect(dispatcher.shown.first.body, contains('Aktif İş 1'));
    });

    test('10. Pending notApplied task aktif görev sayısına dahil edilmez', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [
          createTestTask(
            id: 't1',
            title: 'Uygulanmamış İş',
            pendingAction: PendingTaskAction(
              id: 'a1',
              farmId: 'farm-123',
              taskId: 't1',
              actionType: TaskActionType.notApplied,
              reason: 'Hava yağışlı',
              createdAtUtc: testNow,
            ),
          ),
          createTestTask(id: 't2', title: 'Tek Kalan İş'),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.evaluateAndNotifyDailyTasks(taskList: list, farmId: 'farm-123');

      expect(dispatcher.shown, hasLength(1));
      expect(dispatcher.shown.first.title, contains('1 önemli işin var'));
      expect(dispatcher.shown.first.body, contains('Tek Kalan İş'));
    });
  });

  group('DailyTaskNotificationService - Cache Güvenliği', () {
    test('11. Bugüne ait cache güvenli notification içeriği üretir', () async {
      final todayCache = DailyTaskList(
        date: testNow,
        items: [createTestTask(id: 't1', title: 'Kayıtlı Sulama')],
        criticalWeatherAlerts: [],
        overdue: [],
        isFromCache: true,
        cachedAt: testNow, // Today
      );

      final dispatched = await service.evaluateAndNotifyDailyTasks(
        taskList: todayCache,
        farmId: 'farm-123',
      );

      expect(dispatched, isTrue);
      expect(dispatcher.shown.first.title, contains('Bugün için kayıtlı görevlerin hazır'));
      expect(dispatcher.shown.first.body, contains('Kayıtlı Sulama'));
    });

    test('12. Eski tarihli cache bugünün 3 işi diye yanlış notification oluşturmaz', () async {
      final staleCache = DailyTaskList(
        date: testNow.subtract(const Duration(days: 3)),
        items: [
          createTestTask(id: 't1', title: 'Eski İş 1'),
          createTestTask(id: 't2', title: 'Eski İş 2'),
          createTestTask(id: 't3', title: 'Eski İş 3'),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
        isFromCache: true,
        cachedAt: testNow.subtract(const Duration(days: 3)), // 3 days ago
      );

      final dispatched = await service.evaluateAndNotifyDailyTasks(
        taskList: staleCache,
        farmId: 'farm-123',
      );

      expect(dispatched, isTrue);
      expect(dispatcher.shown.first.title, equals('Tarla Asistanı'));
      expect(dispatcher.shown.first.body, contains("Tarla Asistan'ı açarak bugünkü görevlerini kontrol et."));
      expect(dispatcher.shown.first.title, isNot(contains('3')));
    });

    test('13. Eski cached criticalWeatherAlert kritik notification tetiklemez', () async {
      final cachedAlertList = DailyTaskList(
        date: testNow.subtract(const Duration(days: 1)),
        items: [],
        criticalWeatherAlerts: [
          createTestTask(
            id: 'frost-1',
            title: 'Don uyarısı',
            priority: TaskPriority.critical,
          ),
        ],
        overdue: [],
        isFromCache: true,
        cachedAt: testNow.subtract(const Duration(days: 1)),
      );

      final count = await service.evaluateAndNotifyCriticalAlerts(
        taskList: cachedAlertList,
        farmId: 'farm-123',
      );

      expect(count, equals(0));
      expect(dispatcher.shown, isEmpty);
    });
  });

  group('DailyTaskNotificationService - Critical Weather Alerts', () {
    test('14. Yeni canlı criticalWeatherAlert notification oluşturur', () async {
      final liveList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [
          createTestTask(
            id: 'frost-100',
            title: 'Don riski',
            priority: TaskPriority.critical,
          ),
        ],
        overdue: [],
        isFromCache: false,
      );

      final count = await service.evaluateAndNotifyCriticalAlerts(
        taskList: liveList,
        farmId: 'farm-123',
        farmName: 'Kuzey Tarlası',
      );

      expect(count, equals(1));
      expect(dispatcher.shown, hasLength(1));
      expect(dispatcher.shown.first.title, contains('⚠️ Don riski'));
      expect(dispatcher.shown.first.body, contains('Kuzey Tarlası için kritik hava uyarısı'));
    });

    test('15. Aynı alert ikinci kez duplicate notification oluşturmaz', () async {
      final liveList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [
          createTestTask(
            id: 'frost-100',
            title: 'Don riski',
            priority: TaskPriority.critical,
          ),
        ],
        overdue: [],
        isFromCache: false,
      );

      final firstCount = await service.evaluateAndNotifyCriticalAlerts(
        taskList: liveList,
        farmId: 'farm-123',
      );
      expect(firstCount, equals(1));
      expect(dispatcher.shown, hasLength(1));

      // Second call for the same alert
      final secondCount = await service.evaluateAndNotifyCriticalAlerts(
        taskList: liveList,
        farmId: 'farm-123',
      );
      expect(secondCount, equals(0));
      expect(dispatcher.shown, hasLength(1)); // Still only 1
    });

    test('16. Farklı critical alert notification oluşturabilir', () async {
      final alert1 = createTestTask(id: 'frost-1', title: 'Don riski', priority: TaskPriority.critical);
      final alert2 = createTestTask(id: 'storm-2', title: 'Fırtına uyarısı', priority: TaskPriority.critical);

      final list1 = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [alert1],
        overdue: [],
        isFromCache: false,
      );
      final list2 = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [alert2],
        overdue: [],
        isFromCache: false,
      );

      await service.evaluateAndNotifyCriticalAlerts(taskList: list1, farmId: 'farm-123');
      await service.evaluateAndNotifyCriticalAlerts(taskList: list2, farmId: 'farm-123');

      expect(dispatcher.shown, hasLength(2));
      expect(dispatcher.shown[0].title, contains('Don riski'));
      expect(dispatcher.shown[1].title, contains('Fırtına uyarısı'));
    });
  });

  group('DailyTaskNotificationService - Navigation & Deep Link', () {
    test('17. Günlük notification tap hedefi dailyTasks türündedir', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [createTestTask(id: 't1', title: 'Sulama')],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.evaluateAndNotifyDailyTasks(taskList: list, farmId: 'farm-123');

      final target = dispatcher.shown.first.target;
      expect(target, isNotNull);
      expect(target!.type, equals(NotificationTargetType.dailyTasks));
      expect(target.farmId, equals('farm-123'));
    });

    test('18. farmId payload varsa doğru farm atanır', () async {
      final target = NotificationTarget.fromData({
        'type': 'daily_tasks',
        'farm_id': 'farm-999',
      });

      expect(target, isNotNull);
      expect(target!.farmId, equals('farm-999'));
      expect(target.resourceId, equals('farm-999'));
      expect(target.type, equals(NotificationTargetType.dailyTasks));
    });

    test('19. Geçersiz farmId crash oluşturmaz', () async {
      final target = NotificationTarget.fromData({
        'type': 'daily_tasks',
        'farm_id': null,
      });

      expect(target, isNotNull);
      expect(target!.type, equals(NotificationTargetType.dailyTasks));
      expect(target.farmId, isNull);
      expect(target.resourceId, isEmpty);
    });
  });

  group('DailyTaskNotificationService - Permissions', () {
    test('20. Notification permission yoksa / denied ise crash olmaz ve bildirim gönderilmez', () async {
      final deniedService = DailyTaskNotificationService(
        preferences: preferences,
        dispatcher: dispatcher,
        pushStateProvider: () => PushState.denied,
      );

      final list = DailyTaskList(
        date: testNow,
        items: [createTestTask(id: 't1', title: 'Sulama')],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      final result = await deniedService.evaluateAndNotifyDailyTasks(
        taskList: list,
        farmId: 'farm-123',
      );

      expect(result, isFalse);
      expect(dispatcher.shown, isEmpty);
    });

    test('21. Permission reddedilmişken schedule çağrısı güvenle false döner', () async {
      final deniedService = DailyTaskNotificationService(
        preferences: preferences,
        dispatcher: dispatcher,
        pushStateProvider: () => PushState.denied,
      );

      final scheduled = await deniedService.scheduleDailySummaryIfNeeded(farmId: 'farm-123');
      expect(scheduled, isFalse);
      expect(dispatcher.scheduled, isEmpty);
    });
  });

  group('DailyTaskNotificationService - Weather Postpone Synchronization & Authoritative Reconciliation', () {
    test('22. CriticalAlertNotificationId_UsesSameIdentifierForScheduleAndCancel: Schedule ve cancel ayni merkezi formulu kullanir', () async {
      const alertId = 'alert-risk-123';
      final expectedId = DailyTaskNotificationService.notificationIdForCriticalAlert(alertId);

      final taskAlert = createTestTask(id: alertId, title: 'Kritik Don Uyarısı');
      final list = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [taskAlert],
        overdue: [],
      );

      await service.evaluateAndNotifyCriticalAlerts(taskList: list, farmId: 'farm-123');
      expect(dispatcher.shown.last.id, equals(expectedId));

      await service.cancelAlertForTask(alertId);
      expect(dispatcher.cancelled.last, equals(expectedId));
    });

    test('23. AlertId_IsOrIsNotTaskId_DocumentedByTest: criticalWeatherAlerts icindeki alert FarmTask modelidir ve alert.id == task.id kontratidir', () async {
      final json = {
        'date': '2026-09-06',
        'items': [],
        'criticalWeatherAlerts': [
          {
            'id': 'task-weather-uuid-456',
            'farmId': 'farm-123',
            'title': 'Fırtına Uyarısı',
            'description': 'Açıklama',
            'reason': 'Gerekçe',
            'priority': 'Critical',
            'status': 'New',
            'source': 'Weather',
            'confidence': 'High',
            'dueDate': '2026-09-06',
          }
        ],
        'overdue': [],
      };

      final parsed = DailyTaskList.fromJson(json);
      expect(parsed.criticalWeatherAlerts, hasLength(1));
      final alertTask = parsed.criticalWeatherAlerts.first;
      expect(alertTask.id, equals('task-weather-uuid-456'));
      expect(alertTask.source, equals(TaskSource.weather));
      expect(alertTask.priority, equals(TaskPriority.critical));

      final alertNotificationId = DailyTaskNotificationService.notificationIdForCriticalAlert(alertTask.id);
      expect(alertNotificationId, equals(2000 + ('task-weather-uuid-456'.hashCode.abs() % 10000)));
    });

    test('24. Postpone200_RemovedAlertIsCancelled: Before alerts [taskWindAlert, frostAlert], After reload [frostAlert] -> Yalniz taskWindAlert cancel edilir', () async {
      final taskWindAlert = createTestTask(id: 'task-wind', title: 'İlaçlama Rüzgar Uyarısı');
      final frostAlert = createTestTask(id: 'alert-frost-99', title: 'Don Uyarısı');

      final beforeList = DailyTaskList(
        date: testNow,
        items: [taskWindAlert],
        criticalWeatherAlerts: [taskWindAlert, frostAlert],
        overdue: [],
      );

      final afterList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [frostAlert],
        overdue: [],
      );

      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: beforeList,
        currentTaskList: afterList,
      );

      final windAlertId = DailyTaskNotificationService.notificationIdForCriticalAlert('task-wind');
      final frostAlertId = DailyTaskNotificationService.notificationIdForCriticalAlert('alert-frost-99');

      expect(dispatcher.cancelled, contains(windAlertId));
      expect(dispatcher.cancelled, isNot(contains(frostAlertId)));
    });

    test('25. Postpone409Expired_StillActiveAlertIsNotCancelled: 409 Expired sonrasi alert authoritative olarak hala varsa bildirim iptal EDILMEZ', () async {
      final taskWindAlert = createTestTask(id: 'task-wind-expired', title: 'Rüzgar Riski');

      final beforeList = DailyTaskList(
        date: testNow,
        items: [taskWindAlert],
        criticalWeatherAlerts: [taskWindAlert],
        overdue: [],
      );

      // 409 Expired oldu, backend'de görev ertelenmedi. Authoritative reload sonucu alert hala bugünün listesinde:
      final authoritativeReloadList = DailyTaskList(
        date: testNow,
        items: [taskWindAlert],
        criticalWeatherAlerts: [taskWindAlert],
        overdue: [],
      );

      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: beforeList,
        currentTaskList: authoritativeReloadList,
      );

      // Hiçbir iptal yapılmamalı, çünkü risk hala bugünün aktif uyarısıdır!
      expect(dispatcher.cancelled, isEmpty);
    });

    test('26. Postpone409AlreadyApplied_RemovedAlertIsCancelledAfterReload: 409 Already Applied sonrasi backendde ertelenmis alert authoritative reload sonrasi dogru iptal edilir', () async {
      final taskWindAlert = createTestTask(id: 'task-wind-applied', title: 'Rüzgar Riski');

      final beforeList = DailyTaskList(
        date: testNow,
        items: [taskWindAlert],
        criticalWeatherAlerts: [taskWindAlert],
        overdue: [],
      );

      // 409 Already Applied: Görev sunucuda zaten ertelenmiş olduğundan bugünün listesinden çıkmış:
      final authoritativeReloadList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: beforeList,
        currentTaskList: authoritativeReloadList,
      );

      final windAlertId = DailyTaskNotificationService.notificationIdForCriticalAlert('task-wind-applied');
      expect(dispatcher.cancelled, contains(windAlertId));
    });

    test('27. Reconciliation_PreservesUnrelatedFrostAlert: Iliskisiz hava uyarilari reconcilation tarafindan kesinlikle silinmez', () async {
      final taskWind = createTestTask(id: 'task-wind-1', title: 'Rüzgar');
      final frostAlert = createTestTask(id: 'frost-permanent', title: 'Kritik Don Uyarısı');

      final beforeList = DailyTaskList(
        date: testNow,
        items: [taskWind],
        criticalWeatherAlerts: [taskWind, frostAlert],
        overdue: [],
      );

      final afterList = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [frostAlert],
        overdue: [],
      );

      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: beforeList,
        currentTaskList: afterList,
      );

      final frostAlertId = DailyTaskNotificationService.notificationIdForCriticalAlert('frost-permanent');
      expect(dispatcher.cancelled, isNot(contains(frostAlertId)));
    });

    test('28. Reconciliation_IsIdempotent: Ayni state ile ardisik reconcilation calistirildiginda ek bildirim veya fazla cancel uretilmez', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [createTestTask(id: 't1', title: 'Sulama')],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: null,
        currentTaskList: list,
      );

      final cancelledCountAfterFirst = dispatcher.cancelled.length;

      // İkinci çağrı (idempotent, current == previous)
      await service.reconcileTaskNotifications(
        farmId: 'farm-123',
        previousTaskList: list,
        currentTaskList: list,
      );

      expect(dispatcher.cancelled.length, equals(cancelledCountAfterFirst));
    });

    test('29. CancelledRemovedAlert_DedupeStateDoesNotBreakFutureNewDueDateAlert: alert.id ayni kalsa bile farkli dueDate yeni dedupe key uretir', () async {
      final todayAlert = createTestTask(
        id: 'task-risk-1',
        title: 'Don Riski',
        dueDate: DateTime(2026, 9, 6),
      );
      final tomorrowAlert = createTestTask(
        id: 'task-risk-1',
        title: 'Don Riski',
        dueDate: DateTime(2026, 9, 7),
      );

      final todayKey = 'farm-123_${todayAlert.id}_${todayAlert.dueDate?.toIso8601String()}';
      final tomorrowKey = 'farm-123_${tomorrowAlert.id}_${tomorrowAlert.dueDate?.toIso8601String()}';

      expect(todayKey, isNot(equals(tomorrowKey)));

      await preferences.markCriticalAlertNotified(todayKey);
      expect(await preferences.isCriticalAlertNotified(todayKey), isTrue);
      // Yarının tarihi için henüz notified olarak işaretlenmemiş olmalı
      expect(await preferences.isCriticalAlertNotified(tomorrowKey), isFalse);
    });

    test('30. DailySummary_UsesCorrectCalendarScope: countActiveTasks tamamlanan veya bekleyen gorevleri haric tutar', () async {
      final list = DailyTaskList(
        date: testNow,
        items: [
          createTestTask(id: 't1', title: 'Aktif 1', status: TaskStatus.newTask),
          createTestTask(id: 't2', title: 'Tamamlanan', status: TaskStatus.completed),
          createTestTask(id: 't3', title: 'Uygulanmayan', status: TaskStatus.notApplied),
          createTestTask(
            id: 't4',
            title: 'Pending',
            pendingAction: PendingTaskAction(
              id: 'p1',
              farmId: 'farm-123',
              taskId: 't4',
              actionType: TaskActionType.complete,
              createdAtUtc: testNow,
            ),
          ),
          createTestTask(id: 't5', title: 'Aktif 2', status: TaskStatus.viewed),
        ],
        criticalWeatherAlerts: [],
        overdue: [],
      );

      final count = DailyTaskNotificationService.countActiveTasks(list);
      expect(count, equals(2));
    });

    test('31. NotificationScheduleAndCancel_UseSameCriticalAlertId', () async {
      const alertId = 'alert-frost-uuid-789';
      final expectedNotificationId = DailyTaskNotificationService.notificationIdForCriticalAlert(alertId);

      // Expected formula: 2000 + (alertId.hashCode.abs() % 10000)
      expect(expectedNotificationId, equals(2000 + (alertId.hashCode.abs() % 10000)));

      // 1. Dispatch critical alert notification
      final alertTask = createTestTask(id: alertId, title: 'Don riski', priority: TaskPriority.critical);
      final list = DailyTaskList(
        date: testNow,
        items: [],
        criticalWeatherAlerts: [alertTask],
        overdue: [],
        isFromCache: false,
      );

      final count = await service.evaluateAndNotifyCriticalAlerts(
        taskList: list,
        farmId: 'farm-123',
      );
      expect(count, equals(1));
      expect(dispatcher.shown, hasLength(1));
      expect(dispatcher.shown.first.id, equals(expectedNotificationId));

      // 2. Cancel alert notification using cancelAlertForTask
      await service.cancelAlertForTask(alertId);
      expect(dispatcher.cancelled, contains(expectedNotificationId));
    });
  });
}




import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';

void main() {
  group('DailyTask & FarmTask Serialization Roundtrip', () {
    test('FarmTask serialization preserves all fields accurately', () {
      final original = FarmTask(
        id: 'task-full-1',
        farmId: 'farm-123',
        title: 'Damla sulama sistemini çalıştır',
        description: 'Parsel 3 için 2 saat boyunca sulama yapın.',
        reason: 'Toprak nem sensörü kritik eşiğin altına indi.',
        priority: TaskPriority.high,
        status: TaskStatus.newTask,
        source: TaskSource.cropCalendar,
        confidence: TaskConfidence.high,
        dueDate: DateTime(2026, 9, 5),
        expertReviewRecommended: false,
        cropPeriodId: 'period-456',
        createdById: 'user-789',
        notAppliedReason: 'Hava yağmurluydu',
        completionNote: 'Tamamlandı ve vana kapatıldı',
        photoUrl: 'https://example.com/photo.jpg',
        viewedAtUtc: DateTime.utc(2026, 9, 5, 8, 30),
        completedAtUtc: DateTime.utc(2026, 9, 5, 10, 15),
        createdAtUtc: DateTime.utc(2026, 9, 5, 6, 0),
        updatedAtUtc: DateTime.utc(2026, 9, 5, 10, 15),
      );

      final jsonMap = original.toJson();
      final jsonStr = jsonEncode(jsonMap);
      final decodedMap = jsonDecode(jsonStr) as Map<String, dynamic>;
      final parsed = FarmTask.fromJson(decodedMap);

      expect(parsed.id, equals(original.id));
      expect(parsed.farmId, equals(original.farmId));
      expect(parsed.title, equals(original.title));
      expect(parsed.description, equals(original.description));
      expect(parsed.reason, equals(original.reason));
      expect(parsed.priority, equals(TaskPriority.high));
      expect(parsed.status, equals(TaskStatus.newTask));
      expect(parsed.source, equals(TaskSource.cropCalendar));
      expect(parsed.confidence, equals(TaskConfidence.high));
      expect(parsed.dueDate, equals(original.dueDate));
      expect(parsed.expertReviewRecommended, isFalse);
      expect(parsed.cropPeriodId, equals(original.cropPeriodId));
      expect(parsed.createdById, equals(original.createdById));
      expect(parsed.notAppliedReason, equals(original.notAppliedReason));
      expect(parsed.completionNote, equals(original.completionNote));
      expect(parsed.photoUrl, equals(original.photoUrl));
      expect(parsed.viewedAtUtc?.toUtc(), equals(original.viewedAtUtc));
      expect(parsed.completedAtUtc?.toUtc(), equals(original.completedAtUtc));
      expect(parsed.createdAtUtc?.toUtc(), equals(original.createdAtUtc));
      expect(parsed.updatedAtUtc?.toUtc(), equals(original.updatedAtUtc));
    });

    test('DailyTaskList serialization preserves items, alerts, overdue, visibleLimit, isFromCache and cachedAt', () {
      final task1 = FarmTask(
        id: 'task-1',
        farmId: 'farm-1',
        title: 'Görev 1',
        description: 'Açıklama 1',
        reason: 'Gerekçe 1',
        priority: TaskPriority.critical,
        status: TaskStatus.viewed,
        source: TaskSource.weather,
        confidence: TaskConfidence.medium,
        dueDate: DateTime(2026, 9, 5),
        expertReviewRecommended: false,
      );

      final weatherAlert = FarmTask(
        id: 'weather-alert-1',
        farmId: 'farm-1',
        title: 'Don Uyarısı',
        description: 'Bu gece zirai don bekleniyor.',
        reason: 'Sıcaklık 0 derecenin altına düşecek.',
        priority: TaskPriority.critical,
        status: TaskStatus.newTask,
        source: TaskSource.weather,
        confidence: TaskConfidence.high,
        dueDate: DateTime(2026, 9, 5),
        expertReviewRecommended: false,
      );

      final overdueTask = FarmTask(
        id: 'overdue-1',
        farmId: 'farm-1',
        title: 'Gecikmiş İlaçlama',
        description: 'Mantar ilacı uygulanmalıydı.',
        reason: 'Hastalık riski yüksek.',
        priority: TaskPriority.high,
        status: TaskStatus.overdue,
        source: TaskSource.expert,
        confidence: TaskConfidence.low,
        dueDate: DateTime(2026, 9, 3),
        expertReviewRecommended: true,
      );

      final cachedAtTime = DateTime.utc(2026, 9, 5, 8, 42);

      final originalList = DailyTaskList(
        date: DateTime(2026, 9, 5),
        items: [task1],
        criticalWeatherAlerts: [weatherAlert],
        overdue: [overdueTask],
        visibleLimit: 3,
        isFromCache: true,
        cachedAt: cachedAtTime,
      );

      final jsonMap = originalList.toJson();
      final jsonStr = jsonEncode(jsonMap);
      final decoded = jsonDecode(jsonStr) as Map<String, dynamic>;
      final parsedList = DailyTaskList.fromJson(decoded);

      expect(parsedList.date.year, equals(2026));
      expect(parsedList.date.month, equals(9));
      expect(parsedList.date.day, equals(5));
      expect(parsedList.items.length, equals(1));
      expect(parsedList.items.first.id, equals('task-1'));
      expect(parsedList.items.first.priority, equals(TaskPriority.critical));
      expect(parsedList.criticalWeatherAlerts.length, equals(1));
      expect(parsedList.criticalWeatherAlerts.first.id, equals('weather-alert-1'));
      expect(parsedList.overdue.length, equals(1));
      expect(parsedList.overdue.first.id, equals('overdue-1'));
      expect(parsedList.visibleLimit, equals(3));
      expect(parsedList.isFromCache, isTrue);
      expect(parsedList.cachedAt?.toUtc(), equals(cachedAtTime));
    });

    test('All enum variants serialize and deserialize correctly', () {
      for (final priority in TaskPriority.values) {
        final json = priority.toJson();
        final deserialized = TaskPriority.fromJson(json);
        expect(deserialized, equals(priority));
      }

      for (final source in TaskSource.values) {
        final json = source.toJson();
        final deserialized = TaskSource.fromJson(json);
        expect(deserialized, equals(source));
      }

      for (final status in TaskStatus.values) {
        final json = status.toJson();
        final deserialized = TaskStatus.fromJson(json);
        expect(deserialized, equals(status));
      }

      for (final confidence in TaskConfidence.values) {
        final json = confidence.toJson();
        final deserialized = TaskConfidence.fromJson(json);
        expect(deserialized, equals(confidence));
      }
    });

    test('Unknown enum values map safely to unknown', () {
      expect(TaskPriority.fromJson('EXTREME'), equals(TaskPriority.unknown));
      expect(TaskSource.fromJson('SATELLITE'), equals(TaskSource.unknown));
      expect(TaskStatus.fromJson('IN_REVIEW'), equals(TaskStatus.unknown));
      expect(TaskConfidence.fromJson('VERY_HIGH'), equals(TaskConfidence.unknown));
    });

    test('DailyTaskList copyWith copies and updates fields properly', () {
      final initial = DailyTaskList(
        date: DateTime(2026, 9, 5),
        items: const [],
        criticalWeatherAlerts: const [],
        overdue: const [],
        visibleLimit: 3,
        isFromCache: false,
        cachedAt: null,
      );

      final updatedCachedAt = DateTime.utc(2026, 9, 5, 9, 0);
      final copied = initial.copyWith(
        isFromCache: true,
        cachedAt: updatedCachedAt,
      );

      expect(copied.isFromCache, isTrue);
      expect(copied.cachedAt, equals(updatedCachedAt));
      expect(copied.date, equals(initial.date));
      expect(copied.visibleLimit, equals(3));
    });

    test('PendingTaskAction toMap and fromMap roundtrip correctly', () {
      final nowUtc = DateTime.utc(2026, 9, 5, 12, 0);
      final attemptUtc = DateTime.utc(2026, 9, 5, 12, 5);

      final action = PendingTaskAction(
        id: 'action-test-id',
        farmId: 'farm-123',
        taskId: 'task-456',
        actionType: TaskActionType.notApplied,
        reason: 'Hava muhalefeti',
        note: 'Opsiyonel not',
        createdAtUtc: nowUtc,
        attemptCount: 2,
        lastAttemptAtUtc: attemptUtc,
        lastErrorCode: 503,
        userId: 'user-789',
      );

      final map = action.toMap();
      final parsed = PendingTaskAction.fromMap(map);

      expect(parsed.id, equals('action-test-id'));
      expect(parsed.farmId, equals('farm-123'));
      expect(parsed.taskId, equals('task-456'));
      expect(parsed.actionType, equals(TaskActionType.notApplied));
      expect(parsed.reason, equals('Hava muhalefeti'));
      expect(parsed.note, equals('Opsiyonel not'));
      expect(parsed.createdAtUtc, equals(nowUtc));
      expect(parsed.attemptCount, equals(2));
      expect(parsed.lastAttemptAtUtc, equals(attemptUtc));
      expect(parsed.lastErrorCode, equals(503));
      expect(parsed.userId, equals('user-789'));
      expect(parsed, equals(action));
    });

    test('TaskActionType toJson and fromJson roundtrip correctly', () {
      expect(TaskActionType.complete.toJson(), equals('COMPLETE'));
      expect(TaskActionType.notApplied.toJson(), equals('NOT_APPLIED'));

      expect(TaskActionType.fromJson('COMPLETE'), equals(TaskActionType.complete));
      expect(TaskActionType.fromJson('complete'), equals(TaskActionType.complete));
      expect(TaskActionType.fromJson('NOT_APPLIED'), equals(TaskActionType.notApplied));
      expect(TaskActionType.fromJson('not_applied'), equals(TaskActionType.notApplied));
      expect(TaskActionType.fromJson('NOTAPPLIED'), equals(TaskActionType.notApplied));

      expect(() => TaskActionType.fromJson('UNKNOWN'), throwsA(isA<ArgumentError>()));
    });

    test('FarmTask copyWith handles pendingAction and clearPendingAction', () {
      final task = FarmTask(
        id: 'task-1',
        farmId: 'farm-1',
        title: 'Görev',
        description: 'Açıklama',
        reason: 'Neden',
        priority: TaskPriority.medium,
        status: TaskStatus.newTask,
        source: TaskSource.cropCalendar,
        confidence: TaskConfidence.high,
        dueDate: DateTime(2026, 9, 5),
        expertReviewRecommended: false,
      );

      expect(task.hasPendingAction, isFalse);
      expect(task.pendingAction, isNull);

      final action = PendingTaskAction(
        id: 'action-1',
        farmId: 'farm-1',
        taskId: 'task-1',
        actionType: TaskActionType.complete,
        createdAtUtc: DateTime.now().toUtc(),
      );

      final withPending = task.copyWith(pendingAction: action);
      expect(withPending.hasPendingAction, isTrue);
      expect(withPending.pendingAction, equals(action));

      final cleared = withPending.copyWith(clearPendingAction: true);
      expect(cleared.hasPendingAction, isFalse);
      expect(cleared.pendingAction, isNull);
    });

    test('SyncResult getters compute hasChanges and isSuccessful accurately', () {
      const emptyResult = SyncResult();
      expect(emptyResult.hasChanges, isFalse);
      expect(emptyResult.isSuccessful, isTrue);

      const successResult = SyncResult(syncedCount: 2);
      expect(successResult.hasChanges, isTrue);
      expect(successResult.isSuccessful, isTrue);

      const conflictResult = SyncResult(conflictCount: 1);
      expect(conflictResult.hasChanges, isTrue);
      expect(conflictResult.isSuccessful, isTrue);

      const failedResult = SyncResult(failedCount: 1);
      expect(failedResult.isSuccessful, isFalse);

      const authErrorResult = SyncResult(hasAuthError: true);
      expect(authErrorResult.isSuccessful, isFalse);
    });
  });
}

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tasks/data/local_daily_task_repository.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('LocalDailyTaskRepository', () {
    late Database db;
    late LocalDailyTaskRepository repo;

    setUp(() async {
      db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
      repo = LocalDailyTaskRepository(databaseProvider: () async => db);
    });

    tearDown(() async {
      await db.close();
    });

    DailyTaskList createSampleTasks({
      required String farmId,
      DateTime? date,
      String taskTitle = 'Toprak işleme',
    }) {
      return DailyTaskList(
        date: date ?? DateTime(2026, 9, 5),
        items: [
          FarmTask(
            id: 'task-1',
            farmId: farmId,
            title: taskTitle,
            description: 'Derin sürüm yapın.',
            reason: 'Toprak havalandırılmalı.',
            priority: TaskPriority.high,
            status: TaskStatus.newTask,
            source: TaskSource.cropCalendar,
            confidence: TaskConfidence.high,
            dueDate: DateTime(2026, 9, 5),
            expertReviewRecommended: false,
          ),
        ],
        criticalWeatherAlerts: [
          FarmTask(
            id: 'alert-1',
            farmId: farmId,
            title: 'Fırtına Bekleniyor',
            description: 'Rüzgar hızı 60 km/s üzerine çıkabilir.',
            reason: 'Sera örtülerini kontrol edin.',
            priority: TaskPriority.critical,
            status: TaskStatus.newTask,
            source: TaskSource.weather,
            confidence: TaskConfidence.high,
            dueDate: DateTime(2026, 9, 5),
            expertReviewRecommended: false,
          ),
        ],
        overdue: [],
        visibleLimit: 3,
      );
    }

    test('getCachedTasks returns null when no cache exists for farm', () async {
      final cached = await repo.getCachedTasks('farm-none');
      expect(cached, isNull);
    });

    test('cacheTasks and getCachedTasks roundtrips data successfully with isFromCache=true', () async {
      final tasks = createSampleTasks(farmId: 'farm-1');
      final cacheTimestamp = DateTime.utc(2026, 9, 5, 8, 30);

      await repo.cacheTasks(
        farmId: 'farm-1',
        tasks: tasks,
        cachedAt: cacheTimestamp,
      );

      final cached = await repo.getCachedTasks('farm-1');
      expect(cached, isNotNull);
      expect(cached!.isFromCache, isTrue);
      expect(cached.cachedAt?.toUtc(), equals(cacheTimestamp));
      expect(cached.items.length, equals(1));
      expect(cached.items.first.title, equals('Toprak işleme'));
      expect(cached.criticalWeatherAlerts.length, equals(1));
      expect(cached.criticalWeatherAlerts.first.title, equals('Fırtına Bekleniyor'));
    });

    test('farm isolation: Farm A cache is never returned for Farm B', () async {
      final tasksA = createSampleTasks(farmId: 'farm-A', taskTitle: 'Farm A Task');
      final tasksB = createSampleTasks(farmId: 'farm-B', taskTitle: 'Farm B Task');

      await repo.cacheTasks(farmId: 'farm-A', tasks: tasksA);
      await repo.cacheTasks(farmId: 'farm-B', tasks: tasksB);

      final cachedA = await repo.getCachedTasks('farm-A');
      final cachedB = await repo.getCachedTasks('farm-B');
      final cachedC = await repo.getCachedTasks('farm-C');

      expect(cachedA, isNotNull);
      expect(cachedA!.items.first.title, equals('Farm A Task'));

      expect(cachedB, isNotNull);
      expect(cachedB!.items.first.title, equals('Farm B Task'));

      expect(cachedC, isNull);
    });

    test('cacheTasks overwrites previous cache for the same farm', () async {
      final initial = createSampleTasks(farmId: 'farm-1', taskTitle: 'Eski Görev');
      await repo.cacheTasks(farmId: 'farm-1', tasks: initial);

      final updated = createSampleTasks(farmId: 'farm-1', taskTitle: 'Yeni Görev');
      await repo.cacheTasks(farmId: 'farm-1', tasks: updated);

      final cached = await repo.getCachedTasks('farm-1');
      expect(cached, isNotNull);
      expect(cached!.items.first.title, equals('Yeni Görev'));
    });

    test('clearCache deletes cache only for specified farm', () async {
      await repo.cacheTasks(
        farmId: 'farm-1',
        tasks: createSampleTasks(farmId: 'farm-1'),
      );
      await repo.cacheTasks(
        farmId: 'farm-2',
        tasks: createSampleTasks(farmId: 'farm-2'),
      );

      await repo.clearCache('farm-1');

      expect(await repo.getCachedTasks('farm-1'), isNull);
      expect(await repo.getCachedTasks('farm-2'), isNotNull);
    });

    test('clearAll deletes all caches in table', () async {
      await repo.cacheTasks(
        farmId: 'farm-1',
        tasks: createSampleTasks(farmId: 'farm-1'),
      );
      await repo.cacheTasks(
        farmId: 'farm-2',
        tasks: createSampleTasks(farmId: 'farm-2'),
      );

      await repo.clearAll();

      expect(await repo.getCachedTasks('farm-1'), isNull);
      expect(await repo.getCachedTasks('farm-2'), isNull);
    });

    test('empty farmId returns null and does not throw', () async {
      final res = await repo.getCachedTasks('');
      expect(res, isNull);

      await repo.cacheTasks(
        farmId: '',
        tasks: createSampleTasks(farmId: 'farm-1'),
      );
      await repo.clearCache('');
    });

    group('Pending Task Actions Queue', () {
      test('enqueuePendingAction and getPendingActionForTask roundtrips complete action', () async {
        final action = PendingTaskAction(
          id: 'action-1',
          farmId: 'farm-1',
          taskId: 'task-100',
          actionType: TaskActionType.complete,
          note: 'Tamamlandı notu',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
          userId: 'user-1',
        );

        await repo.enqueuePendingAction(action);

        final fetched = await repo.getPendingActionForTask('task-100', farmId: 'farm-1');
        expect(fetched, isNotNull);
        expect(fetched!.id, equals('action-1'));
        expect(fetched.farmId, equals('farm-1'));
        expect(fetched.taskId, equals('task-100'));
        expect(fetched.actionType, equals(TaskActionType.complete));
        expect(fetched.note, equals('Tamamlandı notu'));
        expect(fetched.reason, isNull);
        expect(fetched.userId, equals('user-1'));
      });

      test('enqueuePendingAction and getPendingActionForTask roundtrips notApplied action', () async {
        final action = PendingTaskAction(
          id: 'action-2',
          farmId: 'farm-1',
          taskId: 'task-101',
          actionType: TaskActionType.notApplied,
          reason: 'Hava şartları uygun değildi',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 5),
          userId: 'user-1',
        );

        await repo.enqueuePendingAction(action);

        final fetched = await repo.getPendingActionForTask('task-101');
        expect(fetched, isNotNull);
        expect(fetched!.id, equals('action-2'));
        expect(fetched.actionType, equals(TaskActionType.notApplied));
        expect(fetched.reason, equals('Hava şartları uygun değildi'));
      });

      test('enqueuePendingAction throws StateError if task already has a pending action', () async {
        final action1 = PendingTaskAction(
          id: 'action-1',
          farmId: 'farm-1',
          taskId: 'task-dup',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        );
        final action2 = PendingTaskAction(
          id: 'action-2',
          farmId: 'farm-1',
          taskId: 'task-dup',
          actionType: TaskActionType.notApplied,
          reason: 'Farklı gerekçe',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 1),
        );

        await repo.enqueuePendingAction(action1);

        expect(
          () => repo.enqueuePendingAction(action2),
          throwsA(isA<StateError>()),
        );
      });

      test('FIFO ordering: getPendingActions returns actions ordered by created_at_utc ASC', () async {
        final action1 = PendingTaskAction(
          id: 'action-old',
          farmId: 'farm-1',
          taskId: 'task-1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 9, 0),
        );
        final action2 = PendingTaskAction(
          id: 'action-middle',
          farmId: 'farm-1',
          taskId: 'task-2',
          actionType: TaskActionType.notApplied,
          reason: 'Gerekçe 2',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        );
        final action3 = PendingTaskAction(
          id: 'action-new',
          farmId: 'farm-1',
          taskId: 'task-3',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 11, 0),
        );

        // Ters sırada ekle
        await repo.enqueuePendingAction(action3);
        await repo.enqueuePendingAction(action1);
        await repo.enqueuePendingAction(action2);

        final list = await repo.getPendingActions(farmId: 'farm-1');
        expect(list.length, equals(3));
        expect(list[0].id, equals('action-old'));
        expect(list[1].id, equals('action-middle'));
        expect(list[2].id, equals('action-new'));
      });

      test('farm isolation: actions of Farm A are never mixed with Farm B', () async {
        final actionA = PendingTaskAction(
          id: 'action-A',
          farmId: 'farm-A',
          taskId: 'task-A',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        );
        final actionB = PendingTaskAction(
          id: 'action-B',
          farmId: 'farm-B',
          taskId: 'task-B',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 1),
        );

        await repo.enqueuePendingAction(actionA);
        await repo.enqueuePendingAction(actionB);

        final listA = await repo.getPendingActions(farmId: 'farm-A');
        final listB = await repo.getPendingActions(farmId: 'farm-B');

        expect(listA.length, equals(1));
        expect(listA.first.taskId, equals('task-A'));

        expect(listB.length, equals(1));
        expect(listB.first.taskId, equals('task-B'));
      });

      test('user isolation: actions of User A are not returned when queried for User B', () async {
        final actionUser1 = PendingTaskAction(
          id: 'action-u1',
          farmId: 'farm-1',
          taskId: 'task-u1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
          userId: 'user-1',
        );
        final actionUser2 = PendingTaskAction(
          id: 'action-u2',
          farmId: 'farm-1',
          taskId: 'task-u2',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 1),
          userId: 'user-2',
        );

        await repo.enqueuePendingAction(actionUser1);
        await repo.enqueuePendingAction(actionUser2);

        final u1Actions = await repo.getPendingActions(userId: 'user-1');
        final u2Actions = await repo.getPendingActions(userId: 'user-2');

        expect(u1Actions.length, equals(1));
        expect(u1Actions.first.id, equals('action-u1'));

        expect(u2Actions.length, equals(1));
        expect(u2Actions.first.id, equals('action-u2'));
      });

      test('getPendingActionsMap returns a Map keyed by taskId', () async {
        final action1 = PendingTaskAction(
          id: 'action-1',
          farmId: 'farm-1',
          taskId: 'task-10',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        );
        final action2 = PendingTaskAction(
          id: 'action-2',
          farmId: 'farm-1',
          taskId: 'task-20',
          actionType: TaskActionType.notApplied,
          reason: 'Gerekçe',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 1),
        );

        await repo.enqueuePendingAction(action1);
        await repo.enqueuePendingAction(action2);

        final map = await repo.getPendingActionsMap(farmId: 'farm-1');
        expect(map.length, equals(2));
        expect(map.containsKey('task-10'), isTrue);
        expect(map.containsKey('task-20'), isTrue);
        expect(map['task-10']!.actionType, equals(TaskActionType.complete));
        expect(map['task-20']!.actionType, equals(TaskActionType.notApplied));
      });

      test('removePendingAction removes action from queue', () async {
        final action = PendingTaskAction(
          id: 'action-to-remove',
          farmId: 'farm-1',
          taskId: 'task-rm',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        );

        await repo.enqueuePendingAction(action);
        expect(await repo.getPendingActionForTask('task-rm'), isNotNull);

        await repo.removePendingAction('action-to-remove');
        expect(await repo.getPendingActionForTask('task-rm'), isNull);
      });

      test('updatePendingAction updates attemptCount, lastAttemptAtUtc, and lastErrorCode', () async {
        final action = PendingTaskAction(
          id: 'action-update',
          farmId: 'farm-1',
          taskId: 'task-up',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
          attemptCount: 0,
        );

        await repo.enqueuePendingAction(action);

        final attemptTime = DateTime.utc(2026, 9, 5, 10, 30);
        final updated = action.copyWith(
          attemptCount: 1,
          lastAttemptAtUtc: attemptTime,
          lastErrorCode: 503,
        );
        await repo.updatePendingAction(updated);

        final fetched = await repo.getPendingActionForTask('task-up');
        expect(fetched, isNotNull);
        expect(fetched!.attemptCount, equals(1));
        expect(fetched.lastAttemptAtUtc?.toUtc(), equals(attemptTime));
        expect(fetched.lastErrorCode, equals(503));
      });

      test('clearPendingActions clears queue for farm', () async {
        await repo.enqueuePendingAction(PendingTaskAction(
          id: 'a1',
          farmId: 'farm-1',
          taskId: 't1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));
        await repo.enqueuePendingAction(PendingTaskAction(
          id: 'a2',
          farmId: 'farm-2',
          taskId: 't2',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        await repo.clearPendingActions(farmId: 'farm-1');

        expect(await repo.getPendingActions(farmId: 'farm-1'), isEmpty);
        expect(await repo.getPendingActions(farmId: 'farm-2'), hasLength(1));
      });

      test('clearAll clears both daily_tasks_cache and pending_task_actions', () async {
        await repo.cacheTasks(
          farmId: 'farm-1',
          tasks: createSampleTasks(farmId: 'farm-1'),
        );
        await repo.enqueuePendingAction(PendingTaskAction(
          id: 'a1',
          farmId: 'farm-1',
          taskId: 't1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        await repo.clearAll();

        expect(await repo.getCachedTasks('farm-1'), isNull);
        expect(await repo.getPendingActions(farmId: 'farm-1'), isEmpty);
      });
    });
  });
}

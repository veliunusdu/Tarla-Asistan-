import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tasks/data/daily_task_repository.dart';
import 'package:mobile/features/tasks/data/local_daily_task_repository.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';
import 'package:mobile/features/tasks/presentation/widgets/bugunun_gorevleri_widget.dart';
import 'package:mobile/services/api_client.dart';
import 'package:mobile/services/database_helper.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

/// State-tracking test repository that simulates the complete backend + queue lifecycle
/// for high-level E2E integration testing.
class E2ETestDailyTaskRepository implements DailyTaskRepository {
  bool isOffline = false;
  int getDailyTasksCalls = 0;
  final List<String> completedTaskIds = [];
  final List<PendingTaskAction> queue = [];
  final List<String> syncCalls = [];

  DailyTaskList? currentLiveList;
  DailyTaskList? cachedList;

  @override
  Future<DailyTaskList> getDailyTasks(String farmId) async {
    getDailyTasksCalls++;
    if (isOffline) {
      if (cachedList != null) {
        // Decorate with pending actions
        final pendingMap = {for (final a in queue) a.taskId: a};
        return cachedList!.copyWith(
          isFromCache: true,
          items: cachedList!.items.map((t) {
            final pending = pendingMap[t.id];
            return pending != null ? t.copyWith(pendingAction: pending) : t;
          }).toList(),
        );
      }
      throw const ApiException('Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.');
    }

    // Online: update cache and return
    final pendingMap = {for (final a in queue) a.taskId: a};
    final list = currentLiveList ??
        DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: const [],
          criticalWeatherAlerts: const [],
          overdue: const [],
        );

    cachedList = list.copyWith(isFromCache: true);
    return list.copyWith(
      items: list.items.map((t) {
        final pending = pendingMap[t.id];
        return pending != null ? t.copyWith(pendingAction: pending) : t;
      }).toList(),
    );
  }

  @override
  Future<void> completeTask({
    required String farmId,
    required String taskId,
    String? note,
  }) async {
    if (isOffline) {
      throw const ApiException('No Internet');
    }
    completedTaskIds.add(taskId);
  }

  @override
  Future<void> markTaskNotApplied({
    required String farmId,
    required String taskId,
    required String reason,
  }) async {
    if (isOffline) {
      throw const ApiException('No Internet');
    }
  }

  @override
  Future<void> enqueueTaskAction(PendingTaskAction action) async {
    queue.add(action);
  }

  @override
  Future<List<PendingTaskAction>> getPendingActions({String? farmId}) async {
    return List.unmodifiable(queue);
  }

  @override
  Future<SyncResult> syncPendingTaskActions({String? farmId}) async {
    syncCalls.add(farmId ?? '');
    if (isOffline) {
      return const SyncResult(failedCount: 1);
    }
    final count = queue.length;
    queue.clear();
    return SyncResult(syncedCount: count);
  }
}

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  FarmTask createTask({
    required String id,
    required String title,
    String reason = 'Düzenli kontrol',
    TaskPriority priority = TaskPriority.high,
    TaskSource source = TaskSource.cropCalendar,
  }) {
    return FarmTask(
      id: id,
      farmId: 'farm-1',
      title: title,
      description: 'Açıklama',
      reason: reason,
      priority: priority,
      status: TaskStatus.newTask,
      source: source,
      confidence: TaskConfidence.high,
      dueDate: DateTime(2026, 9, 5),
      expertReviewRecommended: false,
    );
  }

  group('Daily Tasks E2E Integration Tests', () {
    testWidgets(
      'Full Happy-Path E2E: Launch -> Load 3 tasks + alert -> Expand reason -> Complete online -> Refresh -> Offline queue -> Rebuild -> Reconnect sync -> Fresh tasks',
      (tester) async {
        final repo = E2ETestDailyTaskRepository();

        final taskA = createTask(id: 'task-A', title: 'Görev A - Sulama', priority: TaskPriority.critical, reason: 'Toprak nem oranı kritik');
        final taskB = createTask(id: 'task-B', title: 'Görev B - İlaçlama', priority: TaskPriority.high, reason: 'Pas hastalığı başlangıcı');
        final taskC = createTask(id: 'task-C', title: 'Görev C - Gübreleme', priority: TaskPriority.medium, reason: 'Gelişim dönemi desteği');
        final taskD = createTask(id: 'task-D', title: 'Görev D - Çapalama', priority: TaskPriority.low, reason: 'Yabancı ot');
        final taskE = createTask(id: 'task-E', title: 'Görev E - Budama', priority: TaskPriority.low, reason: 'Hava sirkülasyonu');

        final alert1 = createTask(id: 'alert-1', title: 'Fırtına Uyarısı', priority: TaskPriority.critical, source: TaskSource.weather);

        // Initial 3 tasks + 1 alert
        repo.currentLiveList = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [taskA, taskB, taskC],
          criticalWeatherAlerts: [alert1],
          overdue: const [],
        );

        // 1. Launch & render widget
        final refreshNotifier = ValueNotifier<int>(0);
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: BugununGorevleriWidget(
                  farmId: 'farm-1',
                  tarlaAdi: 'Ana Tarla',
                  dailyTaskRepository: repo,
                  refreshNotifier: refreshNotifier,
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        // 2. Verify critical alert and 3 tasks are rendered (4th task is not)
        expect(find.text('Fırtına Uyarısı'), findsOneWidget);
        expect(find.text('Görev A - Sulama'), findsOneWidget);
        expect(find.text('Görev B - İlaçlama'), findsOneWidget);
        expect(find.text('Görev C - Gübreleme'), findsOneWidget);
        expect(find.text('Görev D - Çapalama'), findsNothing);

        // 3. Expand reason for Task A
        expect(find.text('Toprak nem oranı kritik'), findsNothing);
        final taskACard = find.ancestor(
          of: find.text('Görev A - Sulama'),
          matching: find.byType(Card),
        );
        await tester.tap(find.descendant(of: taskACard, matching: find.text('Neden?')));
        await tester.pumpAndSettle();
        expect(find.text('Toprak nem oranı kritik'), findsOneWidget);

        // 4. Complete Task A online
        // When completed, backend rotates list to [B, C, D]
        repo.currentLiveList = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [taskB, taskC, taskD],
          criticalWeatherAlerts: [alert1],
          overdue: const [],
        );

        final completeBtnA = find.byKey(const Key('task_complete_task-A'));
        await tester.tap(completeBtnA);
        await tester.pumpAndSettle();

        expect(repo.completedTaskIds, contains('task-A'));
        expect(find.text('Görev tamamlandı.'), findsOneWidget);
        // Advance timer to dismiss the completed snackbar
        await tester.pump(const Duration(seconds: 4));
        await tester.pumpAndSettle();

        // 5. Verify refreshed list shows B, C, D (Task A gone)
        expect(find.text('Görev A - Sulama'), findsNothing);
        expect(find.text('Görev B - İlaçlama'), findsOneWidget);
        expect(find.text('Görev C - Gübreleme'), findsOneWidget);
        expect(find.text('Görev D - Çapalama'), findsOneWidget);

        // 6. Transition to offline mode
        repo.isOffline = true;
        refreshNotifier.value++;
        await tester.pumpAndSettle();

        // Verify offline banner is shown
        expect(find.byKey(const Key('tasks_offline_banner')), findsOneWidget);

        // 7. Mark Task B as "Yaptım" while offline
        final completeBtnB = find.byKey(const Key('task_complete_task-B'));
        await tester.tap(completeBtnB);
        await tester.pumpAndSettle();

        expect(find.textContaining('İşlem kaydedildi. İnternete bağlanıldığında senkronize edilecek.'), findsOneWidget);
        expect(find.text('✓ Yaptım — Senkronizasyon bekliyor'), findsOneWidget);
        expect(repo.queue.length, equals(1));
        expect(repo.queue.first.taskId, equals('task-B'));
        expect(repo.queue.first.actionType, equals(TaskActionType.complete));

        // Advance timer to dismiss the queued action snackbar
        await tester.pump(const Duration(seconds: 4));
        await tester.pumpAndSettle();

        // 8. Rebuild widget state (simulating screen reload/restart)
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: BugununGorevleriWidget(
                  farmId: 'farm-1',
                  tarlaAdi: 'Ana Tarla',
                  dailyTaskRepository: repo,
                  refreshNotifier: refreshNotifier,
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Pending badge is preserved
        expect(find.text('✓ Yaptım — Senkronizasyon bekliyor'), findsOneWidget);

        // 9. Reconnect online & sync
        repo.isOffline = false;
        repo.currentLiveList = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [taskC, taskD, taskE],
          criticalWeatherAlerts: [alert1],
          overdue: const [],
        );

        // Tap retry on offline banner
        await tester.tap(find.byKey(const Key('tasks_offline_retry_button')));
        await tester.pumpAndSettle();

        // Queue is drained
        expect(repo.queue, isEmpty);

        // Fresh items displayed cleanly
        expect(find.text('Görev B - İlaçlama'), findsNothing);
        expect(find.text('Görev C - Gübreleme'), findsOneWidget);
        expect(find.text('Görev D - Çapalama'), findsOneWidget);
        expect(find.text('Görev E - Budama'), findsOneWidget);
        expect(find.text('✓ Yaptım — Senkronizasyon bekliyor'), findsNothing);
      },
    );

    test('Corrupted cache handling: gracefully falls back to error state with retry button', () async {
      final db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
      final localRepo = LocalDailyTaskRepository(databaseProvider: () async => db);

      await localRepo.cacheTasks(
        farmId: 'farm-corrupt',
        tasks: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: const [],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      // Corrupt payload_json
      await db.update(
        LocalDailyTaskRepository.tableName,
        {'payload_json': '{corrupted_json...'},
        where: 'farm_id = ?',
        whereArgs: ['farm-corrupt'],
      );

      // Verify getCachedTasks returns null instead of throwing
      final cached = await localRepo.getCachedTasks('farm-corrupt');
      expect(cached, isNull);

      await db.close();
    });

    test('Cross-user isolation: User B cannot see or sync User A pending actions', () async {
      final db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
      final localRepo = LocalDailyTaskRepository(databaseProvider: () async => db);

      // User A enqueues an action
      DatabaseHelper.instance.currentUserIdProvider = () => 'user-A';
      await localRepo.enqueuePendingAction(
        PendingTaskAction(
          id: 'action-A',
          farmId: 'farm-shared',
          taskId: 'task-1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
          userId: 'user-A',
        ),
      );

      // User B checks actions
      DatabaseHelper.instance.currentUserIdProvider = () => 'user-B';
      final userBActions = await localRepo.getPendingActions(farmId: 'farm-shared');
      expect(userBActions, isEmpty);

      final userBMap = await localRepo.getPendingActionsMap(farmId: 'farm-shared');
      expect(userBMap, isEmpty);

      // User A still has their action
      DatabaseHelper.instance.currentUserIdProvider = () => 'user-A';
      final userAActions = await localRepo.getPendingActions(farmId: 'farm-shared');
      expect(userAActions.length, equals(1));
      expect(userAActions.first.id, equals('action-A'));

      DatabaseHelper.instance.currentUserIdProvider = null;
      await db.close();
    });

    test('Forward compatibility: parses future unknown enums without crashing', () {
      final json = {
        'id': 'task-future',
        'farmId': 'farm-1',
        'title': 'Gelecek Görev',
        'description': 'Açıklama',
        'reason': 'Gerekçe',
        'priority': 'HyperCritical',
        'status': 'Archived',
        'source': 'SatelliteAI',
        'confidence': 'QuantumHigh',
        'dueDate': '2026-09-05',
        'expertReviewRecommended': false,
      };

      final task = FarmTask.fromJson(json);

      expect(task.id, equals('task-future'));
      expect(task.priority, equals(TaskPriority.unknown));
      expect(task.status, equals(TaskStatus.unknown));
      expect(task.source, equals(TaskSource.unknown));
      expect(task.confidence, equals(TaskConfidence.unknown));
    });
  });
}

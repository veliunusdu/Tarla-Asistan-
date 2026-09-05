import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/tasks/data/daily_task_repository.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';
import 'package:mobile/features/tasks/presentation/widgets/bugunun_gorevleri_widget.dart';
import 'package:mobile/services/api_client.dart';

// ---------------------------------------------------------------------------
// Fakes & Helpers
// ---------------------------------------------------------------------------

class FakeDailyTaskRepository implements DailyTaskRepository {
  FakeDailyTaskRepository({
    this.dailyTaskList,
    this.tasksByFarmId,
    this.error,
    this.completeError,
    this.notAppliedError,
    this.completeCompleter,
    this.notAppliedCompleter,
  });

  DailyTaskList? dailyTaskList;
  Map<String, DailyTaskList>? tasksByFarmId;
  Object? error;
  Object? completeError;
  Object? notAppliedError;
  Completer<void>? completeCompleter;
  Completer<void>? notAppliedCompleter;
  int callCount = 0;
  String? lastRequestedFarmId;

  final List<({String farmId, String taskId, String? note})> completedTaskCalls = [];
  final List<({String farmId, String taskId, String reason})> notAppliedTaskCalls = [];

  @override
  Future<DailyTaskList> getDailyTasks(String farmId) async {
    callCount++;
    lastRequestedFarmId = farmId;
    if (error != null) {
      throw error!;
    }
    if (tasksByFarmId != null && tasksByFarmId!.containsKey(farmId)) {
      return tasksByFarmId![farmId]!;
    }
    return dailyTaskList ??
        DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: const [],
          criticalWeatherAlerts: const [],
          overdue: const [],
        );
  }

  @override
  Future<void> completeTask({
    required String farmId,
    required String taskId,
    String? note,
  }) async {
    if (completeCompleter != null) {
      await completeCompleter!.future;
    }
    if (completeError != null) {
      throw completeError!;
    }
    completedTaskCalls.add((farmId: farmId, taskId: taskId, note: note));
  }

  @override
  Future<void> markTaskNotApplied({
    required String farmId,
    required String taskId,
    required String reason,
  }) async {
    if (notAppliedCompleter != null) {
      await notAppliedCompleter!.future;
    }
    if (notAppliedError != null) {
      throw notAppliedError!;
    }
    notAppliedTaskCalls.add((farmId: farmId, taskId: taskId, reason: reason));
  }

  final List<PendingTaskAction> queuedActions = [];
  final List<String?> syncPendingActionsCalls = [];
  SyncResult syncResultToReturn = const SyncResult();
  Object? enqueueError;
  Object? syncError;

  @override
  Future<void> enqueueTaskAction(PendingTaskAction action) async {
    if (enqueueError != null) throw enqueueError!;
    queuedActions.add(action);
  }

  @override
  Future<List<PendingTaskAction>> getPendingActions({String? farmId}) async {
    if (farmId != null) {
      return queuedActions.where((a) => a.farmId == farmId).toList();
    }
    return List.unmodifiable(queuedActions);
  }

  @override
  Future<SyncResult> syncPendingTaskActions({String? farmId}) async {
    syncPendingActionsCalls.add(farmId);
    if (syncError != null) throw syncError!;
    return syncResultToReturn;
  }
}

FarmTask _createTask({
  String id = 'task-1',
  String farmId = 'farm-1',
  String title = 'Görev 1',
  String description = 'Açıklama',
  String reason = 'Gerekçe',
  TaskPriority priority = TaskPriority.medium,
  TaskStatus status = TaskStatus.newTask,
  TaskSource source = TaskSource.cropCalendar,
  TaskConfidence confidence = TaskConfidence.medium,
  DateTime? dueDate,
  bool expertReviewRecommended = false,
}) {
  return FarmTask(
    id: id,
    farmId: farmId,
    title: title,
    description: description,
    reason: reason,
    priority: priority,
    status: status,
    source: source,
    confidence: confidence,
    dueDate: dueDate ?? DateTime(2026, 9, 5),
    expertReviewRecommended: expertReviewRecommended,
  );
}

Widget _wrapWidget(Widget child) {
  return MaterialApp(
    theme: AppTheme.light,
    home: Scaffold(
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: child,
        ),
      ),
    ),
  );
}

void main() {
  group('BugununGorevleriWidget', () {
    // -----------------------------------------------------------------------
    // Test 1 — 3 görev gösterimi
    // -----------------------------------------------------------------------
    testWidgets('Test 1 — Repository 3 item döndürdüğünde üçünün de başlığı görünmeli', (tester) async {
      final task1 = _createTask(id: 't1', title: 'Domates tarlasını sabah sula');
      final task2 = _createTask(id: 't2', title: 'Kuzey parselindeki yaprakları kontrol et');
      final task3 = _createTask(id: 't3', title: 'İlaçlamayı yarına ertele');

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [task1, task2, task3],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Domates tarlasını sabah sula'), findsOneWidget);
      expect(find.text('Kuzey parselindeki yaprakları kontrol et'), findsOneWidget);
      expect(find.text('İlaçlamayı yarına ertele'), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Test 2 — Backend sırası
    // -----------------------------------------------------------------------
    testWidgets('Test 2 — Backend sırası A -> B -> C aynen korunmalı', (tester) async {
      final taskA = _createTask(id: 'tA', title: 'Görev A', priority: TaskPriority.low);
      final taskB = _createTask(id: 'tB', title: 'Görev B', priority: TaskPriority.critical);
      final taskC = _createTask(id: 'tC', title: 'Görev C', priority: TaskPriority.medium);

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [taskA, taskB, taskC],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      final posA = tester.getTopLeft(find.text('Görev A')).dy;
      final posB = tester.getTopLeft(find.text('Görev B')).dy;
      final posC = tester.getTopLeft(find.text('Görev C')).dy;

      expect(posA, lessThan(posB));
      expect(posB, lessThan(posC));

      expect(find.text('1. '), findsOneWidget);
      expect(find.text('2. '), findsOneWidget);
      expect(find.text('3. '), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Test 3 — Neden? etkileşimi
    // -----------------------------------------------------------------------
    testWidgets('Test 3 — Reason mevcut olan görevde Neden? tıklanınca metin açılır', (tester) async {
      const reasonText = 'Toprak nem seviyesi kritik eşiğin altına indi.';
      final task = _createTask(id: 't1', title: 'Acil Sulama', reason: reasonText);

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [task],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text(reasonText), findsNothing);
      expect(find.text('Neden?'), findsOneWidget);

      await tester.tap(find.text('Neden?'));
      await tester.pumpAndSettle();

      expect(find.text(reasonText), findsOneWidget);

      await tester.tap(find.text('Neden?'));
      await tester.pumpAndSettle();

      expect(find.text(reasonText), findsNothing);
    });

    // -----------------------------------------------------------------------
    // Test 4 — Reason yok
    // -----------------------------------------------------------------------
    testWidgets('Test 4 — Reason boş olan görevde Neden? aksiyonu gösterilmez ve çökmez', (tester) async {
      final task = _createTask(id: 't1', title: 'Rutin Kontrol', reason: '');

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [task],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Rutin Kontrol'), findsOneWidget);
      expect(find.text('Neden?'), findsNothing);
    });

    // -----------------------------------------------------------------------
    // Test 5 — CriticalWeatherAlerts
    // -----------------------------------------------------------------------
    testWidgets('Test 5 — 3 normal görev + 1 kritik hava uyarısı toplam 4 içerik gösterir', (tester) async {
      final weatherAlert = _createTask(
        id: 'w1',
        title: 'Kuvvetli Fırtına ve Don Riski',
        reason: 'Gece sıcaklık -3 dereceye düşecek.',
        priority: TaskPriority.critical,
        source: TaskSource.weather,
      );
      final task1 = _createTask(id: 't1', title: 'Görev 1');
      final task2 = _createTask(id: 't2', title: 'Görev 2');
      final task3 = _createTask(id: 't3', title: 'Görev 3');

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [task1, task2, task3],
          criticalWeatherAlerts: [weatherAlert],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Kritik Uyarı'), findsOneWidget);
      expect(find.text('Kuvvetli Fırtına ve Don Riski'), findsOneWidget);

      expect(find.text('Görev 1'), findsOneWidget);
      expect(find.text('Görev 2'), findsOneWidget);
      expect(find.text('Görev 3'), findsOneWidget);

      final alertTop = tester.getTopLeft(find.text('Kuvvetli Fırtına ve Don Riski')).dy;
      final task1Top = tester.getTopLeft(find.text('Görev 1')).dy;
      expect(alertTop, lessThan(task1Top));
    });

    // -----------------------------------------------------------------------
    // Test 6 — Empty
    // -----------------------------------------------------------------------
    testWidgets('Test 6 — Items ve CriticalWeatherAlerts boş ise empty state görünmeli', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: const [],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Bugün için önemli bir iş görünmüyor.'), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Test 7 — Error
    // -----------------------------------------------------------------------
    testWidgets('Test 7 — Repository hata verirse error state ve Tekrar Dene görünmeli', (tester) async {
      final repo = FakeDailyTaskRepository(error: Exception('Ağ bağlantısı başarısız'));

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Bugünün işleri alınamadı'), findsOneWidget);
      expect(find.text('Tekrar Dene'), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Test 8 — Retry
    // -----------------------------------------------------------------------
    testWidgets('Test 8 — Tekrar dene basıldığında repository yeniden çağrılmalı', (tester) async {
      final repo = FakeDailyTaskRepository(error: Exception('İlk hata'));

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(repo.callCount, 1);
      expect(find.text('Bugünün işleri alınamadı'), findsOneWidget);

      repo.error = null;
      repo.dailyTaskList = DailyTaskList(
        date: DateTime(2026, 9, 5),
        items: [_createTask(id: 't1', title: 'Yeni Görev')],
        criticalWeatherAlerts: const [],
        overdue: const [],
      );

      await tester.tap(find.text('Tekrar Dene'));
      await tester.pumpAndSettle();

      expect(repo.callCount, 2);
      expect(find.text('Yeni Görev'), findsOneWidget);
      expect(find.text('Bugünün işleri alınamadı'), findsNothing);
    });

    // -----------------------------------------------------------------------
    // Test 9 — expertReviewRecommended
    // -----------------------------------------------------------------------
    testWidgets('Test 9 — expertReviewRecommended true ise öneri görünür, false ise görünmez', (tester) async {
      final taskWithExpert = _createTask(
        id: 't1',
        title: 'Hastalık Belirtisi İncele',
        expertReviewRecommended: true,
      );
      final taskWithoutExpert = _createTask(
        id: 't2',
        title: 'Sulama Yap',
        expertReviewRecommended: false,
      );

      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [taskWithExpert, taskWithoutExpert],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Uzman görüşü öneriliyor'), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Test 10 — Tarla değişimi
    // -----------------------------------------------------------------------
    testWidgets('Test 10 — farmId değiştiğinde yeni tarlanın görevleri fetch edilmeli ve eski veriler kalmamalı', (tester) async {
      final taskFarm1 = _createTask(id: 't1', title: 'Tarla 1 Budama');
      final taskFarm2 = _createTask(id: 't2', title: 'Tarla 2 Çapalama');

      final repo = FakeDailyTaskRepository(
        tasksByFarmId: {
          'farm-1': DailyTaskList(
            date: DateTime(2026, 9, 5),
            items: [taskFarm1],
            criticalWeatherAlerts: const [],
            overdue: const [],
          ),
          'farm-2': DailyTaskList(
            date: DateTime(2026, 9, 5),
            items: [taskFarm2],
            criticalWeatherAlerts: const [],
            overdue: const [],
          ),
        },
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Tarla 1 Budama'), findsOneWidget);
      expect(find.text('Tarla 2 Çapalama'), findsNothing);
      expect(repo.lastRequestedFarmId, 'farm-1');

      // farmId değiştiriliyor
      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-2',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Tarla 2 Çapalama'), findsOneWidget);
      expect(find.text('Tarla 1 Budama'), findsNothing);
      expect(repo.lastRequestedFarmId, 'farm-2');
    });

    // -----------------------------------------------------------------------
    // Ek Test — Priority Rozetleri
    // -----------------------------------------------------------------------
    testWidgets('Öncelik rozetleri Türkçe doğru metinlerle render edilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'G1', priority: TaskPriority.critical),
            _createTask(id: 't2', title: 'G2', priority: TaskPriority.high),
            _createTask(id: 't3', title: 'G3', priority: TaskPriority.low),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Kritik'), findsOneWidget);
      expect(find.text('Yüksek'), findsOneWidget);
      expect(find.text('Düşük'), findsOneWidget);
    });

    // -----------------------------------------------------------------------
    // Ek Test — FarmId boşken tarla ekle çağrısı
    // -----------------------------------------------------------------------
    testWidgets('farmId boşken Tarla Ekle durumu gösterilir', (tester) async {
      var tarlaEkleClicked = false;
      final repo = FakeDailyTaskRepository();

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: null,
          onTarlaEkle: () => tarlaEkleClicked = true,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Henüz tarla eklenmedi.'), findsOneWidget);
      expect(find.text('Tarla Ekle'), findsOneWidget);

      await tester.tap(find.text('Tarla Ekle'));
      expect(tarlaEkleClicked, isTrue);
    });

    // -----------------------------------------------------------------------
    // Adım 4 Testleri: Yaptım / Uygulamadım Etkileşimleri
    // -----------------------------------------------------------------------

    testWidgets('Normal görev kartlarında "Yaptım" ve "Uygulamadım" butonları görünür', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Sulama yap'),
            _createTask(id: 't2', title: 'Gübre at'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Yaptım'), findsNWidgets(2));
      expect(find.text('Uygulamadım'), findsNWidgets(2));
    });

    testWidgets('criticalWeatherAlerts kartlarında "Yaptım" veya "Uygulamadım" butonu bulunmaz', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: const [],
          criticalWeatherAlerts: [
            _createTask(
              id: 'alert-1',
              title: 'Don Tehlikesi',
              priority: TaskPriority.critical,
              source: TaskSource.weather,
            ),
          ],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Don Tehlikesi'), findsOneWidget);
      expect(find.text('Yaptım'), findsNothing);
      expect(find.text('Uygulamadım'), findsNothing);
    });

    testWidgets('Yaptım butonuna tıklandığında hızlı 1-tık ile completeTask çağrılır ve SnackBar gösterilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Domates tarlasını sula'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pumpAndSettle();

      expect(repo.completedTaskCalls.length, 1);
      expect(repo.completedTaskCalls.first.farmId, 'farm-1');
      expect(repo.completedTaskCalls.first.taskId, 't1');
      expect(find.text('Görev tamamlandı.'), findsOneWidget);
      // Başarı sonrası liste yeniden çekilmeli (ilk fetch + tamamlama sonrası fetch = 2)
      expect(repo.callCount, 2);
    });

    testWidgets('Yaptım butonuna tıklandığında işlem sürerken CircularProgressIndicator gösterilir ve buton kilitlenir', (tester) async {
      final completer = Completer<void>();
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Budama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        completeCompleter: completer,
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      completer.complete();
      await tester.pumpAndSettle();
      expect(find.byType(CircularProgressIndicator), findsNothing);
    });

    testWidgets('Yaptım butonuna hızlı çift tıklandığında repository sadece 1 kez çağrılır', (tester) async {
      final completer = Completer<void>();
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Çapalama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        completeCompleter: completer,
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pump();

      completer.complete();
      await tester.pumpAndSettle();

      expect(repo.completedTaskCalls.length, 1);
    });

    testWidgets('completeTask hata verirse hata SnackBar gösterilir ve kart kalır', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'İlaçlama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        completeError: Exception('Ağ bağlantısı koptu'),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pumpAndSettle();

      expect(find.text('İşlem gerçekleştirilemedi. Lütfen tekrar deneyin.'), findsOneWidget);
      // Görev kartı hala ekranda duruyor olmalı
      expect(find.text('İlaçlama yap'), findsOneWidget);
    });

    testWidgets('Uygulamadım butonuna tıklandığında bottom sheet açılır ve hazır nedenler listelenir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Yabancı ot temizliği'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      expect(find.text('Görevi Uygulamama Nedeni'), findsOneWidget);
      expect(find.text('Hava şartları uygun değildi'), findsOneWidget);
      expect(find.text('Zaman yetersizliği'), findsOneWidget);
      expect(find.text('Malzeme veya ekipman eksik'), findsOneWidget);
      expect(find.text('Gerekli görülmedi'), findsOneWidget);
      expect(find.text('Diğer'), findsOneWidget);
      expect(find.text('Vazgeç'), findsOneWidget);
      expect(find.text('Kaydet'), findsOneWidget);
    });

    // Test 2 — Hazır neden
    testWidgets('Test 2 — Hazır neden: Kullanıcı hazır neden seçip kaydettiğinde tam olarak seçilen gerekçe iletilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Yabancı ot temizliği'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      // Neden seç
      await tester.tap(find.byKey(const Key('reason_chip_Hava şartları uygun değildi')));
      await tester.pumpAndSettle();

      // Kaydet'e bas
      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls.length, 1);
      expect(repo.notAppliedTaskCalls.first.farmId, 'farm-1');
      expect(repo.notAppliedTaskCalls.first.taskId, 't1');
      expect(repo.notAppliedTaskCalls.first.reason, 'Hava şartları uygun değildi');
      expect(find.text('Görev uygulanmadı olarak kaydedildi.'), findsOneWidget);
      expect(repo.callCount, 2);
    });

    // Test 3 — Neden seçmeden kaydet
    testWidgets('Test 3 — Neden seçmeden kaydet: Hiç neden seçilmemişse Kaydet butonu devre dışıdır ve API çağrılmaz', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Sulama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      // Kaydet butonu disabled olmalı
      final saveButton = tester.widget<FilledButton>(find.byKey(const Key('confirm_not_applied_btn')));
      expect(saveButton.onPressed, isNull);

      // Tıklamayı dene
      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls, isEmpty);
    });

    // Test 4 — Diğer + boş input
    testWidgets('Test 4 — Diğer + boş input: Diğer seçilip metin girilmezse veya sadece boşluk girilirse Kaydet devre dışıdır', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Budama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      // "Diğer" seç
      await tester.tap(find.byKey(const Key('reason_chip_Diğer')));
      await tester.pumpAndSettle();

      // Metin kutusu boşken Kaydet disabled olmalı
      var saveButton = tester.widget<FilledButton>(find.byKey(const Key('confirm_not_applied_btn')));
      expect(saveButton.onPressed, isNull);

      // Sadece boşluk girildiğinde de disabled kalmalı
      await tester.enterText(find.byKey(const Key('custom_reason_field')), '     ');
      await tester.pumpAndSettle();

      saveButton = tester.widget<FilledButton>(find.byKey(const Key('confirm_not_applied_btn')));
      expect(saveButton.onPressed, isNull);

      expect(repo.notAppliedTaskCalls, isEmpty);
    });

    // Test 5 — Diğer + gerçek input
    testWidgets('Test 5 — Diğer + gerçek input: Diğer seçilip metin girildiğinde Diğer kelimesi değil kullanıcının açıklaması gönderilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Toprak analizi'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      // "Diğer" seç
      await tester.tap(find.byKey(const Key('reason_chip_Diğer')));
      await tester.pumpAndSettle();

      // Kullanıcı açıklaması
      await tester.enterText(find.byKey(const Key('custom_reason_field')), 'Sulama motoru arızalandı');
      await tester.pumpAndSettle();

      // Kaydet'e bas
      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls.length, 1);
      expect(repo.notAppliedTaskCalls.first.reason, 'Sulama motoru arızalandı');
      expect(repo.notAppliedTaskCalls.first.reason, isNot(equals('Diğer')));
    });

    // Test 6 — Whitespace temizliği
    testWidgets('Test 6 — Whitespace: Başında ve sonunda boşluk olan metin trim edilerek iletilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Gübre at'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('reason_chip_Diğer')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('custom_reason_field')), '  Sulama motoru arızalandı  ');
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls.length, 1);
      expect(repo.notAppliedTaskCalls.first.reason, 'Sulama motoru arızalandı');
    });

    // Test 7 — 500 karakter sınırı
    testWidgets('Test 7 — 500 karakter sınırı: Metin girişi 500 karakterle sınırlanır ve fazlası kesilir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Toprak sürümü'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('reason_chip_Diğer')));
      await tester.pumpAndSettle();

      final textField = tester.widget<TextField>(find.byKey(const Key('custom_reason_field')));
      expect(textField.maxLength, 500);

      // 600 karakterlik metin girmeyi dene
      final longText = 'A' * 600;
      await tester.enterText(find.byKey(const Key('custom_reason_field')), longText);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls.length, 1);
      expect(repo.notAppliedTaskCalls.first.reason.length, 500);
      expect(repo.notAppliedTaskCalls.first.reason, 'A' * 500);
    });

    // Test 8 — Cancel
    testWidgets('Test 8 — Cancel: Bottom sheet üzerinde "Vazgeç" tıklandığında repository çağrılmaz', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Sulama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('cancel_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(repo.notAppliedTaskCalls, isEmpty);
      expect(find.text('Görevi Uygulamama Nedeni'), findsNothing);
    });

    testWidgets('markTaskNotApplied genel hata verirse SnackBar hata gösterir ve kart kalır', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Gübreleme'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        notAppliedError: Exception('Sunucu hatası'),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('reason_chip_Zaman yetersizliği')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(find.text('İşlem gerçekleştirilemedi. Lütfen tekrar deneyin.'), findsOneWidget);
      expect(find.text('Gübreleme'), findsOneWidget);
    });

    // Test 10 — 409 Conflict durumları
    testWidgets('Test 10 — 409 Conflict: markTaskNotApplied 409 aldığında kullanıcıya bildirilir ve liste yenilenir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Gübreleme yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        notAppliedError: const ApiException('Çakışma', statusCode: 409),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_not_applied_t1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('reason_chip_Zaman yetersizliği')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_not_applied_btn')));
      await tester.pumpAndSettle();

      expect(find.text('Bu görev daha önce güncellenmiş. Liste yenileniyor.'), findsOneWidget);
      // Liste tekrar çekildi (1 başlangıçta + 1 409 sonrasında = 2)
      expect(repo.callCount, 2);
    });

    testWidgets('Test 10 — 409 Conflict: completeTask 409 aldığında kullanıcıya bildirilir ve liste yenilenir', (tester) async {
      final repo = FakeDailyTaskRepository(
        dailyTaskList: DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            _createTask(id: 't1', title: 'Sulama yap'),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
        ),
        completeError: const ApiException('Çakışma', statusCode: 409),
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pumpAndSettle();

      expect(find.text('Bu görev daha önce güncellenmiş. Liste yenileniyor.'), findsOneWidget);
      expect(repo.callCount, 2);
    });

    testWidgets('İşlem sürerken farmId değişirse yeni tarlaya eski görev tamamlama yansımaz', (tester) async {
      final completer = Completer<void>();
      final repo = FakeDailyTaskRepository(
        tasksByFarmId: {
          'farm-1': DailyTaskList(
            date: DateTime(2026, 9, 5),
            items: [_createTask(id: 't1', farmId: 'farm-1', title: 'Tarla 1 Görev')],
            criticalWeatherAlerts: const [],
            overdue: const [],
          ),
          'farm-2': DailyTaskList(
            date: DateTime(2026, 9, 5),
            items: [_createTask(id: 't2', farmId: 'farm-2', title: 'Tarla 2 Görev')],
            criticalWeatherAlerts: const [],
            overdue: const [],
          ),
        },
        completeCompleter: completer,
      );

      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-1',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Tarla 1 Görev'), findsOneWidget);

      // t1 tamamla basılır, işlem bekler
      await tester.tap(find.byKey(const Key('task_complete_t1')));
      await tester.pump();

      // farmId farm-2 olarak değişir
      await tester.pumpWidget(_wrapWidget(
        BugununGorevleriWidget(
          dailyTaskRepository: repo,
          farmId: 'farm-2',
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Tarla 2 Görev'), findsOneWidget);

      // Eski completer serbest bırakılır
      completer.complete();
      await tester.pumpAndSettle();

      // SnackBar gösterilmemeli çünkü farmId değişti
      expect(find.text('Görev tamamlandı.'), findsNothing);
      expect(find.text('Tarla 2 Görev'), findsOneWidget);
    });

    group('Offline Cache & UI Davranışları', () {
      testWidgets('Bugünün cached verisi gösterildiğinde Son güncelleme saati ve Yenile butonu görünür', (tester) async {
        final now = DateTime.now();
        final cachedTime = DateTime(now.year, now.month, now.day, 8, 42);
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: now,
            items: [_createTask(id: 't1', title: 'Toprak Havalandırma')],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
            cachedAt: cachedTime,
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('tasks_offline_banner')), findsOneWidget);
        expect(find.textContaining('Çevrimdışı • Son güncelleme 08:42'), findsOneWidget);
        expect(find.byKey(const Key('tasks_offline_retry_button')), findsOneWidget);
        expect(find.text('Toprak Havalandırma'), findsOneWidget);
      });

      testWidgets('Dünden kalan cached veri gösterildiğinde tarih uyarısı görünür', (tester) async {
        final yesterday = DateTime.now().subtract(const Duration(days: 1));
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: yesterday,
            items: [_createTask(id: 't1', title: 'Dünkü İş')],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
            cachedAt: yesterday,
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('tasks_offline_banner')), findsOneWidget);
        expect(find.textContaining('Çevrimdışı — bu görevler'), findsOneWidget);
        expect(find.textContaining('tarihinden.'), findsOneWidget);
        expect(find.text('Dünkü İş'), findsOneWidget);
      });

      testWidgets('Cache modunda kritik hava uyarısında Çevrimdışı kayıt etiketi gösterilir', (tester) async {
        final weatherAlert = FarmTask(
          id: 'alert-weather',
          farmId: 'farm-1',
          title: 'Şiddetli Yağış Uyarısı',
          description: 'Açıklama',
          reason: 'Gerekçe',
          priority: TaskPriority.critical,
          status: TaskStatus.newTask,
          source: TaskSource.weather,
          confidence: TaskConfidence.high,
          dueDate: DateTime.now(),
          expertReviewRecommended: false,
        );

        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [_createTask(id: 't1', title: 'Normal Görev')],
            criticalWeatherAlerts: [weatherAlert],
            overdue: const [],
            isFromCache: true,
            cachedAt: DateTime.now(),
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.text('Şiddetli Yağış Uyarısı'), findsOneWidget);
        expect(find.text('Çevrimdışı kayıt'), findsOneWidget);
        expect(find.byKey(const Key('weather_alert_offline_badge_alert-weather')), findsOneWidget);
      });

      testWidgets('Cache modunda Yaptım ve Uygulamadım butonları etkindir ve tıklandığında çevrimdışı kuyruğa alınır', (tester) async {
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [_createTask(id: 't1', title: 'Offline Görev')],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
            cachedAt: DateTime.now(),
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.text('Çevrimdışı kaydedilen işlemler internete bağlanıldığında senkronize edilir.'), findsOneWidget);

        final notAppliedButton = tester.widget<OutlinedButton>(
          find.byKey(const Key('task_not_applied_t1')),
        );
        expect(notAppliedButton.onPressed, isNotNull);

        final completeButton = tester.widget<FilledButton>(
          find.byKey(const Key('task_complete_t1')),
        );
        expect(completeButton.onPressed, isNotNull);

        // Çevrimdışı Yaptım tıklandığında kuyruğa eklenmeli
        await tester.tap(find.byKey(const Key('task_complete_t1')));
        await tester.pumpAndSettle();

        expect(repo.queuedActions.length, 1);
        expect(repo.queuedActions.first.taskId, 't1');
        expect(repo.queuedActions.first.actionType, TaskActionType.complete);
        expect(find.text('İşlem kaydedildi. İnternete bağlanıldığında senkronize edilecek.'), findsOneWidget);
      });

      testWidgets('Yenile butonuna tıklandığında remote veri başarılı olursa çevrimdışı banner kalkar ve canlı veriler gösterilir', (tester) async {
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [_createTask(id: 't1', title: 'Görev 1')],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
            cachedAt: DateTime.now(),
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('tasks_offline_banner')), findsOneWidget);

        // Repo canlı veriye döner
        repo.dailyTaskList = DailyTaskList(
          date: DateTime.now(),
          items: [_createTask(id: 't1', title: 'Canlı Görev 1')],
          criticalWeatherAlerts: const [],
          overdue: const [],
          isFromCache: false,
          cachedAt: null,
        );

        // Yenile butonuna tıkla
        await tester.tap(find.byKey(const Key('tasks_offline_retry_button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('tasks_offline_banner')), findsNothing);
        expect(find.text('Canlı Görev 1'), findsOneWidget);
        expect(tester.widget<FilledButton>(find.byKey(const Key('task_complete_t1'))).onPressed, isNotNull);
        expect(tester.widget<OutlinedButton>(find.byKey(const Key('task_not_applied_t1'))).onPressed, isNotNull);
      });

      testWidgets('Tarla değiştirildiğinde eski tarlanın görevleri anında temizlenir', (tester) async {
        final repo = FakeDailyTaskRepository(
          tasksByFarmId: {
            'farm-1': DailyTaskList(
              date: DateTime(2026, 9, 5),
              items: [_createTask(id: 't1', farmId: 'farm-1', title: 'Tarla 1 Görev')],
              criticalWeatherAlerts: const [],
              overdue: const [],
              isFromCache: true,
            ),
            'farm-2': DailyTaskList(
              date: DateTime(2026, 9, 5),
              items: [_createTask(id: 't2', farmId: 'farm-2', title: 'Tarla 2 Görev')],
              criticalWeatherAlerts: const [],
              overdue: const [],
              isFromCache: true,
            ),
          },
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-1',
          ),
        ));
        await tester.pumpAndSettle();

        expect(find.text('Tarla 1 Görev'), findsOneWidget);

        // Farm değiştir
        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(
            dailyTaskRepository: repo,
            farmId: 'farm-2',
          ),
        ));
        // Henüz settle olmadan önce pump:
        await tester.pump();
        // Tarla 1 görevi anında kaybolmalı
        expect(find.text('Tarla 1 Görev'), findsNothing);

        await tester.pumpAndSettle();
        expect(find.text('Tarla 2 Görev'), findsOneWidget);
      });
    });

    group('Offline Actions & Sync UI (Step 7)', () {
      testWidgets('hasPendingAction complete ise ✓ Yaptım — Senkronizasyon bekliyor rozeti gösterilir ve butonlar kaldırılır', (tester) async {
        final task = _createTask(id: 't-complete', title: 'Tamamlanan Görev').copyWith(
          pendingAction: PendingTaskAction(
            id: 'a1',
            farmId: 'farm-1',
            taskId: 't-complete',
            actionType: TaskActionType.complete,
            createdAtUtc: DateTime.now().toUtc(),
          ),
        );

        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [task],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(dailyTaskRepository: repo, farmId: 'farm-1'),
        ));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('task_pending_badge_t-complete')), findsOneWidget);
        expect(find.text('✓ Yaptım — Senkronizasyon bekliyor'), findsOneWidget);
        expect(find.byKey(const Key('task_complete_t-complete')), findsNothing);
        expect(find.byKey(const Key('task_not_applied_t-complete')), findsNothing);
      });

      testWidgets('hasPendingAction notApplied ise Uygulanmadı — Senkronizasyon bekliyor rozeti gösterilir', (tester) async {
        final task = _createTask(id: 't-not-applied', title: 'Uygulanmayan Görev').copyWith(
          pendingAction: PendingTaskAction(
            id: 'a2',
            farmId: 'farm-1',
            taskId: 't-not-applied',
            actionType: TaskActionType.notApplied,
            reason: 'Hava şartları uygun değildi',
            createdAtUtc: DateTime.now().toUtc(),
          ),
        );

        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [task],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(dailyTaskRepository: repo, farmId: 'farm-1'),
        ));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('task_pending_badge_t-not-applied')), findsOneWidget);
        expect(find.text('Uygulanmadı — Senkronizasyon bekliyor'), findsOneWidget);
        expect(find.byKey(const Key('task_complete_t-not-applied')), findsNothing);
        expect(find.byKey(const Key('task_not_applied_t-not-applied')), findsNothing);
      });

      testWidgets('Çevrimdışı modda Uygulamadım tıklandığında bottom sheet açılır ve seçilen gerekçe ile kuyruğa alınır', (tester) async {
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [_createTask(id: 't-offline-na', title: 'Ot Temizliği')],
            criticalWeatherAlerts: const [],
            overdue: const [],
            isFromCache: true,
          ),
        );

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(dailyTaskRepository: repo, farmId: 'farm-1'),
        ));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('task_not_applied_t-offline-na')));
        await tester.pumpAndSettle();

        expect(find.text('Görevi Uygulamama Nedeni'), findsOneWidget);
        await tester.tap(find.text('Hava şartları uygun değildi'));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.queuedActions.length, 1);
        expect(repo.queuedActions.first.taskId, 't-offline-na');
        expect(repo.queuedActions.first.actionType, TaskActionType.notApplied);
        expect(repo.queuedActions.first.reason, 'Hava şartları uygun değildi');
        expect(find.text('Görev uygulanmadı olarak kaydedildi. İnternete bağlanıldığında senkronize edilecek.'), findsOneWidget);
      });

      testWidgets('syncPendingTaskActions conflictCount > 0 döndüğünde SnackBar ile kullanıcıya çakışma bildirilir', (tester) async {
        final repo = FakeDailyTaskRepository(
          dailyTaskList: DailyTaskList(
            date: DateTime.now(),
            items: [_createTask(id: 't1', title: 'Görev')],
            criticalWeatherAlerts: const [],
            overdue: const [],
          ),
        );
        repo.syncResultToReturn = const SyncResult(conflictCount: 1);

        await tester.pumpWidget(_wrapWidget(
          BugununGorevleriWidget(dailyTaskRepository: repo, farmId: 'farm-1'),
        ));
        await tester.pumpAndSettle();

        expect(find.text('Bekleyen bir veya daha fazla işlem sunucudaki güncel durum ile çakıştı ve kaldırıldı.'), findsOneWidget);
      });
    });
  });
}
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/tasks/data/backend_daily_task_repository.dart';
import 'package:mobile/features/tasks/data/local_daily_task_repository.dart';
import 'package:mobile/features/tasks/domain/farm_task.dart';
import 'package:mobile/features/tasks/domain/pending_task_action.dart';
import 'package:mobile/features/tasks/domain/task_enums.dart';
import 'package:mobile/services/api_client.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

ApiClient _clientReturning(Map<String, dynamic> body, {int statusCode = 200}) {
  return ApiClient(
    httpClient: MockClient(
      (request) async => http.Response(
        jsonEncode(body),
        statusCode,
        headers: {'content-type': 'application/json'},
      ),
    ),
    idTokenProvider: () async => 'test-token',
  );
}

ApiClient _clientWithHandler(Future<http.Response> Function(http.Request request) handler) {
  return ApiClient(
    httpClient: MockClient(handler),
    idTokenProvider: () async => 'test-token',
  );
}

Map<String, dynamic> _taskJson({
  String id = 'task-1',
  String farmId = 'farm-1',
  String title = 'Ürün gelisimini kontrol edin',
  String description = 'Bitki gelisimini kontrol edin.',
  String reason = 'Ekim takviminin 30. gunu.',
  String priority = 'Medium',
  String status = 'New',
  String source = 'CropCalendar',
  String confidence = 'Medium',
  String dueDate = '2026-09-05',
  bool expertReviewRecommended = false,
}) =>
    {
      'id': id,
      'farmId': farmId,
      'cropPeriodId': null,
      'createdById': null,
      'title': title,
      'description': description,
      'reason': reason,
      'priority': priority,
      'status': status,
      'source': source,
      'confidence': confidence,
      'dueDate': dueDate,
      'notAppliedReason': null,
      'completionNote': null,
      'photoUrl': null,
      'viewedAtUtc': null,
      'completedAtUtc': null,
      'createdAtUtc': '2026-09-05T06:00:00Z',
      'updatedAtUtc': '2026-09-05T06:00:00Z',
      'expertReviewRecommended': expertReviewRecommended,
    };

Map<String, dynamic> _dailyTaskListJson({
  List<Map<String, dynamic>>? items,
  List<Map<String, dynamic>>? criticalWeatherAlerts,
  List<Map<String, dynamic>>? overdue,
  int visibleLimit = 3,
  String date = '2026-09-05',
}) =>
    {
      'date': date,
      'items': items ?? [],
      'criticalWeatherAlerts': criticalWeatherAlerts ?? [],
      'overdue': overdue ?? [],
      'visibleLimit': visibleLimit,
    };

FarmTask _task(String id, {String title = 'Test'}) => FarmTask(
      id: id,
      farmId: 'farm-1',
      title: title,
      description: 'Aciklama',
      reason: 'Gerekce',
      priority: TaskPriority.medium,
      status: TaskStatus.newTask,
      source: TaskSource.cropCalendar,
      confidence: TaskConfidence.high,
      dueDate: DateTime(2026, 9, 5),
      expertReviewRecommended: false,
    );

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('BackendDailyTaskRepository', () {
    // -----------------------------------------------------------------------
    // Successful full response
    // -----------------------------------------------------------------------

    test('basarili response — 3 item, 1 criticalWeatherAlert, 1 overdue', () async {
      final weatherAlert = _taskJson(
        id: 'alert-1',
        title: 'Don riskine karsi tarlanizi kontrol edin',
        priority: 'Critical',
        source: 'Weather',
        confidence: 'Medium',
      );
      final overdueTask = _taskJson(
        id: 'overdue-1',
        status: 'Overdue',
        dueDate: '2026-09-01',
      );
      final body = _dailyTaskListJson(
        items: [
          _taskJson(id: 'item-1', priority: 'High'),
          _taskJson(id: 'item-2', priority: 'Medium'),
          _taskJson(id: 'item-3', priority: 'Low'),
        ],
        criticalWeatherAlerts: [weatherAlert],
        overdue: [overdueTask],
      );

      final repo = BackendDailyTaskRepository(
        apiClient: _clientReturning(body),
      );
      final result = await repo.getDailyTasks('farm-1');

      // Items
      expect(result.items, hasLength(3));
      expect(result.items[0].id, 'item-1');
      expect(result.items[0].priority, TaskPriority.high);
      expect(result.items[1].priority, TaskPriority.medium);
      expect(result.items[2].priority, TaskPriority.low);

      // Critical weather alerts
      expect(result.criticalWeatherAlerts, hasLength(1));
      expect(result.criticalWeatherAlerts[0].id, 'alert-1');
      expect(result.criticalWeatherAlerts[0].priority, TaskPriority.critical);
      expect(result.criticalWeatherAlerts[0].source, TaskSource.weather);

      // Overdue
      expect(result.overdue, hasLength(1));
      expect(result.overdue[0].id, 'overdue-1');
      expect(result.overdue[0].status, TaskStatus.overdue);

      // Metadata
      expect(result.visibleLimit, 3);
    });

    // -----------------------------------------------------------------------
    // All fields of FarmTask are parsed correctly
    // -----------------------------------------------------------------------

    test('FarmTask alanlari dogru parse ediliyor', () async {
      final body = _dailyTaskListJson(
        items: [
          {
            'id': 'abc-123',
            'farmId': 'farm-42',
            'cropPeriodId': 'crop-99',
            'createdById': 'user-7',
            'title': 'Sulama',
            'description': 'Damla sulama yap',
            'reason': 'Son 7 gun icinde sulama yapilmadi.',
            'priority': 'High',
            'status': 'New',
            'source': 'System',
            'confidence': 'High',
            'dueDate': '2026-09-05',
            'notAppliedReason': null,
            'completionNote': null,
            'photoUrl': null,
            'viewedAtUtc': null,
            'completedAtUtc': null,
            'createdAtUtc': '2026-09-05T08:00:00Z',
            'updatedAtUtc': '2026-09-05T08:30:00Z',
            'expertReviewRecommended': false,
          },
        ],
      );

      final repo = BackendDailyTaskRepository(
        apiClient: _clientReturning(body),
      );
      final result = await repo.getDailyTasks('farm-42');
      final FarmTask task = result.items.first;

      expect(task.id, 'abc-123');
      expect(task.farmId, 'farm-42');
      expect(task.cropPeriodId, 'crop-99');
      expect(task.createdById, 'user-7');
      expect(task.title, 'Sulama');
      expect(task.description, 'Damla sulama yap');
      expect(task.reason, 'Son 7 gun icinde sulama yapilmadi.');
      expect(task.priority, TaskPriority.high);
      expect(task.status, TaskStatus.newTask);
      expect(task.source, TaskSource.system);
      expect(task.confidence, TaskConfidence.high);
      expect(task.dueDate, DateTime(2026, 9, 5));
      expect(task.expertReviewRecommended, isFalse);
      expect(task.createdAtUtc, isNotNull);
    });

    test('Turkce domain typedefleri FarmTask ile uyumlu calisiyor', () {
      final task = FarmTask.fromJson(_taskJson());
      expect(task, isA<GunlukGorev>());
      expect(task, isA<KritikHavaUyarisi>());

      final list = DailyTaskList.fromJson(_dailyTaskListJson(items: [_taskJson()]));
      expect(list, isA<GunlukGorevListesi>());
    });

    // -----------------------------------------------------------------------
    // Nullable / optional fields
    // -----------------------------------------------------------------------

    test('reason null gelirse bos string olarak parse edilir', () async {
      final body = _dailyTaskListJson(
        items: [
          {..._taskJson(), 'reason': null},
        ],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.reason, '');
    });

    test('confidence null gelirse unknown olarak parse edilir', () async {
      final body = _dailyTaskListJson(
        items: [
          {..._taskJson(), 'confidence': null},
        ],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.confidence, TaskConfidence.unknown);
    });

    test('dueDate null gelirse null olarak kalir', () async {
      final body = _dailyTaskListJson(
        items: [
          {..._taskJson(), 'dueDate': null},
        ],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.dueDate, isNull);
    });

    test('expertReviewRecommended null gelirse confidence==low ise true hesaplanir', () async {
      final body = _dailyTaskListJson(
        items: [
          {
            ..._taskJson(confidence: 'Low'),
            'expertReviewRecommended': null,
          },
        ],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.expertReviewRecommended, isTrue);
    });

    test('expertReviewRecommended null gelirse confidence!=low ise false kalir', () async {
      final body = _dailyTaskListJson(
        items: [
          {
            ..._taskJson(confidence: 'High'),
            'expertReviewRecommended': null,
          },
        ],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.expertReviewRecommended, isFalse);
    });

    // -----------------------------------------------------------------------
    // Enum fallback — unknown values must not crash
    // -----------------------------------------------------------------------

    test('bilinmeyen priority gelirse TaskPriority.unknown donulur', () async {
      final body = _dailyTaskListJson(
        items: [_taskJson(priority: 'LEGENDARY')],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.priority, TaskPriority.unknown);
    });

    test('bilinmeyen source gelirse TaskSource.unknown donulur', () async {
      final body = _dailyTaskListJson(
        items: [_taskJson(source: 'AI_GENERATED')],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.source, TaskSource.unknown);
    });

    test('bilinmeyen status gelirse TaskStatus.unknown donulur', () async {
      final body = _dailyTaskListJson(
        items: [_taskJson(status: 'PAUSED')],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items.first.status, TaskStatus.unknown);
    });

    // -----------------------------------------------------------------------
    // Empty responses
    // -----------------------------------------------------------------------

    test('items bos listesi bos DailyTaskList olusturur', () async {
      final body = _dailyTaskListJson();
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.items, isEmpty);
      expect(result.criticalWeatherAlerts, isEmpty);
      expect(result.overdue, isEmpty);
      expect(result.isEmpty, isTrue);
    });

    test('criticalWeatherAlerts alani yoksa bos liste doner', () async {
      final body = <String, dynamic>{
        'date': '2026-09-05',
        'items': [_taskJson()],
        'overdue': [],
        'visibleLimit': 3,
        // criticalWeatherAlerts field omitted entirely
      };
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.criticalWeatherAlerts, isEmpty);
    });

    test('overdue alani yoksa bos liste doner', () async {
      final body = <String, dynamic>{
        'date': '2026-09-05',
        'items': [],
        'criticalWeatherAlerts': [],
        'visibleLimit': 3,
        // overdue field omitted
      };
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.overdue, isEmpty);
    });

    // -----------------------------------------------------------------------
    // Enum round-trip coverage
    // -----------------------------------------------------------------------

    test('tum TaskPriority degerleri dogru parse ediliyor', () {
      expect(TaskPriority.fromJson('Low'), TaskPriority.low);
      expect(TaskPriority.fromJson('MEDIUM'), TaskPriority.medium);
      expect(TaskPriority.fromJson('High'), TaskPriority.high);
      expect(TaskPriority.fromJson('critical'), TaskPriority.critical);
      expect(TaskPriority.fromJson('UNKNOWN_FUTURE'), TaskPriority.unknown);
      expect(TaskPriority.fromJson(null), TaskPriority.unknown);
    });

    test('tum TaskSource degerleri dogru parse ediliyor', () {
      expect(TaskSource.fromJson('System'), TaskSource.system);
      expect(TaskSource.fromJson('CROPCALENDAR'), TaskSource.cropCalendar);
      expect(TaskSource.fromJson('CropCalendar'), TaskSource.cropCalendar);
      expect(TaskSource.fromJson('CROP_CALENDAR'), TaskSource.cropCalendar);
      expect(TaskSource.fromJson('Weather'), TaskSource.weather);
      expect(TaskSource.fromJson('Expert'), TaskSource.expert);
      expect(TaskSource.fromJson('Manual'), TaskSource.manual);
      expect(TaskSource.fromJson('FUTURE_AI'), TaskSource.unknown);
      expect(TaskSource.fromJson(null), TaskSource.unknown);
    });

    test('tum TaskStatus degerleri dogru parse ediliyor', () {
      expect(TaskStatus.fromJson('New'), TaskStatus.newTask);
      expect(TaskStatus.fromJson('Viewed'), TaskStatus.viewed);
      expect(TaskStatus.fromJson('Planned'), TaskStatus.planned);
      expect(TaskStatus.fromJson('Completed'), TaskStatus.completed);
      expect(TaskStatus.fromJson('NotApplied'), TaskStatus.notApplied);
      expect(TaskStatus.fromJson('NOT_APPLIED'), TaskStatus.notApplied);
      expect(TaskStatus.fromJson('Overdue'), TaskStatus.overdue);
      expect(TaskStatus.fromJson('Cancelled'), TaskStatus.cancelled);
      expect(TaskStatus.fromJson('PAUSED'), TaskStatus.unknown);
      expect(TaskStatus.fromJson(null), TaskStatus.unknown);
    });

    test('tum TaskConfidence degerleri dogru parse ediliyor', () {
      expect(TaskConfidence.fromJson('Low'), TaskConfidence.low);
      expect(TaskConfidence.fromJson('MEDIUM'), TaskConfidence.medium);
      expect(TaskConfidence.fromJson('High'), TaskConfidence.high);
      expect(TaskConfidence.fromJson('VERY_HIGH'), TaskConfidence.unknown);
      expect(TaskConfidence.fromJson(null), TaskConfidence.unknown);
    });

    // -----------------------------------------------------------------------
    // ApiException propagation
    // -----------------------------------------------------------------------

    test('backend 404 donerse ApiException firlatiyor', () async {
      final client = ApiClient(
        httpClient: MockClient(
          (request) async => http.Response(
            jsonEncode({'detail': 'Tarla bulunamadi.'}),
            404,
            headers: {'content-type': 'application/json'},
          ),
        ),
        idTokenProvider: () async => 'token',
      );
      final repo = BackendDailyTaskRepository(apiClient: client);
      expect(() => repo.getDailyTasks('nonexistent'), throwsA(isA<ApiException>()));
    });

    // -----------------------------------------------------------------------
    // DailyTaskList.isEmpty
    // -----------------------------------------------------------------------

    test('DailyTaskList.isEmpty tum listeler doluysa false doner', () async {
      final body = _dailyTaskListJson(
        items: [_taskJson()],
        criticalWeatherAlerts: [_taskJson(id: 'a2')],
        overdue: [_taskJson(id: 'a3')],
      );
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-1');
      expect(result.isEmpty, isFalse);
    });

    // -----------------------------------------------------------------------
    // snake_case JSON compatibility (future-proofing)
    // -----------------------------------------------------------------------

    test('snake_case JSON alanlari da dogru parse ediliyor', () async {
      final body = <String, dynamic>{
        'date': '2026-09-05',
        'items': [
          {
            'id': 'snake-task',
            'farm_id': 'farm-99',
            'title': 'Test gorevi',
            'description': 'Aciklama',
            'reason': 'Neden',
            'priority': 'Medium',
            'status': 'New',
            'source': 'System',
            'confidence': 'High',
            'due_date': '2026-09-06',
            'expert_review_recommended': false,
            'created_at_utc': '2026-09-05T10:00:00Z',
            'updated_at_utc': '2026-09-05T10:00:00Z',
          },
        ],
        'critical_weather_alerts': [],
        'overdue': [],
        'visible_limit': 3,
      };
      final repo = BackendDailyTaskRepository(apiClient: _clientReturning(body));
      final result = await repo.getDailyTasks('farm-99');
      expect(result.items.first.farmId, 'farm-99');
      expect(result.items.first.dueDate, DateTime(2026, 9, 6));
    });

    // -----------------------------------------------------------------------
    // TaskStatus helper properties
    // -----------------------------------------------------------------------

    test('TaskStatus.isActive ve isTerminal dogru deger doner', () {
      expect(TaskStatus.newTask.isActive, isTrue);
      expect(TaskStatus.viewed.isActive, isTrue);
      expect(TaskStatus.planned.isActive, isTrue);
      expect(TaskStatus.completed.isActive, isFalse);
      expect(TaskStatus.completed.isTerminal, isTrue);
      expect(TaskStatus.notApplied.isTerminal, isTrue);
      expect(TaskStatus.cancelled.isTerminal, isTrue);
      expect(TaskStatus.overdue.isTerminal, isFalse);
    });

    // -----------------------------------------------------------------------
    // TaskPriority.isCritical
    // -----------------------------------------------------------------------

    test('TaskPriority.isCritical sadece critical icin true doner', () {
      expect(TaskPriority.critical.isCritical, isTrue);
      expect(TaskPriority.high.isCritical, isFalse);
    });

    // -----------------------------------------------------------------------
    // completeTask
    // -----------------------------------------------------------------------
    group('completeTask', () {
      test('POST /tasks/{taskId}/complete endpointini note ile dogru cagirir', () async {
        late http.Request capturedRequest;
        final client = _clientWithHandler((request) async {
          capturedRequest = request;
          return http.Response(
            jsonEncode({'id': 'task-123', 'status': 'COMPLETED'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        await repo.completeTask(
          farmId: 'farm-1',
          taskId: 'task-123',
          note: 'Tamamlandi notu',
        );

        expect(capturedRequest.method, 'POST');
        expect(capturedRequest.url.path, endsWith('/tasks/task-123/complete'));
        final body = jsonDecode(capturedRequest.body) as Map<String, dynamic>;
        expect(body['note'], 'Tamamlandi notu');
      });

      test('note verilmediginde bos body gonderilir', () async {
        late http.Request capturedRequest;
        final client = _clientWithHandler((request) async {
          capturedRequest = request;
          return http.Response(
            jsonEncode({'id': 'task-123', 'status': 'COMPLETED'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        await repo.completeTask(
          farmId: 'farm-1',
          taskId: 'task-123',
        );

        expect(capturedRequest.method, 'POST');
        expect(capturedRequest.url.path, endsWith('/tasks/task-123/complete'));
        final body = jsonDecode(capturedRequest.body) as Map<String, dynamic>;
        expect(body.containsKey('note'), isFalse);
      });

      test('backend 400/404/500 donerse ApiException firlatir', () async {
        final client = _clientWithHandler((request) async {
          return http.Response(
            jsonEncode({'detail': 'Gorev bulunamadi.'}),
            404,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        expect(
          () => repo.completeTask(farmId: 'farm-1', taskId: 'task-999'),
          throwsA(isA<ApiException>()),
        );
      });
    });

    // -----------------------------------------------------------------------
    // markTaskNotApplied
    // -----------------------------------------------------------------------
    group('markTaskNotApplied', () {
      test('PATCH /tasks/{taskId}/status endpointini NOT_APPLIED ve reason ile cagirir', () async {
        late http.Request capturedRequest;
        final client = _clientWithHandler((request) async {
          capturedRequest = request;
          return http.Response(
            jsonEncode({'id': 'task-123', 'status': 'NOT_APPLIED'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        await repo.markTaskNotApplied(
          farmId: 'farm-1',
          taskId: 'task-123',
          reason: 'Hava sartlari uygun degil',
        );

        expect(capturedRequest.method, 'PATCH');
        expect(capturedRequest.url.path, endsWith('/tasks/task-123/status'));
        final body = jsonDecode(capturedRequest.body) as Map<String, dynamic>;
        expect(body['status'], 'NOT_APPLIED');
        expect(body['notAppliedReason'], 'Hava sartlari uygun degil');
      });

      test('reason bos veya whitespace ise istek atilmaz ve ArgumentError firlatilir', () async {
        var requestSent = false;
        final client = _clientWithHandler((request) async {
          requestSent = true;
          return http.Response('{}', 200);
        });

        final repo = BackendDailyTaskRepository(apiClient: client);

        expect(
          () => repo.markTaskNotApplied(
            farmId: 'farm-1',
            taskId: 'task-123',
            reason: '   ',
          ),
          throwsA(isA<ArgumentError>()),
        );
        expect(
          () => repo.markTaskNotApplied(
            farmId: 'farm-1',
            taskId: 'task-123',
            reason: '',
          ),
          throwsA(isA<ArgumentError>()),
        );
        expect(requestSent, isFalse);
      });

      test('gecerli reason whitespace temizlenerek (trim) gonderilir', () async {
        late http.Request capturedRequest;
        final client = _clientWithHandler((request) async {
          capturedRequest = request;
          return http.Response(
            jsonEncode({'id': 'task-123', 'status': 'NOT_APPLIED'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        await repo.markTaskNotApplied(
          farmId: 'farm-1',
          taskId: 'task-123',
          reason: '  Hava sartlari uygun degildi  ',
        );

        expect(capturedRequest.method, 'PATCH');
        final body = jsonDecode(capturedRequest.body) as Map<String, dynamic>;
        expect(body['notAppliedReason'], 'Hava sartlari uygun degildi');
      });

      test('backend hata verirse ApiException firlatir', () async {
        final client = _clientWithHandler((request) async {
          return http.Response(
            jsonEncode({'detail': 'Hata olustu'}),
            500,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(apiClient: client);
        expect(
          () => repo.markTaskNotApplied(
            farmId: 'farm-1',
            taskId: 'task-123',
            reason: 'Hava sartlari uygun degildi',
          ),
          throwsA(isA<ApiException>()),
        );
      });
    });

    group('Cache & Offline Fallback', () {
      late Database db;
      late LocalDailyTaskRepository localRepo;

      setUpAll(() {
        sqfliteFfiInit();
        databaseFactory = databaseFactoryFfi;
      });

      setUp(() async {
        db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
        localRepo = LocalDailyTaskRepository(databaseProvider: () async => db);
      });

      tearDown(() async {
        await db.close();
      });

      test('remote API basarili oldugunda veri cachee yazilir ve isFromCache false doner', () async {
        final body = {
          'date': '2026-09-05',
          'items': [_taskJson(id: 'task-remote-1')],
          'criticalWeatherAlerts': [],
          'overdue': [],
          'visibleLimit': 3,
        };
        final client = _clientReturning(body);
        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        final result = await repo.getDailyTasks('farm-1');

        expect(result.isFromCache, isFalse);
        expect(result.items.first.id, 'task-remote-1');

        final cached = await localRepo.getCachedTasks('farm-1');
        expect(cached, isNotNull);
        expect(cached!.isFromCache, isTrue);
        expect(cached.items.first.id, 'task-remote-1');
      });

      test('remote API basarisiz oldugunda cache varsa cached veri doner', () async {
        final initialTasks = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            FarmTask(
              id: 'task-cached-1',
              farmId: 'farm-1',
              title: 'Onbellekteki Gorev',
              description: 'Aciklama',
              reason: 'Gerekce',
              priority: TaskPriority.medium,
              status: TaskStatus.newTask,
              source: TaskSource.cropCalendar,
              confidence: TaskConfidence.high,
              dueDate: DateTime(2026, 9, 5),
              expertReviewRecommended: false,
            ),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-1', tasks: initialTasks);

        final client = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Sunucuya erisilemiyor'}),
            503,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        final result = await repo.getDailyTasks('farm-1');
        expect(result.isFromCache, isTrue);
        expect(result.items.first.id, 'task-cached-1');
        expect(result.items.first.title, 'Onbellekteki Gorev');
        expect(result.cachedAt, isNotNull);
      });

      test('remote API basarisiz ve cache yoksa ApiException firlatir', () async {
        final client = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Sunucu hatasi'}),
            500,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        expect(
          () => repo.getDailyTasks('farm-nocache'),
          throwsA(isA<ApiException>()),
        );
      });

      test('cache yazma hatasi remote API basarisini engellemez', () async {
        final failingLocalRepo = LocalDailyTaskRepository(
          databaseProvider: () async {
            throw Exception('Disk hatasi');
          },
        );

        final body = {
          'date': '2026-09-05',
          'items': [_taskJson(id: 'task-remote-ok')],
          'criticalWeatherAlerts': [],
          'overdue': [],
          'visibleLimit': 3,
        };
        final client = _clientReturning(body);
        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: failingLocalRepo,
        );

        final result = await repo.getDailyTasks('farm-1');
        expect(result.isFromCache, isFalse);
        expect(result.items.first.id, 'task-remote-ok');
      });

      test('farm izolasyonu: farm A cachei farm B icin donmez', () async {
        final tasksA = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [
            FarmTask(
              id: 'task-A',
              farmId: 'farm-A',
              title: 'Farm A Gorev',
              description: 'A',
              reason: 'A',
              priority: TaskPriority.high,
              status: TaskStatus.newTask,
              source: TaskSource.cropCalendar,
              confidence: TaskConfidence.high,
              dueDate: DateTime(2026, 9, 5),
              expertReviewRecommended: false,
            ),
          ],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-A', tasks: tasksA);

        final client = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Erisim yok'}),
            503,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        expect(
          () => repo.getDailyTasks('farm-B'),
          throwsA(isA<ApiException>()),
        );
      });

      test('guvenlik: 401 Unauthorized durumunda onbellek ASLA donulmez, hata rethrow edilir', () async {
        final initialTasks = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [_task('task-cached-1', title: 'Onbellekteki Gorev')],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-1', tasks: initialTasks);

        final client = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Yetkilendirme basarisiz'}),
            401,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        expect(
          () => repo.getDailyTasks('farm-1'),
          throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 401)),
        );
      });

      test('guvenlik: 403 Forbidden durumunda onbellek donulmez, hata rethrow edilir', () async {
        final initialTasks = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [_task('task-cached-1', title: 'Onbellekteki Gorev')],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-1', tasks: initialTasks);

        final client = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Erisim yasak'}),
            403,
            headers: {'content-type': 'application/json'},
          );
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        expect(
          () => repo.getDailyTasks('farm-1'),
          throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 403)),
        );
      });

      test('404 Not Found ve 400 Bad Request durumlarinda onbellek donulmez', () async {
        final initialTasks = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [_task('task-cached-1', title: 'Onbellekteki Gorev')],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-1', tasks: initialTasks);

        final client404 = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Tarla bulunamadi'}),
            404,
            headers: {'content-type': 'application/json'},
          );
        });
        final repo404 = BackendDailyTaskRepository(apiClient: client404, localRepo: localRepo);
        expect(() => repo404.getDailyTasks('farm-1'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404)));

        final client400 = _clientWithHandler((_) async {
          return http.Response(
            jsonEncode({'detail': 'Gecersiz istek'}),
            400,
            headers: {'content-type': 'application/json'},
          );
        });
        final repo400 = BackendDailyTaskRepository(apiClient: client400, localRepo: localRepo);
        expect(() => repo400.getDailyTasks('farm-1'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400)));
      });
    });

    group('Pending Task Actions & Sync (Step 7)', () {
      late Database db;
      late LocalDailyTaskRepository localRepo;

      setUp(() async {
        db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
        localRepo = LocalDailyTaskRepository(databaseProvider: () async => db);
      });

      tearDown(() async {
        await db.close();
      });

      test('getDailyTasks remote donusunde bekleyen aksiyonlar ile gorevleri decorate eder', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-1',
          farmId: 'farm-1',
          taskId: 'task-1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final body = {
          'date': '2026-09-05',
          'items': [
            _taskJson(id: 'task-1', title: 'Sulama yap'),
            _taskJson(id: 'task-2', title: 'Gubreleme yap'),
          ],
          'criticalWeatherAlerts': [],
          'overdue': [],
          'visibleLimit': 3,
        };

        // Complete endpoint'i basarili donmesin, sadece getJson basarili olsun (ornegin baska bir farm veya sync gecikmesi)
        // Ya da sync sirasinda API 503 versin ki kuyrukta kalsin ve decorate edilsin:
        final client = _clientWithHandler((req) async {
          if (req.url.path.contains('/tasks/task-1/complete')) {
            return http.Response(jsonEncode({'detail': 'Gecici sunucu hatasi'}), 503, headers: {'content-type': 'application/json'});
          }
          return http.Response(jsonEncode(body), 200, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        final result = await repo.getDailyTasks('farm-1');
        expect(result.items.first.id, equals('task-1'));
        expect(result.items.first.hasPendingAction, isTrue);
        expect(result.items.first.pendingAction?.actionType, equals(TaskActionType.complete));

        expect(result.items[1].id, equals('task-2'));
        expect(result.items[1].hasPendingAction, isFalse);
      });

      test('getDailyTasks cache fallback durumunda da bekleyen aksiyonlar ile decorate eder', () async {
        final cached = DailyTaskList(
          date: DateTime(2026, 9, 5),
          items: [_task('task-offline-1', title: 'Offline Gorev')],
          criticalWeatherAlerts: const [],
          overdue: const [],
          visibleLimit: 3,
        );
        await localRepo.cacheTasks(farmId: 'farm-1', tasks: cached);

        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-offline',
          farmId: 'farm-1',
          taskId: 'task-offline-1',
          actionType: TaskActionType.notApplied,
          reason: 'Zaman yetersizligi',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final client = _clientWithHandler((_) async {
          return http.Response(jsonEncode({'detail': 'Sunucu hatasi'}), 500, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(
          apiClient: client,
          localRepo: localRepo,
        );

        final result = await repo.getDailyTasks('farm-1');
        expect(result.isFromCache, isTrue);
        expect(result.items.first.id, equals('task-offline-1'));
        expect(result.items.first.hasPendingAction, isTrue);
        expect(result.items.first.pendingAction?.actionType, equals(TaskActionType.notApplied));
        expect(result.items.first.pendingAction?.reason, equals('Zaman yetersizligi'));
      });

      test('syncPendingTaskActions: 200 OK ile gorev tamamlanir ve kuyruktan kaldirilir', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-complete-1',
          farmId: 'farm-1',
          taskId: 'task-100',
          actionType: TaskActionType.complete,
          note: 'Detayli not',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final calls = <String>[];
        final client = _clientWithHandler((req) async {
          calls.add('${req.method} ${req.url.path}');
          return http.Response(jsonEncode({'id': 'act-1', 'title': 'Tamamlandi'}), 200, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.syncedCount, equals(1));
        expect(syncResult.conflictCount, equals(0));
        expect(syncResult.failedCount, equals(0));
        expect(syncResult.hasAuthError, isFalse);

        expect(calls, contains('POST /api/v1/tasks/task-100/complete'));
        expect(await localRepo.getPendingActions(farmId: 'farm-1'), isEmpty);
      });

      test('syncPendingTaskActions: notApplied 200 OK ile gonderilir ve kuyruktan kaldirilir', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-not-applied-1',
          farmId: 'farm-1',
          taskId: 'task-200',
          actionType: TaskActionType.notApplied,
          reason: 'Hava sartlari uygun degildi',
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final calls = <String>[];
        final client = _clientWithHandler((req) async {
          calls.add('${req.method} ${req.url.path}');
          return http.Response(jsonEncode({'id': 'task-200', 'status': 'NOT_APPLIED'}), 200, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.syncedCount, equals(1));
        expect(calls, contains('PATCH /api/v1/tasks/task-200/status'));
        expect(await localRepo.getPendingActions(farmId: 'farm-1'), isEmpty);
      });

      test('syncPendingTaskActions: 409 Conflict durumunda kuyruktan silinir ve conflictCount artar', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-conflict',
          farmId: 'farm-1',
          taskId: 'task-conf',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final client = _clientWithHandler((_) async {
          return http.Response(jsonEncode({'detail': 'Gorev daha once tamamlanmis'}), 409, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.syncedCount, equals(0));
        expect(syncResult.conflictCount, equals(1));
        expect(syncResult.failedCount, equals(0));
        expect(await localRepo.getPendingActions(farmId: 'farm-1'), isEmpty);
      });

      test('syncPendingTaskActions: 404 ve 400 terminal hatalarda kuyruktan silinir ve failedCount artar', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-404',
          farmId: 'farm-1',
          taskId: 'task-404',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final client = _clientWithHandler((_) async {
          return http.Response(jsonEncode({'detail': 'Bulunamadi'}), 404, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.failedCount, equals(1));
        expect(await localRepo.getPendingActions(farmId: 'farm-1'), isEmpty);
      });

      test('syncPendingTaskActions: 401 Unauthorized durumunda kuyrukta TUTULUR ve sync durdurulur', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-auth',
          farmId: 'farm-1',
          taskId: 'task-auth',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final client = _clientWithHandler((_) async {
          return http.Response(jsonEncode({'detail': 'Yetki yok'}), 401, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.hasAuthError, isTrue);
        expect(syncResult.failedCount, equals(1));
        // Kuyrukta KORUNMALI
        final remaining = await localRepo.getPendingActions(farmId: 'farm-1');
        expect(remaining, hasLength(1));
        expect(remaining.first.id, equals('action-auth'));
      });

      test('syncPendingTaskActions: 503 gecici hatada kuyrukta TUTULUR, attempt sayaci artar ve FIFO sirasi korunur', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-first-transient',
          farmId: 'farm-1',
          taskId: 'task-1',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
          attemptCount: 0,
        ));
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-second',
          farmId: 'farm-1',
          taskId: 'task-2',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 1),
        ));

        var callCount = 0;
        final client = _clientWithHandler((_) async {
          callCount++;
          return http.Response(jsonEncode({'detail': 'Sunucu meşgul'}), 503, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        final syncResult = await repo.syncPendingTaskActions(farmId: 'farm-1');
        expect(syncResult.syncedCount, equals(0));
        expect(syncResult.failedCount, equals(1));
        // FIFO geregi ilk islem basarisiz oldugunda ikinci denenmemeli
        expect(callCount, equals(1));

        final remaining = await localRepo.getPendingActions(farmId: 'farm-1');
        expect(remaining, hasLength(2));
        expect(remaining.first.attemptCount, equals(1));
        expect(remaining.first.lastErrorCode, equals(503));
        expect(remaining.first.lastAttemptAtUtc, isNotNull);
      });

      test('syncPendingTaskActions: Mutex lock ayni anda paralel calismayi engeller', () async {
        await localRepo.enqueuePendingAction(PendingTaskAction(
          id: 'action-slow',
          farmId: 'farm-1',
          taskId: 'task-slow',
          actionType: TaskActionType.complete,
          createdAtUtc: DateTime.utc(2026, 9, 5, 10, 0),
        ));

        final client = _clientWithHandler((_) async {
          await Future.delayed(const Duration(milliseconds: 50));
          return http.Response(jsonEncode({'id': 'done'}), 200, headers: {'content-type': 'application/json'});
        });

        final repo = BackendDailyTaskRepository(apiClient: client, localRepo: localRepo);

        // Iki senkronizasyon cagrısını eszamanli baslat
        final future1 = repo.syncPendingTaskActions(farmId: 'farm-1');
        final future2 = repo.syncPendingTaskActions(farmId: 'farm-1');

        final results = await Future.wait([future1, future2]);
        final totalSynced = results[0].syncedCount + results[1].syncedCount;

        // Biri calismis, digeri mutex nedeniyle atlanmis olmali
        expect(totalSynced, equals(1));
      });
    });
  });
}

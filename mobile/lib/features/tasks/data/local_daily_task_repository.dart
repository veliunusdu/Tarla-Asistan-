import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';

import '../../../services/database_helper.dart';
import '../domain/farm_task.dart';
import '../domain/pending_task_action.dart';

/// Günlük görevleri ve çevrimdışı bekleyen aksiyonları SQLite yerel veri tabanında
/// farm bazlı önbelleğe alan repository sınıfı.
///
/// Projedeki [LocalWeatherRepository] desenini takip eder; kendi tablolarını lazily
/// `_ensureTable` ile yönetir ve test edilebilirlik için opsiyonel `databaseProvider` kabul eder.
class LocalDailyTaskRepository {
  const LocalDailyTaskRepository({Future<Database> Function()? databaseProvider})
      : _dbProvider = databaseProvider;

  final Future<Database> Function()? _dbProvider;

  Future<Database> get _database async {
    final provider = _dbProvider;
    if (provider != null) return provider();
    return DatabaseHelper.instance.database;
  }

  static const String tableName = 'daily_tasks_cache';
  static const String pendingActionsTableName = 'pending_task_actions';

  static const String sqlCreateTable = '''
    CREATE TABLE IF NOT EXISTS $tableName (
      farm_id TEXT PRIMARY KEY,
      cached_at_utc TEXT NOT NULL,
      payload_json TEXT NOT NULL
    )
  ''';

  static const String sqlCreatePendingActionsTable = '''
    CREATE TABLE IF NOT EXISTS $pendingActionsTableName (
      id TEXT PRIMARY KEY,
      farm_id TEXT NOT NULL,
      task_id TEXT NOT NULL,
      action_type TEXT NOT NULL,
      reason TEXT,
      note TEXT,
      created_at_utc TEXT NOT NULL,
      attempt_count INTEGER NOT NULL DEFAULT 0,
      last_attempt_at_utc TEXT,
      last_error_code INTEGER,
      user_id TEXT
    )
  ''';

  static const String sqlCreatePendingActionsTaskIndex = '''
    CREATE UNIQUE INDEX IF NOT EXISTS ux_pending_task_actions_farm_task
    ON $pendingActionsTableName(farm_id, task_id)
  ''';

  static const String sqlCreatePendingActionsCreatedIndex = '''
    CREATE INDEX IF NOT EXISTS ix_pending_task_actions_created
    ON $pendingActionsTableName(created_at_utc)
  ''';

  static const String sqlCreatePendingActionsUserIndex = '''
    CREATE INDEX IF NOT EXISTS ix_pending_task_actions_user_id
    ON $pendingActionsTableName(user_id)
  ''';

  Future<void> _ensureTable(Database db) async {
    try {
      await db.execute(sqlCreateTable);
      await db.execute(sqlCreatePendingActionsTable);
      await db.execute(sqlCreatePendingActionsTaskIndex);
      await db.execute(sqlCreatePendingActionsCreatedIndex);
      await db.execute(sqlCreatePendingActionsUserIndex);
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: _ensureTable error: $e');
    }
  }

  // ---------------------------------------------------------------------------
  // DailyTaskList Cache
  // ---------------------------------------------------------------------------

  /// Belirtilen [farmId] için yerel önbellekte saklanan [DailyTaskList] verisini döner.
  Future<DailyTaskList?> getCachedTasks(String farmId) async {
    if (farmId.isEmpty) return null;
    try {
      final db = await _database;
      await _ensureTable(db);

      final maps = await db.query(
        tableName,
        where: 'farm_id = ?',
        whereArgs: [farmId],
        limit: 1,
      );

      if (maps.isEmpty) return null;

      final row = maps.first;
      final cachedAtStr = row['cached_at_utc'] as String?;
      final payloadJson = row['payload_json'] as String?;

      if (payloadJson == null || payloadJson.isEmpty) return null;

      final decoded = jsonDecode(payloadJson);
      final Map<String, dynamic> map;
      if (decoded is Map<String, dynamic>) {
        map = decoded;
      } else if (decoded is Map) {
        map = Map<String, dynamic>.from(decoded);
      } else {
        return null;
      }

      final taskList = DailyTaskList.fromJson(map);
      final cachedAt = cachedAtStr != null
          ? DateTime.tryParse(cachedAtStr)?.toLocal()
          : null;

      return taskList.copyWith(
        isFromCache: true,
        cachedAt: cachedAt ?? taskList.cachedAt,
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: getCachedTasks error: $e');
      return null;
    }
  }

  /// [farmId] için [tasks] verisini yerel önbelleğe kaydeder.
  Future<void> cacheTasks({
    required String farmId,
    required DailyTaskList tasks,
    DateTime? cachedAt,
  }) async {
    if (farmId.isEmpty) return;
    try {
      final db = await _database;
      await _ensureTable(db);

      final nowUtc = (cachedAt ?? DateTime.now()).toUtc();
      final jsonMap = tasks.toJson();
      jsonMap['cachedAt'] = nowUtc.toIso8601String();
      jsonMap['isFromCache'] = true;

      await db.insert(
        tableName,
        {
          'farm_id': farmId,
          'cached_at_utc': nowUtc.toIso8601String(),
          'payload_json': jsonEncode(jsonMap),
        },
        conflictAlgorithm: ConflictAlgorithm.replace,
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: cacheTasks error: $e');
    }
  }

  /// Belirli bir [farmId]'ye ait görev önbelleğini siler.
  Future<void> clearCache(String farmId) async {
    if (farmId.isEmpty) return;
    try {
      final db = await _database;
      await _ensureTable(db);
      await db.delete(
        tableName,
        where: 'farm_id = ?',
        whereArgs: [farmId],
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: clearCache error: $e');
    }
  }

  // ---------------------------------------------------------------------------
  // PendingTaskAction Queue
  // ---------------------------------------------------------------------------

  /// Yeni bir çevrimdışı görev aksiyonunu kuyruğa ekler.
  ///
  /// Aynı görev için bekleyen bir işlem zaten varsa [StateError] fırlatır.
  Future<void> enqueuePendingAction(PendingTaskAction action) async {
    final db = await _database;
    await _ensureTable(db);

    final actionUserId = action.userId ?? DatabaseHelper.instance.currentUserId;
    final effectiveAction = (action.userId == null && actionUserId != null)
        ? action.copyWith(userId: actionUserId)
        : action;

    final existing = await getPendingActionForTask(
      effectiveAction.taskId,
      farmId: effectiveAction.farmId,
      userId: effectiveAction.userId,
    );
    if (existing != null) {
      throw StateError('Bu görev için zaten bekleyen bir işlem var.');
    }

    await db.insert(
      pendingActionsTableName,
      effectiveAction.toMap(),
      conflictAlgorithm: ConflictAlgorithm.abort,
    );
  }

  /// Belirli bir [taskId] için bekleyen aksiyonu döner.
  Future<PendingTaskAction?> getPendingActionForTask(
    String taskId, {
    String? farmId,
    String? userId,
  }) async {
    if (taskId.isEmpty) return null;
    try {
      final db = await _database;
      await _ensureTable(db);

      final conditions = <String>['task_id = ?'];
      final args = <Object?>[taskId];

      if (farmId != null && farmId.isNotEmpty) {
        conditions.add('farm_id = ?');
        args.add(farmId);
      }
      final activeUserId = userId ?? DatabaseHelper.instance.currentUserId;
      if (activeUserId != null && activeUserId.isNotEmpty) {
        conditions.add('user_id = ?');
        args.add(activeUserId);
      }

      final rows = await db.query(
        pendingActionsTableName,
        where: conditions.join(' AND '),
        whereArgs: args,
        limit: 1,
      );

      if (rows.isEmpty) return null;
      return PendingTaskAction.fromMap(rows.first);
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: getPendingActionForTask error: $e');
      return null;
    }
  }

  /// Belirtilen [farmId] (ve opsiyonel [userId]) için bekleyen tüm aksiyonları FIFO (eski -> yeni) sırasıyla döner.
  Future<List<PendingTaskAction>> getPendingActions({
    String? farmId,
    String? userId,
  }) async {
    try {
      final db = await _database;
      await _ensureTable(db);

      final conditions = <String>[];
      final args = <Object?>[];

      if (farmId != null && farmId.isNotEmpty) {
        conditions.add('farm_id = ?');
        args.add(farmId);
      }
      final activeUserId = userId ?? DatabaseHelper.instance.currentUserId;
      if (activeUserId != null && activeUserId.isNotEmpty) {
        conditions.add('user_id = ?');
        args.add(activeUserId);
      }

      final rows = await db.query(
        pendingActionsTableName,
        where: conditions.isNotEmpty ? conditions.join(' AND ') : null,
        whereArgs: args.isNotEmpty ? args : null,
        orderBy: 'created_at_utc ASC',
      );

      return rows.map((r) => PendingTaskAction.fromMap(r)).toList();
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: getPendingActions error: $e');
      return const [];
    }
  }

  /// [farmId] için bekleyen aksiyonları taskId'ye göre harita (map) olarak döner.
  Future<Map<String, PendingTaskAction>> getPendingActionsMap({
    required String farmId,
    String? userId,
  }) async {
    final list = await getPendingActions(farmId: farmId, userId: userId);
    return {for (final a in list) a.taskId: a};
  }

  /// Senkronize edilen veya iptal edilen aksiyonu kuyruktan siler.
  Future<void> removePendingAction(String id) async {
    if (id.isEmpty) return;
    try {
      final db = await _database;
      await _ensureTable(db);
      await db.delete(
        pendingActionsTableName,
        where: 'id = ?',
        whereArgs: [id],
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: removePendingAction error: $e');
    }
  }

  /// Başarısız deneme sonrası aksiyonun deneme sayısını ve hata kodunu günceller.
  Future<void> updatePendingAction(PendingTaskAction action) async {
    try {
      final db = await _database;
      await _ensureTable(db);
      await db.update(
        pendingActionsTableName,
        action.toMap(),
        where: 'id = ?',
        whereArgs: [action.id],
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: updatePendingAction error: $e');
    }
  }

  /// Bekleyen aksiyonları temizler.
  Future<void> clearPendingActions({String? farmId, String? userId}) async {
    try {
      final db = await _database;
      await _ensureTable(db);

      final conditions = <String>[];
      final args = <Object?>[];

      if (farmId != null && farmId.isNotEmpty) {
        conditions.add('farm_id = ?');
        args.add(farmId);
      }
      final activeUserId = userId ?? DatabaseHelper.instance.currentUserId;
      if (activeUserId != null && activeUserId.isNotEmpty) {
        conditions.add('user_id = ?');
        args.add(activeUserId);
      }

      await db.delete(
        pendingActionsTableName,
        where: conditions.isNotEmpty ? conditions.join(' AND ') : null,
        whereArgs: args.isNotEmpty ? args : null,
      );
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: clearPendingActions error: $e');
    }
  }

  /// Tüm önbelleği ve bekleyen kuyruk işlemlerini siler.
  Future<void> clearAll() async {
    try {
      final db = await _database;
      await _ensureTable(db);
      await db.delete(tableName);
      await db.delete(pendingActionsTableName);
    } catch (e) {
      debugPrint('LocalDailyTaskRepository: clearAll error: $e');
    }
  }
}

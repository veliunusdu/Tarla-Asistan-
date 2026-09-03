import 'dart:convert';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';

import '../core/database/migrations.dart';
import '../models/faaliyet.dart';
import '../models/sync_operation.dart';
import '../models/tarla.dart';

class DatabaseHelper implements SyncOperationStore {
  static final DatabaseHelper instance = DatabaseHelper._init();
  static Database? _database;

  DatabaseHelper._init();

  DatabaseHelper.withDatabase(
    Database db, {
    String? Function()? userIdProvider,
  })  : _providedDatabase = db,
        currentUserIdProvider = userIdProvider;

  Database? _providedDatabase;
  String? Function()? currentUserIdProvider;

  String? get currentUserId {
    if (currentUserIdProvider != null) {
      return currentUserIdProvider!();
    }
    try {
      return FirebaseAuth.instance.currentUser?.uid;
    } catch (_) {
      return null;
    }
  }

  Future<Database> get database async {
    if (_providedDatabase != null) return _providedDatabase!;
    if (_database != null) return _database!;
    _database = await _initDB('tarla_asistani.db');
    return _database!;
  }

  Future<Database> _initDB(String filePath) async {
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, filePath);

    return await openDatabase(
      path,
      version: 5,
      onConfigure: (db) => db.execute('PRAGMA foreign_keys = ON'),
      onCreate: _createDB,
      onUpgrade: _upgradeDB,
    );
  }

  Future<void> _createDB(Database db, int version) async {
    await db.execute('''
      CREATE TABLE tarlalar (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        latitude REAL,
        longitude REAL,
        size REAL,
        cropType TEXT,
        plantingDate TEXT,
        userId TEXT
      )
    ''');
    await db.execute('''
      CREATE INDEX IF NOT EXISTS ix_tarlalar_user_id
      ON tarlalar(userId)
    ''');

    await db.execute('''
      CREATE TABLE faaliyetler (
        id TEXT PRIMARY KEY,
        tarlaId TEXT NOT NULL,
        type TEXT NOT NULL,
        note TEXT,
        audioPath TEXT,
        photos TEXT,
        timestamp TEXT NOT NULL,
        dueDate TEXT,
        isCompleted INTEGER NOT NULL DEFAULT 1,
        userId TEXT
      )
    ''');
    await db.execute('''
      CREATE INDEX IF NOT EXISTS ix_faaliyetler_user_id
      ON faaliyetler(userId)
    ''');

    await _createSyncOperationsTable(db);
  }

  Future<void> _upgradeDB(Database db, int oldVersion, int newVersion) async {
    if (oldVersion < 2) {
      await Migrations.v1ToV2(db);
    }
    if (oldVersion < 3) {
      await _createSyncOperationsTable(db);
    }
    if (oldVersion < 4) {
      await Migrations.v3ToV4(db);
    }
    if (oldVersion < 5) {
      await Migrations.v4ToV5(db);
    }
  }

  Future<void> _createSyncOperationsTable(Database db) async {
    await db.execute('''
      CREATE TABLE IF NOT EXISTS sync_operations (
        id TEXT PRIMARY KEY,
        method TEXT NOT NULL,
        endpoint TEXT NOT NULL,
        payload TEXT NOT NULL,
        attempts INTEGER NOT NULL DEFAULT 0,
        lastError TEXT,
        createdAt TEXT NOT NULL,
        updatedAt TEXT NOT NULL,
        userId TEXT
      )
    ''');
    await db.execute('''
      CREATE INDEX IF NOT EXISTS ix_sync_operations_created
      ON sync_operations(createdAt)
    ''');
    await db.execute('''
      CREATE INDEX IF NOT EXISTS ix_sync_operations_user_id
      ON sync_operations(userId)
    ''');
  }

  // --- TARLA METODLARI ---

  Future<int> insertTarla(Tarla tarla, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final map = Map<String, dynamic>.from(tarla.toJson());
    if (activeUserId != null) {
      map['userId'] = activeUserId;
    }
    return await db.insert(
      'tarlalar',
      map,
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<Tarla>> getTarlalar({String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, dynamic>> result;
    if (activeUserId != null) {
      result = await db.query(
        'tarlalar',
        where: 'userId = ?',
        whereArgs: [activeUserId],
      );
    } else {
      result = await db.query(
        'tarlalar',
        where: 'userId IS NULL',
      );
    }
    return result.map((json) => Tarla.fromJson(json)).toList();
  }

  Future<int> updateTarlaLocation(
    String id,
    double latitude,
    double longitude, {
    String? userId,
  }) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    return await db.update(
      'tarlalar',
      {'latitude': latitude, 'longitude': longitude},
      where: activeUserId != null ? 'id = ? AND userId = ?' : 'id = ?',
      whereArgs: activeUserId != null ? [id, activeUserId] : [id],
    );
  }

  Future<int> deleteTarla(String id, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    return db.delete(
      'tarlalar',
      where: activeUserId != null ? 'id = ? AND userId = ?' : 'id = ?',
      whereArgs: activeUserId != null ? [id, activeUserId] : [id],
    );
  }

  Future<void> upsertTarlalar(List<Tarla> tarlalar, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    await db.transaction((txn) async {
      for (final tarla in tarlalar) {
        final map = Map<String, dynamic>.from(tarla.toJson());
        if (activeUserId != null) {
          map['userId'] = activeUserId;
        }
        await txn.insert(
          'tarlalar',
          map,
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    });
  }

  // --- FAALİYET METODLARI ---

  Future<int> insertFaaliyet(Faaliyet faaliyet, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final map = Map<String, dynamic>.from(faaliyet.toJson());
    if (activeUserId != null) {
      map['userId'] = activeUserId;
    }
    return await db.insert('faaliyetler', map);
  }

  Future<void> insertFaaliyetWithSync(Faaliyet faaliyet, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final now = DateTime.now().toUtc().toIso8601String();
    await db.transaction((txn) async {
      final faaliyetMap = Map<String, dynamic>.from(faaliyet.toJson());
      if (activeUserId != null) {
        faaliyetMap['userId'] = activeUserId;
      }
      await txn.insert(
        'faaliyetler',
        faaliyetMap,
        conflictAlgorithm: ConflictAlgorithm.ignore,
      );
      await txn.insert('sync_operations', {
        'id': faaliyet.id,
        'method': 'POST',
        'endpoint': '/farms/${faaliyet.tarlaId}/activities',
        'payload': jsonEncode({
          'client_operation_id': faaliyet.id,
          'activity_type': 'OTHER',
          'description': faaliyet.type,
          'occurred_at': faaliyet.timestamp.toUtc().toIso8601String(),
          'input_method': faaliyet.inputMethod,
        }),
        'attempts': 0,
        'createdAt': now,
        'updatedAt': now,
        'userId': activeUserId,
      }, conflictAlgorithm: ConflictAlgorithm.ignore);
    });
  }

  Future<List<Faaliyet>> getFaaliyetler(String tarlaId, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, dynamic>> result;
    if (activeUserId != null) {
      result = await db.query(
        'faaliyetler',
        where: 'tarlaId = ? AND userId = ?',
        whereArgs: [tarlaId, activeUserId],
        orderBy: 'timestamp DESC',
      );
    } else {
      result = await db.query(
        'faaliyetler',
        where: 'tarlaId = ? AND userId IS NULL',
        whereArgs: [tarlaId],
        orderBy: 'timestamp DESC',
      );
    }
    return result.map((json) => Faaliyet.fromJson(json)).toList();
  }

  Future<List<Faaliyet>> getTumFaaliyetler({String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, dynamic>> result;
    if (activeUserId != null) {
      result = await db.query(
        'faaliyetler',
        where: 'userId = ?',
        whereArgs: [activeUserId],
      );
    } else {
      result = await db.query(
        'faaliyetler',
        where: 'userId IS NULL',
      );
    }
    return result.map((json) => Faaliyet.fromJson(json)).toList();
  }

  // --- SyncOperationStore ---

  @override
  Future<List<SyncOperation>> getPendingSyncOperations({
    int limit = 20,
    String? userId,
  }) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, dynamic>> rows;
    if (activeUserId != null) {
      rows = await db.query(
        'sync_operations',
        where: 'userId = ?',
        whereArgs: [activeUserId],
        orderBy: 'createdAt ASC',
        limit: limit,
      );
    } else {
      rows = await db.query(
        'sync_operations',
        where: 'userId IS NULL',
        orderBy: 'createdAt ASC',
        limit: limit,
      );
    }
    return rows.map(SyncOperation.fromJson).toList();
  }

  @override
  Future<int> getPendingSyncCount({String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, Object?>> result;
    if (activeUserId != null) {
      result = await db.rawQuery(
        'SELECT COUNT(*) FROM sync_operations WHERE userId = ?',
        [activeUserId],
      );
    } else {
      result = await db.rawQuery(
        'SELECT COUNT(*) FROM sync_operations WHERE userId IS NULL',
      );
    }
    return Sqflite.firstIntValue(result) ?? 0;
  }

  @override
  Future<void> markSyncCompleted(String id) async {
    final db = await database;
    await db.delete('sync_operations', where: 'id = ?', whereArgs: [id]);
  }

  @override
  Future<void> markSyncFailed(String id, String error) async {
    final db = await database;
    await db.rawUpdate(
      '''
      UPDATE sync_operations
      SET attempts = attempts + 1, lastError = ?, updatedAt = ?
      WHERE id = ?
      ''',
      [
        error.length > 500 ? error.substring(0, 500) : error,
        DateTime.now().toUtc().toIso8601String(),
        id,
      ],
    );
  }

  // --- İSTATİSTİK SORGULARI ---

  Future<int> getTarlaSayisi({String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, Object?>> result;
    if (activeUserId != null) {
      result = await db.rawQuery(
        'SELECT COUNT(*) FROM tarlalar WHERE userId = ?',
        [activeUserId],
      );
    } else {
      result = await db.rawQuery(
        'SELECT COUNT(*) FROM tarlalar WHERE userId IS NULL',
      );
    }
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<double> getToplamDonum({String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    final List<Map<String, Object?>> result;
    if (activeUserId != null) {
      result = await db.rawQuery(
        'SELECT SUM(size) as total FROM tarlalar WHERE userId = ?',
        [activeUserId],
      );
    } else {
      result = await db.rawQuery(
        'SELECT SUM(size) as total FROM tarlalar WHERE userId IS NULL',
      );
    }
    return (result.first['total'] as num?)?.toDouble() ?? 0.0;
  }

  // --- SİLME VE TEMİZLEME İŞLEMLERİ ---

  Future<int> deleteFaaliyet(String id, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    return await db.delete(
      'faaliyetler',
      where: activeUserId != null ? 'id = ? AND userId = ?' : 'id = ?',
      whereArgs: activeUserId != null ? [id, activeUserId] : [id],
    );
  }

  Future<int> completePlanliGorev(String id, {String? userId}) async {
    final db = await database;
    final activeUserId = userId ?? currentUserId;
    return db.update(
      'faaliyetler',
      {'isCompleted': 1, 'timestamp': DateTime.now().toIso8601String()},
      where: activeUserId != null ? 'id = ? AND userId = ?' : 'id = ?',
      whereArgs: activeUserId != null ? [id, activeUserId] : [id],
    );
  }

  Future<OrphanedDataSummary> getOrphanedDataSummary() async {
    if (kIsWeb) {
      return const OrphanedDataSummary(
        farmCount: 0,
        activityCount: 0,
        syncCount: 0,
      );
    }
    try {
      final db = await database;
      final farmRows = Sqflite.firstIntValue(
        await db.rawQuery('SELECT COUNT(*) FROM tarlalar WHERE userId IS NULL'),
      ) ?? 0;
      final activityRows = Sqflite.firstIntValue(
        await db.rawQuery('SELECT COUNT(*) FROM faaliyetler WHERE userId IS NULL'),
      ) ?? 0;
      final syncRows = Sqflite.firstIntValue(
        await db.rawQuery('SELECT COUNT(*) FROM sync_operations WHERE userId IS NULL'),
      ) ?? 0;

      return OrphanedDataSummary(
        farmCount: farmRows,
        activityCount: activityRows,
        syncCount: syncRows,
      );
    } catch (_) {
      return const OrphanedDataSummary(
        farmCount: 0,
        activityCount: 0,
        syncCount: 0,
      );
    }
  }

  /// Sahipsiz (userId IS NULL) kayıtları kontrollü şekilde siler.
  ///
  /// Güvenlik Politikası: Cihazda önceki kullanıcıdan kalan veya doğrulanmamış
  /// kayıtlar başka kullanıcıların hesaplarına aktarılamaz (anti-hijacking).
  /// Yalnızca cihazdan güvenle temizlenebilir.
  Future<int> clearOrphanedRecords() async {
    if (kIsWeb) return 0;
    try {
      final db = await database;
      var deleted = 0;
      await db.transaction((txn) async {
        deleted += await txn.delete('tarlalar', where: 'userId IS NULL');
        deleted += await txn.delete('faaliyetler', where: 'userId IS NULL');
        deleted += await txn.delete('sync_operations', where: 'userId IS NULL');
      });
      return deleted;
    } catch (_) {
      return 0;
    }
  }

  /// Kullanıcı oturumu kapattığında yerel verilerini, bekleyen sync işlemlerini
  /// ve cihazda kalmış sahipsiz karantina verilerini tamamen temizler.
  Future<void> clearUserData({String? userId}) async {
    if (kIsWeb) return;
    try {
      final activeUserId = userId ?? currentUserId;
      final db = await database;
      await db.transaction((txn) async {
        if (activeUserId != null) {
          await txn.delete('tarlalar', where: 'userId = ?', whereArgs: [activeUserId]);
          await txn.delete('faaliyetler', where: 'userId = ?', whereArgs: [activeUserId]);
          await txn.delete('sync_operations', where: 'userId = ?', whereArgs: [activeUserId]);
        } else {
          await txn.delete('tarlalar');
          await txn.delete('faaliyetler');
          await txn.delete('sync_operations');
        }
        // Sahipsiz karantina verilerini de temizle (at-rest güvenlik)
        await txn.delete('tarlalar', where: 'userId IS NULL');
        await txn.delete('faaliyetler', where: 'userId IS NULL');
        await txn.delete('sync_operations', where: 'userId IS NULL');
      });
    } catch (_) {
      // Ignored: Non-fatal on web or if DB is inaccessible
    }
  }
}

class OrphanedDataSummary {
  const OrphanedDataSummary({
    required this.farmCount,
    required this.activityCount,
    required this.syncCount,
  });

  final int farmCount;
  final int activityCount;
  final int syncCount;

  bool get hasOrphanedData => farmCount > 0 || activityCount > 0 || syncCount > 0;
}

import 'dart:convert';

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

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDB('tarla_asistani.db');
    return _database!;
  }

  Future<Database> _initDB(String filePath) async {
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, filePath);

    return await openDatabase(
      path,
      version: 4,
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
        plantingDate TEXT
      )
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
        isCompleted INTEGER NOT NULL DEFAULT 1
      )
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
        updatedAt TEXT NOT NULL
      )
    ''');
    await db.execute('''
      CREATE INDEX IF NOT EXISTS ix_sync_operations_created
      ON sync_operations(createdAt)
    ''');
  }

  // --- TARLA METODLARI ---

  Future<int> insertTarla(Tarla tarla) async {
    final db = await instance.database;
    return await db.insert(
      'tarlalar',
      tarla.toJson(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<Tarla>> getTarlalar() async {
    final db = await instance.database;
    final List<Map<String, dynamic>> result = await db.query('tarlalar');
    return result.map((json) => Tarla.fromJson(json)).toList();
  }

  Future<void> upsertTarlalar(List<Tarla> tarlalar) async {
    final db = await instance.database;
    await db.transaction((txn) async {
      for (final tarla in tarlalar) {
        await txn.insert(
          'tarlalar',
          tarla.toJson(),
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    });
  }

  // --- FAALİYET METODLARI ---

  Future<int> insertFaaliyet(Faaliyet faaliyet) async {
    final db = await instance.database;
    return await db.insert('faaliyetler', faaliyet.toJson());
  }

  Future<void> insertFaaliyetWithSync(Faaliyet faaliyet) async {
    final db = await instance.database;
    final now = DateTime.now().toUtc().toIso8601String();
    await db.transaction((txn) async {
      await txn.insert(
        'faaliyetler',
        faaliyet.toJson(),
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
      }, conflictAlgorithm: ConflictAlgorithm.ignore);
    });
  }

  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async {
    final db = await instance.database;
    final List<Map<String, dynamic>> result = await db.query(
      'faaliyetler',
      where: 'tarlaId = ?',
      whereArgs: [tarlaId],
      orderBy: 'timestamp DESC',
    );
    return result.map((json) => Faaliyet.fromJson(json)).toList();
  }

  Future<List<Faaliyet>> getTumFaaliyetler() async {
    final db = await instance.database;
    final List<Map<String, dynamic>> result = await db.query('faaliyetler');
    return result.map((json) => Faaliyet.fromJson(json)).toList();
  }

  // --- SyncOperationStore ---

  @override
  Future<List<SyncOperation>> getPendingSyncOperations({int limit = 20}) async {
    final db = await instance.database;
    final rows = await db.query(
      'sync_operations',
      orderBy: 'createdAt ASC',
      limit: limit,
    );
    return rows.map(SyncOperation.fromJson).toList();
  }

  @override
  Future<int> getPendingSyncCount() async {
    final db = await instance.database;
    final result = await db.rawQuery('SELECT COUNT(*) FROM sync_operations');
    return Sqflite.firstIntValue(result) ?? 0;
  }

  @override
  Future<void> markSyncCompleted(String id) async {
    final db = await instance.database;
    await db.delete('sync_operations', where: 'id = ?', whereArgs: [id]);
  }

  @override
  Future<void> markSyncFailed(String id, String error) async {
    final db = await instance.database;
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

  Future<int> getTarlaSayisi() async {
    final db = await instance.database;
    final result = await db.rawQuery('SELECT COUNT(*) FROM tarlalar');
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<double> getToplamDonum() async {
    final db = await instance.database;
    final result = await db.rawQuery('SELECT SUM(size) as total FROM tarlalar');
    return (result.first['total'] as num?)?.toDouble() ?? 0.0;
  }

  // --- SİLME İŞLEMLERİ ---

  Future<int> deleteFaaliyet(String id) async {
    final db = await instance.database;
    return await db.delete('faaliyetler', where: 'id = ?', whereArgs: [id]);
  }

  Future<int> markFaaliyetCompleted(String id) async {
    final db = await instance.database;
    return await db.update(
      'faaliyetler',
      {
        'isCompleted': 1,
        'dueDate': null,
        'timestamp': DateTime.now().toIso8601String(),
      },
      where: 'id = ?',
      whereArgs: [id],
    );
  }
}

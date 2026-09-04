import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';
import '../../../services/database_helper.dart';
import '../domain/weather_summary.dart';

class LocalWeatherRepository {
  const LocalWeatherRepository({Future<Database> Function()? databaseProvider})
      : _dbProvider = databaseProvider;

  final Future<Database> Function()? _dbProvider;

  Future<Database> get _database async {
    final provider = _dbProvider;
    if (provider != null) return provider();
    return DatabaseHelper.instance.database;
  }

  static const String tableName = 'weather_cache';

  static const String sqlCreateTable = '''
    CREATE TABLE IF NOT EXISTS $tableName (
      farm_id TEXT PRIMARY KEY,
      temperature INTEGER NOT NULL,
      description TEXT NOT NULL,
      updated_at_utc TEXT NOT NULL
    )
  ''';

  Future<void> _ensureTable(Database db) async {
    try {
      await db.execute(sqlCreateTable);
    } catch (e) {
      debugPrint('LocalWeatherRepository: _ensureTable error: $e');
    }
  }

  Future<WeatherSummary?> getCachedWeather({String? farmId}) async {
    final key = farmId ?? 'default';
    try {
      final db = await _database;
      await _ensureTable(db);

      final maps = await db.query(
        tableName,
        where: 'farm_id = ?',
        whereArgs: [key],
        limit: 1,
      );

      if (maps.isEmpty) return null;

      final row = maps.first;
      return WeatherSummary(
        temperature: (row['temperature'] as num?)?.toInt() ?? 0,
        description: row['description']?.toString() ?? '',
      );
    } catch (e) {
      debugPrint('LocalWeatherRepository: getCachedWeather error: $e');
      return null;
    }
  }

  Future<void> cacheWeather({String? farmId, required WeatherSummary weather}) async {
    final key = farmId ?? 'default';
    try {
      final db = await _database;
      await _ensureTable(db);

      await db.insert(
        tableName,
        {
          'farm_id': key,
          'temperature': weather.temperature,
          'description': weather.description,
          'updated_at_utc': DateTime.now().toUtc().toIso8601String(),
        },
        conflictAlgorithm: ConflictAlgorithm.replace,
      );
    } catch (e) {
      debugPrint('LocalWeatherRepository: cacheWeather error: $e');
    }
  }
}

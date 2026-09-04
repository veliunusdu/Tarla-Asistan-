import 'dart:convert';
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
      temperature INTEGER,
      description TEXT NOT NULL,
      updated_at_utc TEXT NOT NULL,
      payload_json TEXT
    )
  ''';

  Future<void> _ensureTable(Database db) async {
    try {
      await db.execute(sqlCreateTable);
      try {
        await db.execute('ALTER TABLE $tableName ADD COLUMN payload_json TEXT');
      } catch (_) {
        // Column already exists or table was just created with it
      }
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
      final payloadJson = row['payload_json'] as String?;
      if (payloadJson != null && payloadJson.isNotEmpty) {
        try {
          final decoded = jsonDecode(payloadJson);
          if (decoded is Map<String, dynamic>) {
            return WeatherSummary.fromJson(decoded);
          } else if (decoded is Map) {
            return WeatherSummary.fromJson(Map<String, dynamic>.from(decoded));
          }
        } catch (e) {
          debugPrint('LocalWeatherRepository: error parsing payload_json: $e');
        }
      }

      return WeatherSummary(
        temperature: row['temperature'] as num?,
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
          'temperature': weather.temperature?.round() ?? 0,
          'description': weather.description,
          'updated_at_utc': DateTime.now().toUtc().toIso8601String(),
          'payload_json': jsonEncode(weather.toJson()),
        },
        conflictAlgorithm: ConflictAlgorithm.replace,
      );
    } catch (e) {
      debugPrint('LocalWeatherRepository: cacheWeather error: $e');
    }
  }
}

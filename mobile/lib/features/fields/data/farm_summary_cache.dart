import 'dart:convert';

import 'package:sqflite/sqflite.dart';

import '../../../services/database_helper.dart';

class FarmSummaryCacheSnapshot {
  const FarmSummaryCacheSnapshot({
    required this.payload,
    required this.cachedAt,
  });

  final Map<String, dynamic> payload;
  final DateTime cachedAt;
}

abstract interface class FarmSummaryCache {
  Future<FarmSummaryCacheSnapshot?> read();

  Future<void> write(Map<String, dynamic> payload);
}

class LocalFarmSummaryCache implements FarmSummaryCache {
  const LocalFarmSummaryCache({this.databaseHelper});

  final DatabaseHelper? databaseHelper;

  DatabaseHelper get _databaseHelper =>
      databaseHelper ?? DatabaseHelper.instance;

  @override
  Future<FarmSummaryCacheSnapshot?> read() async {
    final userId = _databaseHelper.currentUserId;
    if (userId == null) return null;

    final db = await _databaseHelper.database;
    final rows = await db.query(
      'farm_summary_cache',
      where: 'user_id = ?',
      whereArgs: [userId],
      limit: 1,
    );
    if (rows.isEmpty) return null;

    final payload = jsonDecode(rows.single['payload_json'] as String);
    final cachedAt = DateTime.tryParse(rows.single['cached_at_utc'] as String);
    if (payload is! Map || cachedAt == null) return null;

    return FarmSummaryCacheSnapshot(
      payload: Map<String, dynamic>.from(payload),
      cachedAt: cachedAt,
    );
  }

  @override
  Future<void> write(Map<String, dynamic> payload) async {
    final userId = _databaseHelper.currentUserId;
    if (userId == null) return;

    final db = await _databaseHelper.database;
    await db.insert('farm_summary_cache', {
      'user_id': userId,
      'cached_at_utc': DateTime.now().toUtc().toIso8601String(),
      'payload_json': jsonEncode(payload),
    }, conflictAlgorithm: ConflictAlgorithm.replace);
  }
}

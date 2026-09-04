import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';
import '../../../services/database_helper.dart';
import '../domain/models/market_category.dart';
import '../domain/models/market_item.dart';
import 'market_repository.dart';

/// Piyasa verilerini SQLite yerel veri tabanında önbelleğe alan ve çevrimdışı okuma sağlayan repository sınıfı.
class LocalMarketRepository implements MarketRepository {
  const LocalMarketRepository({this.dbHelper});

  final DatabaseHelper? dbHelper;

  DatabaseHelper get _helper => dbHelper ?? DatabaseHelper.instance;

  static const String tableName = 'market_cache';

  static const String sqlCreateTable = '''
    CREATE TABLE IF NOT EXISTS $tableName (
      code TEXT PRIMARY KEY,
      name TEXT NOT NULL,
      category TEXT NOT NULL,
      price REAL NOT NULL,
      previous_price REAL NOT NULL,
      change_percent REAL NOT NULL,
      change_direction TEXT NOT NULL,
      unit TEXT NOT NULL,
      icon_key TEXT NOT NULL,
      updated_at_utc TEXT NOT NULL,
      cached_at_utc TEXT NOT NULL
    )
  ''';

  static const String sqlCreateIndexCategory = '''
    CREATE INDEX IF NOT EXISTS idx_market_cache_category ON $tableName(category)
  ''';

  static const String sqlCreateIndexCachedAt = '''
    CREATE INDEX IF NOT EXISTS idx_market_cache_cached_at ON $tableName(cached_at_utc)
  ''';

  Future<void> _ensureTable(Database db) async {
    try {
      await db.execute(sqlCreateTable);
      await db.execute(sqlCreateIndexCategory);
      await db.execute(sqlCreateIndexCachedAt);
    } catch (e) {
      debugPrint('LocalMarketRepository: _ensureTable error: $e');
    }
  }

  /// Yerel veritabanında saklanan piyasa kalemlerini sorgular.
  /// Kategori belirtilirse ("all" haricinde) yalnızca o kategoriye ait kayıtlar döndürülür.
  Future<List<MarketItem>> getCachedData({MarketCategory? category}) async {
    try {
      final db = await _helper.database;
      await _ensureTable(db);

      List<Map<String, dynamic>> maps;

      if (category != null && category != MarketCategory.all) {
        maps = await db.query(
          tableName,
          where: 'category = ?',
          whereArgs: [category.apiValue],
          orderBy: 'category ASC, code ASC',
        );
      } else {
        maps = await db.query(
          tableName,
          orderBy: 'category ASC, code ASC',
        );
      }

      if (maps.isEmpty) {
        return [];
      }

      return maps.map(MarketItem.fromSqlite).toList();
    } catch (e) {
      debugPrint('LocalMarketRepository: getCachedData error: $e');
      return [];
    }
  }

  /// Uzak sunucudan çekilen piyasa kalemlerini toplu işlem (transaction) içinde yerel önbelleğe yazar.
  Future<void> cacheData(List<MarketItem> items) async {
    if (items.isEmpty) return;

    try {
      final db = await _helper.database;
      await _ensureTable(db);
      final cachedAt = DateTime.now().toUtc().toIso8601String();

      await db.transaction((txn) async {
        final batch = txn.batch();
        for (final item in items) {
          batch.insert(
            tableName,
            item.toSqliteMap(cachedAtUtc: cachedAt),
            conflictAlgorithm: ConflictAlgorithm.replace,
          );
        }
        await batch.commit(noResult: true);
      });
    } catch (e) {
      debugPrint('LocalMarketRepository: cacheData error: $e');
    }
  }

  /// Önbellekteki en eski verinin yaşını kontrol eder.
  /// Eğer kayıt yoksa veya en eski kayıt belirtilen süreden (varsayılan: 72 saat) eskiyse true döner.
  Future<bool> isStale({Duration maxAge = const Duration(hours: 72)}) async {
    try {
      final oldest = await getOldestCachedAt();
      if (oldest == null) return true;

      final age = DateTime.now().toUtc().difference(oldest);
      return age > maxAge;
    } catch (e) {
      debugPrint('LocalMarketRepository: isStale error: $e');
      return true;
    }
  }

  /// Önbellekteki en eski kaydın zaman damgasını getirir.
  Future<DateTime?> getOldestCachedAt() async {
    try {
      final db = await _helper.database;
      await _ensureTable(db);
      final result = await db.rawQuery(
        'SELECT MIN(cached_at_utc) as oldest FROM $tableName',
      );

      if (result.isEmpty || result.first['oldest'] == null) {
        return null;
      }

      final rawOldest = result.first['oldest']?.toString();
      return DateTime.tryParse(rawOldest ?? '')?.toUtc();
    } catch (e) {
      debugPrint('LocalMarketRepository: getOldestCachedAt error: $e');
      return null;
    }
  }

  // ── MarketRepository Arayüz Metotları ─────────────────────────────────────

  @override
  Future<List<MarketItem>> getCachedMarketData({MarketCategory? category}) {
    return getCachedData(category: category);
  }

  @override
  Future<List<MarketItem>> getMarketData({MarketCategory? category}) {
    return getCachedData(category: category);
  }

  @override
  Future<void> refreshMarketData({MarketCategory? category}) async {
    // Local repository tek başına dış ağdan yenileme yapmaz.
  }
}

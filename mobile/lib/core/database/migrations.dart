import 'package:sqflite/sqflite.dart';

/// Veritabanı migration işlemleri.
///
/// Her fonksiyon idempotent'tir: PRAGMA table_info ile kolon varlığı kontrol
/// edildikten sonra ALTER TABLE çalıştırılır; aynı kolon ikinci kez eklenmez.
abstract final class Migrations {
  /// Version 1 → 2: faaliyetler tablosuna [dueDate] ve [isCompleted] ekler.
  ///
  /// Sürüm 1 dönemindeki tüm kayıtlar yalnızca geçmiş faaliyetlerdir;
  /// bu nedenle [isCompleted] için DEFAULT 1 kullanılır.
  static Future<void> v1ToV2(Database db) async {
    final existing = await _columnNames(db, 'faaliyetler');

    if (!existing.contains('dueDate')) {
      await db.execute('ALTER TABLE faaliyetler ADD COLUMN dueDate TEXT');
    }

    if (!existing.contains('isCompleted')) {
      await db.execute(
        'ALTER TABLE faaliyetler ADD COLUMN isCompleted INTEGER NOT NULL DEFAULT 1',
      );
    }
  }

  /// [table] tablosundaki mevcut kolon adlarını döndürür.
  static Future<Set<String>> _columnNames(Database db, String table) async {
    final rows = await db.rawQuery('PRAGMA table_info($table)');
    return rows.map((r) => r['name'] as String).toSet();
  }
}

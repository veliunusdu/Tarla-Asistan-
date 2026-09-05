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

  /// Version 3 → 4: tarlalar tablosundaki latitude, longitude, size, cropType,
  /// plantingDate kolonlarından NOT NULL kısıtını kaldırır.
  ///
  /// SQLite ALTER COLUMN'u desteklemediği için tablo yeniden oluşturulur.
  /// Mevcut kayıtlar korunur.
  ///
  /// **Idempotency**: Beş kolonun tamamı zaten nullable ise migration atlanır.
  ///
  /// **Crash recovery**: Önceki bir çalışmada tablo yeniden oluşturulurken
  /// uygulama çökmüş olabilir.  Bu durumda yarım kalan `tarlalar_new`
  /// güvenle bırakılır; asıl `tarlalar` verisi sağlamdır.  Bir sonraki
  /// başlatmada DROP TABLE IF EXISTS ile bu artık tablo temizlenir ve
  /// migration baştan yapılır.
  ///
  /// **Foreign keys**: `faaliyetler.tarlaId` → `tarlalar.id` ilişkisi şema
  /// kısıtı (FOREIGN KEY referansı) olarak tanımlanmamıştır; migration
  /// süresince referans bütünlüğü SQLite tarafından zorlanmaz.
  static Future<void> v3ToV4(Database db) async {
    // Clean up any leftover table from a previous partial run so that a
    // CREATE TABLE below never fails on an already-existing name.
    await db.execute('DROP TABLE IF EXISTS tarlalar_new');

    // Check whether ALL five columns are already nullable.
    final cols = await db.rawQuery('PRAGMA table_info(tarlalar)');
    final colMap = <String, Map<String, Object?>>{
      for (final c in cols)
        c['name'] as String: Map<String, Object?>.from(c),
    };
    const needsNullable = [
      'latitude',
      'longitude',
      'size',
      'cropType',
      'plantingDate',
    ];
    final allNullable = needsNullable.every(
      (name) => (colMap[name]?['notnull'] as int? ?? 0) == 0,
    );
    if (allNullable) return; // Already nullable — no-op.

    await db.execute('''
      CREATE TABLE tarlalar_new (
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
      INSERT INTO tarlalar_new (id, name, latitude, longitude, size, cropType, plantingDate)
      SELECT id, name, latitude, longitude, size, cropType, plantingDate
      FROM tarlalar
    ''');
    await db.execute('DROP TABLE tarlalar');
    await db.execute('ALTER TABLE tarlalar_new RENAME TO tarlalar');
  }

  /// Version 4 → 5: tarlalar, faaliyetler ve sync_operations tablolarına
  /// kullanıcı izolasyonu için [userId] kolonu ve indeksleri ekler.
  static Future<void> v4ToV5(Database db) async {
    final tarlalarCols = await _columnNames(db, 'tarlalar');
    if (!tarlalarCols.contains('userId')) {
      await db.execute('ALTER TABLE tarlalar ADD COLUMN userId TEXT');
    }
    await db.execute(
      'CREATE INDEX IF NOT EXISTS ix_tarlalar_user_id ON tarlalar(userId)',
    );

    final faaliyetlerCols = await _columnNames(db, 'faaliyetler');
    if (!faaliyetlerCols.contains('userId')) {
      await db.execute('ALTER TABLE faaliyetler ADD COLUMN userId TEXT');
    }
    await db.execute(
      'CREATE INDEX IF NOT EXISTS ix_faaliyetler_user_id ON faaliyetler(userId)',
    );

    final syncCols = await _columnNames(db, 'sync_operations');
    if (!syncCols.contains('userId')) {
      await db.execute('ALTER TABLE sync_operations ADD COLUMN userId TEXT');
    }
    await db.execute(
      'CREATE INDEX IF NOT EXISTS ix_sync_operations_user_id ON sync_operations(userId)',
    );
  }

  /// Version 7 -> 8: durable offline case submissions.
  static Future<void> v7ToV8(Database db) async {
    await db.execute('''
      CREATE TABLE IF NOT EXISTS pending_case_submissions (
        id TEXT PRIMARY KEY,
        user_id TEXT NOT NULL,
        farm_id TEXT NOT NULL,
        category TEXT NOT NULL,
        title TEXT NOT NULL,
        description TEXT NOT NULL,
        client_operation_id TEXT NOT NULL,
        local_image_path TEXT,
        uploaded_media_id TEXT,
        created_at_utc TEXT NOT NULL,
        attempt_count INTEGER NOT NULL DEFAULT 0,
        last_attempt_at_utc TEXT,
        last_error_code INTEGER,
        state TEXT NOT NULL DEFAULT 'pending'
      )
    ''');
    await db.execute('CREATE UNIQUE INDEX IF NOT EXISTS ux_pending_cases_user_operation ON pending_case_submissions(user_id, client_operation_id)');
    await db.execute('CREATE INDEX IF NOT EXISTS ix_pending_cases_user_created ON pending_case_submissions(user_id, created_at_utc)');
  }

  /// [table] tablosundaki mevcut kolon adlarını döndürür.
  static Future<Set<String>> _columnNames(Database db, String table) async {
    final rows = await db.rawQuery('PRAGMA table_info($table)');
    return rows.map((r) => r['name'] as String).toSet();
  }
}

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/database/migrations.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

/// Creates the `tarlalar` table that existed in production versions 1–3
/// (all five optional columns carry NOT NULL constraints).
Future<void> _createV3Tarlalar(Database db) async {
  await db.execute('''
    CREATE TABLE tarlalar (
      id TEXT PRIMARY KEY,
      name TEXT NOT NULL,
      latitude REAL NOT NULL,
      longitude REAL NOT NULL,
      size REAL NOT NULL,
      cropType TEXT NOT NULL,
      plantingDate TEXT NOT NULL
    )
  ''');
}

/// Creates the `faaliyetler` table as it existed in production version 1
/// (no `dueDate`, no `isCompleted`).
Future<void> _createV1Faaliyetler(Database db) async {
  await db.execute('''
    CREATE TABLE faaliyetler (
      id TEXT PRIMARY KEY,
      tarlaId TEXT NOT NULL,
      type TEXT NOT NULL,
      note TEXT,
      audioPath TEXT,
      photos TEXT,
      timestamp TEXT NOT NULL
    )
  ''');
}

/// Creates the `faaliyetler` table as it existed in production versions 2–3.
Future<void> _createV2Faaliyetler(Database db) async {
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
}

/// Creates the `sync_operations` table (introduced in version 3).
Future<void> _createSyncOperations(Database db) async {
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

/// Inserts a sample tarla into the v1-v3 `tarlalar` table (all NOT NULL).
Future<void> _insertV3Tarla(
  Database db, {
  required String id,
  String name = 'Test Tarlası',
  double latitude = 38.7,
  double longitude = 35.4,
  double size = 12.5,
  String cropType = 'WHEAT',
  String plantingDate = '2026-03-15T00:00:00.000',
}) async {
  await db.insert('tarlalar', {
    'id': id,
    'name': name,
    'latitude': latitude,
    'longitude': longitude,
    'size': size,
    'cropType': cropType,
    'plantingDate': plantingDate,
  });
}

/// Returns a map from column name to PRAGMA table_info row for `tarlalar`.
Future<Map<String, Map<String, Object?>>> _tarlaColumnInfo(Database db) async {
  final rows = await db.rawQuery('PRAGMA table_info(tarlalar)');
  return {for (final r in rows) r['name'] as String: Map<String, Object?>.from(r)};
}

/// Applies the same upgrade chain that DatabaseHelper._upgradeDB does,
/// upgrading from [from] to the current target version (5).
Future<void> _applyUpgrades(Database db, {required int from}) async {
  if (from < 2) await Migrations.v1ToV2(db);
  if (from < 3) await _createSyncOperations(db);
  if (from < 4) await Migrations.v3ToV4(db);
  if (from < 5) await Migrations.v4ToV5(db);
}

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  // -------------------------------------------------------------------------
  // Existing v1→v2 tests (unchanged)
  // -------------------------------------------------------------------------

  group('Migrations.v1ToV2', () {
    test(
      'version 1 veritabanını version 2 şemasına yükseltir ve veriyi korur',
      () async {
        final db = await openDatabase(
          inMemoryDatabasePath,
          version: 1,
          onCreate: (db, _) async {
            await db.execute('''
            CREATE TABLE faaliyetler (
              id TEXT PRIMARY KEY,
              tarlaId TEXT NOT NULL,
              type TEXT NOT NULL,
              note TEXT,
              audioPath TEXT,
              photos TEXT,
              timestamp TEXT NOT NULL
            )
          ''');
          },
        );

        await db.insert('faaliyetler', {
          'id': 'f1',
          'tarlaId': 't1',
          'type': 'Sulama',
          'note': 'Eski kayıt',
          'timestamp': '2024-01-15T10:00:00.000',
        });

        await Migrations.v1ToV2(db);

        final columns = await db
            .rawQuery('PRAGMA table_info(faaliyetler)')
            .then((rows) => rows.map((r) => r['name'] as String).toSet());

        expect(columns, contains('dueDate'));
        expect(columns, contains('isCompleted'));

        final rows = await db.query(
          'faaliyetler',
          where: 'id = ?',
          whereArgs: ['f1'],
        );
        expect(rows, hasLength(1));
        expect(rows.first['type'], 'Sulama');
        expect(rows.first['isCompleted'], 1);

        await db.close();
      },
    );

    test('migration idempotent — aynı kolon iki kez eklenmez', () async {
      final db = await openDatabase(
        inMemoryDatabasePath,
        version: 1,
        onCreate: (db, _) async {
          await db.execute('''
            CREATE TABLE faaliyetler (
              id TEXT PRIMARY KEY,
              tarlaId TEXT NOT NULL,
              type TEXT NOT NULL,
              note TEXT,
              timestamp TEXT NOT NULL
            )
          ''');
        },
      );

      await Migrations.v1ToV2(db);
      await expectLater(Migrations.v1ToV2(db), completes);

      await db.close();
    });

    test('temiz version 2 kurulumu doğru kolonları içerir', () async {
      final db = await openDatabase(
        inMemoryDatabasePath,
        version: 2,
        onCreate: (db, _) async {
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
        },
      );

      final columns = await db
          .rawQuery('PRAGMA table_info(faaliyetler)')
          .then((rows) => rows.map((r) => r['name'] as String).toSet());

      expect(columns, containsAll(['dueDate', 'isCompleted']));

      final colInfo = await db.rawQuery('PRAGMA table_info(faaliyetler)');
      final isCompletedInfo = colInfo.firstWhere(
        (r) => r['name'] == 'isCompleted',
      );
      expect(isCompletedInfo['notnull'], 1);
      expect(isCompletedInfo['dflt_value'], '1');

      await db.close();
    });
  });

  // -------------------------------------------------------------------------
  // v3ToV4 — core migration tests
  // -------------------------------------------------------------------------

  group('Migrations.v3ToV4', () {
    test(
      'tüm 5 kolon nullable olur ve mevcut tarla kayıtları korunur',
      () async {
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);
        await _insertV3Tarla(db, id: 't1', name: 'Kuzey Tarla', size: 15.0);
        await _insertV3Tarla(
          db,
          id: 't2',
          name: 'Güney Tarla',
          cropType: 'CORN',
        );

        await Migrations.v3ToV4(db);

        final colInfo = await _tarlaColumnInfo(db);
        const checkCols = [
          'latitude',
          'longitude',
          'size',
          'cropType',
          'plantingDate',
        ];
        for (final col in checkCols) {
          expect(
            colInfo[col]?['notnull'],
            0,
            reason: '$col hâlâ NOT NULL — migration başarısız',
          );
        }

        // name NOT NULL kalmalı; id TEXT PRIMARY KEY için SQLite PRAGMA
        // notnull=0 döndürür (PRIMARY KEY kısıtı ayrıca enforce edilir).
        expect(colInfo['name']?['notnull'], 1);

        // Tarla sayısı korundu
        final count = (await db.rawQuery(
          'SELECT COUNT(*) FROM tarlalar',
        )).first.values.first;
        expect(count, 2);

        // Değerler korundu
        final t1 = (await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['t1'],
        )).first;
        expect(t1['name'], 'Kuzey Tarla');
        expect(t1['size'], 15.0);
        expect(t1['cropType'], 'WHEAT');

        await db.close();
      },
    );

    test('tüm ID\'ler değişmez', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await _insertV3Tarla(db, id: 'uuid-abc-123');
      await _insertV3Tarla(db, id: 'uuid-def-456');

      await Migrations.v3ToV4(db);

      final ids = (await db.query(
        'tarlalar',
        columns: ['id'],
      )).map((r) => r['id']).toSet();
      expect(ids, containsAll(['uuid-abc-123', 'uuid-def-456']));

      await db.close();
    });

    test('idempotent — ikinci çalıştırma hata üretmez', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await _insertV3Tarla(db, id: 't1');

      await Migrations.v3ToV4(db);
      await expectLater(Migrations.v3ToV4(db), completes);

      // Veri hâlâ orada
      final count = (await db.rawQuery(
        'SELECT COUNT(*) FROM tarlalar',
      )).first.values.first;
      expect(count, 1);

      await db.close();
    });

    test(
      'yarım kalmış tarlalar_new tablosu varsa hata vermez (crash recovery)',
      () async {
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);
        await _insertV3Tarla(db, id: 't1', name: 'Orijinal Tarla');

        // Crash simülasyonu: sadece CREATE TABLE tarlalar_new çalıştırıldı,
        // DROP/RENAME adımları tamamlanmadı.
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

        // v3ToV4 bu durumda crash etmemeli, migration tamamlanmalı.
        await expectLater(Migrations.v3ToV4(db), completes);

        // Orijinal veri korundu
        final rows = await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['t1'],
        );
        expect(rows, hasLength(1));
        expect(rows.first['name'], 'Orijinal Tarla');

        // tarlalar_new artık yok olmalı (RENAME sonucu)
        final tables = await db.rawQuery(
          "SELECT name FROM sqlite_master WHERE type='table' AND name='tarlalar_new'",
        );
        expect(tables, isEmpty);

        await db.close();
      },
    );

    test(
      'migration sonrası null alanları olan tarla eklenip okunabilir',
      () async {
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);

        await Migrations.v3ToV4(db);

        // Tüm nullable alanlar null olarak yazılabilmeli
        await db.insert('tarlalar', {
          'id': 'null-tarla',
          'name': 'Sadece İsim',
          'latitude': null,
          'longitude': null,
          'size': null,
          'cropType': null,
          'plantingDate': null,
        });

        final rows = await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['null-tarla'],
        );
        expect(rows, hasLength(1));
        expect(rows.first['latitude'], isNull);
        expect(rows.first['size'], isNull);
        expect(rows.first['cropType'], isNull);
        expect(rows.first['plantingDate'], isNull);

        await db.close();
      },
    );

    test('migration öncesi dolu tarlanın tüm değerleri değişmez', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await _insertV3Tarla(
        db,
        id: 'full-t1',
        name: 'Tam Değerli Tarla',
        latitude: 39.5,
        longitude: 32.1,
        size: 8.75,
        cropType: 'SUNFLOWER',
        plantingDate: '2026-04-01T00:00:00.000',
      );

      await Migrations.v3ToV4(db);

      final row = (await db.query(
        'tarlalar',
        where: 'id = ?',
        whereArgs: ['full-t1'],
      )).first;

      expect(row['name'], 'Tam Değerli Tarla');
      expect(row['latitude'], 39.5);
      expect(row['longitude'], 32.1);
      expect(row['size'], 8.75);
      expect(row['cropType'], 'SUNFLOWER');
      expect(row['plantingDate'], '2026-04-01T00:00:00.000');

      await db.close();
    });

    test('temiz version 4 kurulumunda (onCreate) 5 kolon nullable', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      // Simulate onCreate creating v4 schema directly (nullable from start)
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

      final colInfo = await _tarlaColumnInfo(db);
      for (final col in [
        'latitude',
        'longitude',
        'size',
        'cropType',
        'plantingDate',
      ]) {
        expect(
          colInfo[col]?['notnull'],
          0,
          reason: '$col NOT NULL iken nullable olmalı',
        );
      }

      await db.close();
    });
  });

  // -------------------------------------------------------------------------
  // Faaliyet ilişkisi — migration sonrası kayıt bütünlüğü
  // -------------------------------------------------------------------------

  group('Faaliyet tarlaId ilişkisi migration boyunca korunur', () {
    test('v3→v4 sonrası faaliyetlerin tarlaId değerleri değişmez', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await _createV2Faaliyetler(db);

      await _insertV3Tarla(db, id: 'tarla-1');
      await _insertV3Tarla(db, id: 'tarla-2', name: 'İkinci Tarla');

      // Her tarlaya faaliyet ekle
      await db.insert('faaliyetler', {
        'id': 'f1',
        'tarlaId': 'tarla-1',
        'type': 'Sulama',
        'note': '',
        'timestamp': '2026-01-01T10:00:00.000',
        'isCompleted': 1,
      });
      await db.insert('faaliyetler', {
        'id': 'f2',
        'tarlaId': 'tarla-2',
        'type': 'Gübreleme',
        'note': 'Azot',
        'timestamp': '2026-01-02T10:00:00.000',
        'isCompleted': 0,
      });

      await Migrations.v3ToV4(db);

      // Faaliyet kayıtları aynen duruyor
      final f1 = (await db.query(
        'faaliyetler',
        where: 'id = ?',
        whereArgs: ['f1'],
      )).first;
      expect(f1['tarlaId'], 'tarla-1');
      expect(f1['type'], 'Sulama');

      final f2 = (await db.query(
        'faaliyetler',
        where: 'id = ?',
        whereArgs: ['f2'],
      )).first;
      expect(f2['tarlaId'], 'tarla-2');
      expect(f2['note'], 'Azot');

      // Tarla sayısı
      final tarlaCount = (await db.rawQuery(
        'SELECT COUNT(*) FROM tarlalar',
      )).first.values.first;
      expect(tarlaCount, 2);

      await db.close();
    });

    test(
      'foreign key constraint tarlalar tablosunda tanımlı değil — bu beklenen davranıştır',
      () async {
        // The faaliyetler.tarlaId column has no REFERENCES clause, so SQLite
        // does not enforce referential integrity between faaliyetler and tarlalar.
        // This test documents that fact.
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);
        await _createV2Faaliyetler(db);
        await db.execute('PRAGMA foreign_keys = ON');

        // Insert a faaliyet referencing a non-existent farm — should NOT throw
        // because there is no FOREIGN KEY constraint defined.
        await expectLater(
          db.insert('faaliyetler', {
            'id': 'orphan',
            'tarlaId': 'non-existent-farm',
            'type': 'Test',
            'note': '',
            'timestamp': '2026-01-01T00:00:00.000',
            'isCompleted': 1,
          }),
          completes,
        );

        await db.close();
      },
    );
  });

  // -------------------------------------------------------------------------
  // Full upgrade chains
  // -------------------------------------------------------------------------

  group('Tam upgrade zinciri testleri', () {
    test('v3 → v4: verilerle tam upgrade zinciri', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);

      // Simulate v3 database state
      await _createV3Tarlalar(db);
      await _createV2Faaliyetler(db);
      await _createSyncOperations(db);
      await _insertV3Tarla(db, id: 't-v3-1', name: 'V3 Tarlası', size: 5.0);
      await db.insert('faaliyetler', {
        'id': 'f-v3-1',
        'tarlaId': 't-v3-1',
        'type': 'Hasat',
        'note': '',
        'timestamp': '2026-06-01T00:00:00.000',
        'isCompleted': 1,
      });

      await _applyUpgrades(db, from: 3);

      // Tarlalar nullable
      final colInfo = await _tarlaColumnInfo(db);
      expect(colInfo['size']?['notnull'], 0);
      expect(colInfo['cropType']?['notnull'], 0);

      // Veriler korundu
      final tarla = (await db.query(
        'tarlalar',
        where: 'id = ?',
        whereArgs: ['t-v3-1'],
      )).first;
      expect(tarla['name'], 'V3 Tarlası');
      expect(tarla['size'], 5.0);

      final faaliyet = (await db.query(
        'faaliyetler',
        where: 'id = ?',
        whereArgs: ['f-v3-1'],
      )).first;
      expect(faaliyet['tarlaId'], 't-v3-1');

      await db.close();
    });

    test('v2 → v4: v2 şemasından tam upgrade zinciri', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);

      // Simulate v2 database state (tarlalar NOT NULL, no sync_operations)
      await _createV3Tarlalar(db);
      await _createV2Faaliyetler(db);
      await _insertV3Tarla(db, id: 't-v2-1', name: 'V2 Tarlası', size: 20.0);

      await _applyUpgrades(db, from: 2);

      // sync_operations tablosu oluştu
      final tables = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='sync_operations'",
      );
      expect(tables, hasLength(1));

      // Tarlalar nullable
      final colInfo = await _tarlaColumnInfo(db);
      expect(colInfo['latitude']?['notnull'], 0);

      // Veri korundu
      final tarla = (await db.query(
        'tarlalar',
        where: 'id = ?',
        whereArgs: ['t-v2-1'],
      )).first;
      expect(tarla['size'], 20.0);

      await db.close();
    });

    test('v1 → v4: v1 şemasından tam upgrade zinciri', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);

      // Simulate v1 database state
      await _createV3Tarlalar(db);
      await _createV1Faaliyetler(db);
      await _insertV3Tarla(db, id: 't-v1-1', name: 'V1 Tarlası');
      // v1 faaliyet — isCompleted yok
      await db.insert('faaliyetler', {
        'id': 'f-v1-1',
        'tarlaId': 't-v1-1',
        'type': 'Ekim',
        'note': 'Eski kayıt',
        'timestamp': '2024-06-01T00:00:00.000',
      });

      await _applyUpgrades(db, from: 1);

      // faaliyetler dueDate ve isCompleted aldı
      final faaliyetCols = await db
          .rawQuery('PRAGMA table_info(faaliyetler)')
          .then((rows) => rows.map((r) => r['name'] as String).toSet());
      expect(faaliyetCols, containsAll(['dueDate', 'isCompleted']));

      // Eski faaliyet hâlâ var, isCompleted DEFAULT 1 oldu
      final faaliyet = (await db.query(
        'faaliyetler',
        where: 'id = ?',
        whereArgs: ['f-v1-1'],
      )).first;
      expect(faaliyet['type'], 'Ekim');
      expect(faaliyet['isCompleted'], 1);

      // sync_operations tablosu oluştu
      final syncTables = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='sync_operations'",
      );
      expect(syncTables, hasLength(1));

      // Tarlalar nullable
      final colInfo = await _tarlaColumnInfo(db);
      expect(colInfo['plantingDate']?['notnull'], 0);

      // Tarla verisi korundu
      final tarla = (await db.query(
        'tarlalar',
        where: 'id = ?',
        whereArgs: ['t-v1-1'],
      )).first;
      expect(tarla['name'], 'V1 Tarlası');

      await db.close();
    });

    test(
      'v3→v4 sonrası PRAGMA table_info tüm 5 kolon notnull==0 döndürür',
      () async {
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);
        await Migrations.v3ToV4(db);

        final colInfo = await _tarlaColumnInfo(db);
        for (final col in [
          'latitude',
          'longitude',
          'size',
          'cropType',
          'plantingDate',
        ]) {
          expect(
            (colInfo[col]?['notnull'] as int? ?? -1),
            0,
            reason: 'PRAGMA table_info: $col notnull != 0',
          );
        }

        await db.close();
      },
    );

    test(
      'v3→v4 sonrası hem null hem dolu tarla kaydı eklenip okunabilir',
      () async {
        final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
        await _createV3Tarlalar(db);
        await _insertV3Tarla(
          db,
          id: 'existing-1',
          name: 'Eski Tarla',
          size: 5.0,
        );

        await Migrations.v3ToV4(db);

        // Null alanları olan yeni kayıt
        await db.insert('tarlalar', {
          'id': 'new-null',
          'name': 'Eksik Bilgiler',
          'latitude': null,
          'longitude': null,
          'size': null,
          'cropType': null,
          'plantingDate': null,
        });

        // Dolu yeni kayıt
        await db.insert('tarlalar', {
          'id': 'new-full',
          'name': 'Tam Bilgiler',
          'latitude': 39.5,
          'longitude': 32.1,
          'size': 10.0,
          'cropType': 'BARLEY',
          'plantingDate': '2026-05-01T00:00:00.000',
        });

        // Eski kayıt değişmedi
        final old = (await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['existing-1'],
        )).first;
        expect(old['size'], 5.0);

        // Null kayıt doğru okundu
        final nullRow = (await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['new-null'],
        )).first;
        expect(nullRow['latitude'], isNull);
        expect(nullRow['cropType'], isNull);

        // Dolu kayıt doğru okundu
        final fullRow = (await db.query(
          'tarlalar',
          where: 'id = ?',
          whereArgs: ['new-full'],
        )).first;
        expect(fullRow['size'], 10.0);
        expect(fullRow['cropType'], 'BARLEY');

        // Toplam 3 kayıt var
        final count = (await db.rawQuery(
          'SELECT COUNT(*) FROM tarlalar',
        )).first.values.first;
        expect(count, 3);

        await db.close();
      },
    );
  });

  // -------------------------------------------------------------------------
  // Index / trigger / FK structure
  // -------------------------------------------------------------------------

  group('Tablo yapısı kontrolü', () {
    test('tarlalar tablosunda index veya trigger yoktur', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await Migrations.v3ToV4(db);

      final indexes = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='tarlalar'",
      );
      final triggers = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='trigger' AND tbl_name='tarlalar'",
      );

      // There are no extra indexes or triggers on tarlalar (only implicit PK).
      // Autoindex names contain 'autoindex' — these are system-managed and safe.
      final namedIndexes = indexes.where(
        (r) => !(r['name'] as String).startsWith('sqlite_autoindex'),
      );
      expect(namedIndexes, isEmpty);
      expect(triggers, isEmpty);

      await db.close();
    });

    test('sync_operations index upgrade sonrası hâlâ var', () async {
      final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
      await _createV3Tarlalar(db);
      await _createSyncOperations(db);

      await Migrations.v3ToV4(db);

      final indexes = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='sync_operations'",
      );
      final indexNames = indexes.map((r) => r['name']).toList();
      expect(indexNames, contains('ix_sync_operations_created'));

      await db.close();
    });
  });

  // -------------------------------------------------------------------------
  // Version 4 -> 5 migration tests
  // -------------------------------------------------------------------------

  group('Migrations.v4ToV5', () {
    test('tarlalar, faaliyetler ve sync_operations tablolarına userId ekler ve index oluşturur', () async {
      final db = await openDatabase(
        inMemoryDatabasePath,
        singleInstance: false,
        version: 4,
        onCreate: (db, _) async {
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
          await db.execute('''
            CREATE TABLE sync_operations (
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
        },
      );

      await Migrations.v4ToV5(db);

      final tIndexes = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='tarlalar'",
      );
      expect(tIndexes.map((r) => r['name']), contains('ix_tarlalar_user_id'));

      final fIndexes = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='faaliyetler'",
      );
      expect(fIndexes.map((r) => r['name']), contains('ix_faaliyetler_user_id'));

      final sIndexes = await db.rawQuery(
        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='sync_operations'",
      );
      expect(sIndexes.map((r) => r['name']), contains('ix_sync_operations_user_id'));

      await db.close();
    });
  });
}

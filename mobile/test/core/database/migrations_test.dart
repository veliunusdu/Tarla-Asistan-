import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/database/migrations.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('Migrations.v1ToV2', () {
    test(
      'version 1 veritabanını version 2 şemasına yükseltir ve veriyi korur',
      () async {
        final db = await openDatabase(
          inMemoryDatabasePath,
          version: 1,
          onCreate: (db, _) async {
            // Version 1 şeması — dueDate ve isCompleted yok
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

        // Eski kayıt ekle
        await db.insert('faaliyetler', {
          'id': 'f1',
          'tarlaId': 't1',
          'type': 'Sulama',
          'note': 'Eski kayıt',
          'timestamp': '2024-01-15T10:00:00.000',
        });

        // Migration uygula
        await Migrations.v1ToV2(db);

        // dueDate kolonu eklendi mi?
        final columns = await db
            .rawQuery('PRAGMA table_info(faaliyetler)')
            .then((rows) => rows.map((r) => r['name'] as String).toSet());

        expect(columns, contains('dueDate'));
        expect(columns, contains('isCompleted'));

        // Eski kayıt hâlâ var mı?
        final rows = await db.query(
          'faaliyetler',
          where: 'id = ?',
          whereArgs: ['f1'],
        );
        expect(rows, hasLength(1));
        expect(rows.first['type'], 'Sulama');

        // Eski kaydın isCompleted değeri 1 mi? (DEFAULT 1 uygulandı)
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

      // İki kez çalıştırmak hata üretmemeli
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

      // isCompleted NOT NULL DEFAULT 1 doğrula
      final colInfo = await db.rawQuery('PRAGMA table_info(faaliyetler)');
      final isCompletedInfo = colInfo.firstWhere(
        (r) => r['name'] == 'isCompleted',
      );
      expect(isCompletedInfo['notnull'], 1);
      expect(isCompletedInfo['dflt_value'], '1');

      await db.close();
    });
  });
}

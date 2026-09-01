import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/database/migrations.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/services/database_helper.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('Migrations.v4ToV5', () {
    test('tarlalar, faaliyetler ve sync_operations tablolarına userId kolonu ekler ve verileri korur', () async {
      final db = await openDatabase(
        inMemoryDatabasePath,
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

      // Insert pre-migration sample data
      await db.insert('tarlalar', {'id': 't1', 'name': 'Eski Tarla', 'size': 5.0});
      await db.insert('faaliyetler', {
        'id': 'f1',
        'tarlaId': 't1',
        'type': 'Sulama',
        'timestamp': DateTime.now().toIso8601String(),
      });
      await db.insert('sync_operations', {
        'id': 's1',
        'method': 'POST',
        'endpoint': '/farms/t1/activities',
        'payload': '{}',
        'attempts': 0,
        'createdAt': DateTime.now().toIso8601String(),
        'updatedAt': DateTime.now().toIso8601String(),
      });

      // Run migration
      await Migrations.v4ToV5(db);

      // Verify columns exist
      final tCols = (await db.rawQuery('PRAGMA table_info(tarlalar)')).map((r) => r['name'] as String).toSet();
      final fCols = (await db.rawQuery('PRAGMA table_info(faaliyetler)')).map((r) => r['name'] as String).toSet();
      final sCols = (await db.rawQuery('PRAGMA table_info(sync_operations)')).map((r) => r['name'] as String).toSet();

      expect(tCols, contains('userId'));
      expect(fCols, contains('userId'));
      expect(sCols, contains('userId'));

      // Verify old data preserved with null userId
      final tRow = (await db.query('tarlalar', where: 'id = ?', whereArgs: ['t1'])).first;
      expect(tRow['name'], 'Eski Tarla');
      expect(tRow['userId'], isNull);

      final fRow = (await db.query('faaliyetler', where: 'id = ?', whereArgs: ['f1'])).first;
      expect(fRow['type'], 'Sulama');
      expect(fRow['userId'], isNull);

      final sRow = (await db.query('sync_operations', where: 'id = ?', whereArgs: ['s1'])).first;
      expect(sRow['id'], 's1');
      expect(sRow['userId'], isNull);

      // Verify idempotency
      await Migrations.v4ToV5(db);

      await db.close();
    });
  });

  group('DatabaseHelper User Isolation', () {
    late DatabaseHelper dbHelper;
    String? currentTestUser;

    setUp(() async {
      final db = await openDatabase(
        inMemoryDatabasePath,
        version: 5,
        onCreate: (db, _) async {
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
            CREATE TABLE sync_operations (
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
        },
      );

      currentTestUser = 'userA';
      dbHelper = DatabaseHelper.withDatabase(
        db,
        userIdProvider: () => currentTestUser,
      );
    });

    tearDown(() async {
      await (await dbHelper.database).close();
    });

    test('Kullanıcı A tarlaları kullanıcı B tarafından görünmez', () async {
      // User A inserts tarla
      currentTestUser = 'userA';
      await dbHelper.insertTarla(const Tarla(id: 't-userA', name: 'User A Tarlası', size: 10.0));

      var userATarlalar = await dbHelper.getTarlalar();
      expect(userATarlalar, hasLength(1));
      expect(userATarlalar.first.id, 't-userA');

      // Switch to User B
      currentTestUser = 'userB';
      var userBTarlalar = await dbHelper.getTarlalar();
      expect(userBTarlalar, isEmpty);

      // User B inserts own tarla
      await dbHelper.insertTarla(const Tarla(id: 't-userB', name: 'User B Tarlası', size: 20.0));
      userBTarlalar = await dbHelper.getTarlalar();
      expect(userBTarlalar, hasLength(1));
      expect(userBTarlalar.first.id, 't-userB');

      // Switch back to User A
      currentTestUser = 'userA';
      userATarlalar = await dbHelper.getTarlalar();
      expect(userATarlalar, hasLength(1));
      expect(userATarlalar.first.id, 't-userA');
    });

    test('Kullanıcı B kullanıcı A tarlasını güncelleyemez ve silemez', () async {
      currentTestUser = 'userA';
      await dbHelper.insertTarla(const Tarla(id: 't-userA', name: 'User A Tarlası', size: 10.0));

      currentTestUser = 'userB';
      // Attempt update
      final updateCount = await dbHelper.updateTarlaLocation('t-userA', 38.0, 35.0);
      expect(updateCount, 0);

      // Attempt delete
      final deleteCount = await dbHelper.deleteTarla('t-userA');
      expect(deleteCount, 0);

      // Switch to User A and verify tarla remains intact
      currentTestUser = 'userA';
      final list = await dbHelper.getTarlalar();
      expect(list, hasLength(1));
      expect(list.first.latitude, isNull);
    });

    test('Kullanıcı A faaliyetleri ve sync operasyonları kullanıcı B ile karışmaz', () async {
      currentTestUser = 'userA';
      await dbHelper.insertFaaliyetWithSync(
        Faaliyet(
          id: 'f-userA',
          tarlaId: 't-userA',
          type: 'Sulama',
          note: 'User A sulaması',
          timestamp: DateTime.now(),
        ),
      );

      expect(await dbHelper.getTumFaaliyetler(), hasLength(1));
      expect(await dbHelper.getPendingSyncCount(), 1);
      expect(await dbHelper.getPendingSyncOperations(), hasLength(1));

      // Switch to User B
      currentTestUser = 'userB';
      expect(await dbHelper.getTumFaaliyetler(), isEmpty);
      expect(await dbHelper.getPendingSyncCount(), 0);
      expect(await dbHelper.getPendingSyncOperations(), isEmpty);
    });

    test('clearUserData oturumu kapatılan kullanıcının yerel verilerini temizler', () async {
      currentTestUser = 'userA';
      await dbHelper.insertTarla(const Tarla(id: 't-userA', name: 'User A Tarlası'));
      await dbHelper.insertFaaliyetWithSync(
        Faaliyet(
          id: 'f-userA',
          tarlaId: 't-userA',
          type: 'Gübreleme',
          note: 'Not',
          timestamp: DateTime.now(),
        ),
      );

      currentTestUser = 'userB';
      await dbHelper.insertTarla(const Tarla(id: 't-userB', name: 'User B Tarlası'));

      // Logout / Clear data for userA
      await dbHelper.clearUserData(userId: 'userA');

      // Verify User A data is wiped
      currentTestUser = 'userA';
      expect(await dbHelper.getTarlalar(), isEmpty);
      expect(await dbHelper.getTumFaaliyetler(), isEmpty);
      expect(await dbHelper.getPendingSyncCount(), 0);

      // Verify User B data is still safe
      currentTestUser = 'userB';
      expect(await dbHelper.getTarlalar(), hasLength(1));
      expect(await dbHelper.getTarlalar().then((t) => t.first.id), 't-userB');
    });

    test('Sahipsiz eski kayıtlar otomatik olarak yeni kullanıcıya SIZMAZ ve çalınamaz (Anti-Hijacking)', () async {
      // 1. Cihazda eski kullanıcıdan / migration öncesinden kalan sahipsiz kayıtlar (userId = null)
      currentTestUser = null;
      await dbHelper.insertTarla(const Tarla(id: 't-old-user', name: 'Eski Kullanıcı Tarlası', size: 15.0));
      await dbHelper.insertFaaliyetWithSync(
        Faaliyet(
          id: 'f-old-user',
          tarlaId: 't-old-user',
          type: 'İlaçlama',
          note: 'Eski kullanıcının özel notu',
          timestamp: DateTime.now(),
        ),
      );

      // 2. Cihaza tamamen farklı bir kullanıcı (User B) giriş yapar
      currentTestUser = 'userB';

      // 3. User B sahipsiz verileri KESİNLİKLE göremez ve sync kuyruğuna alamaz
      expect(await dbHelper.getTarlalar(), isEmpty);
      expect(await dbHelper.getTumFaaliyetler(), isEmpty);
      expect(await dbHelper.getPendingSyncCount(), 0);
      expect(await dbHelper.getPendingSyncOperations(), isEmpty);
      expect(await dbHelper.getTarlaSayisi(), 0);
      expect(await dbHelper.getToplamDonum(), 0.0);

      // 4. getOrphanedDataSummary sahipsiz verilerin varlığını tespit eder
      final summary = await dbHelper.getOrphanedDataSummary();
      expect(summary.hasOrphanedData, isTrue);
      expect(summary.farmCount, 1);
      expect(summary.activityCount, 1);
      expect(summary.syncCount, 1);

      // 5. User B sahipsiz verileri temizler -> cihazdan tamamen silinir, User B'nin hesabına sızmaz
      final deleted = await dbHelper.clearOrphanedRecords();
      expect(deleted, 3); // 1 tarla + 1 faaliyet + 1 sync

      final afterSummary = await dbHelper.getOrphanedDataSummary();
      expect(afterSummary.hasOrphanedData, isFalse);

      // 6. User B'nin alanı temiz ve boştur
      expect(await dbHelper.getTarlalar(), isEmpty);
      expect(await dbHelper.getPendingSyncCount(), 0);
    });

    test('clearOrphanedRecords sahipsiz kayıtları temizler', () async {
      currentTestUser = null;
      await dbHelper.insertTarla(const Tarla(id: 't-orphan-del', name: 'Silinecek Sahipsiz Tarla'));

      final deleted = await dbHelper.clearOrphanedRecords();
      expect(deleted, greaterThanOrEqualTo(1));

      currentTestUser = null;
      expect(await dbHelper.getTarlalar(), isEmpty);
    });
  });
}

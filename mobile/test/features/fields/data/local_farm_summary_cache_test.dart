import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/database/migrations.dart';
import 'package:mobile/features/fields/data/farm_summary_cache.dart';
import 'package:mobile/services/database_helper.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  test('stores and reads a summary only for the active user', () async {
    final db = await openDatabase(inMemoryDatabasePath, singleInstance: false);
    await Migrations.v10ToV11(db);
    var activeUserId = 'user-a';
    final helper = DatabaseHelper.withDatabase(
      db,
      userIdProvider: () => activeUserId,
    );
    final cache = LocalFarmSummaryCache(databaseHelper: helper);

    await cache.write({
      'farms': [
        {
          'farm': {'id': 'farm-a'},
        },
      ],
      'upcoming_tasks': <dynamic>[],
    });

    expect((await cache.read())?.payload['farms'], isNotEmpty);
    activeUserId = 'user-b';
    expect(await cache.read(), isNull);

    await db.close();
  });
}

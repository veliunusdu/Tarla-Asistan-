import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/cases/data/local_pending_case_repository.dart';
import 'package:mobile/features/cases/domain/models/pending_case_submission.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  late Database db;

  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  setUp(() async {
    db = await databaseFactory.openDatabase(inMemoryDatabasePath);
    await db.execute('''CREATE TABLE pending_case_submissions (
      id TEXT PRIMARY KEY, user_id TEXT NOT NULL, farm_id TEXT NOT NULL,
      category TEXT NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL,
      client_operation_id TEXT NOT NULL, local_image_path TEXT,
      uploaded_media_id TEXT, created_at_utc TEXT NOT NULL,
      attempt_count INTEGER NOT NULL DEFAULT 0, last_attempt_at_utc TEXT,
      last_error_code INTEGER, state TEXT NOT NULL DEFAULT 'pending')''');
    await db.execute('CREATE UNIQUE INDEX ux_pending ON pending_case_submissions(user_id, client_operation_id)');
  });

  tearDown(() => db.close());

  test('enqueue persists user/farm/content and durable photo path', () async {
    final repo = LocalPendingCaseRepository(
      databaseProvider: () async => db,
      userIdProvider: () => 'user-a',
    );

    final id = await repo.enqueue(
      farmId: 'farm-a', category: 'DISEASE', title: 'Domates',
      description: 'Lekeler var', clientOperationId: 'op-a',
      imageBytes: Uint8List.fromList([1, 2, 3]),
    );
    final rows = await repo.getPending();
    final item = rows.single;

    expect(item.id, id);
    expect(item.userId, 'user-a');
    expect(item.farmId, 'farm-a');
    expect(item.clientOperationId, 'op-a');
    expect(item.state, PendingCaseState.pending);
    expect(item.localImagePath, isNotNull);
    expect(await File(item.localImagePath!).exists(), isTrue);

    await repo.remove(item);
    expect(await File(item.localImagePath!).exists(), isFalse);
  });

  test('does not expose another user queue and rejects duplicate operation', () async {
    final repo = LocalPendingCaseRepository(
      databaseProvider: () async => db,
      userIdProvider: () => 'user-a',
    );
    final args = {
      'farmId': 'farm-a', 'category': 'DISEASE', 'title': 'T',
      'description': 'D', 'clientOperationId': 'same-op',
    };
    await repo.enqueue(
      farmId: args['farmId']!, category: args['category']!, title: args['title']!,
      description: args['description']!, clientOperationId: args['clientOperationId']!,
    );
    expect(repo.enqueue(
      farmId: args['farmId']!, category: args['category']!, title: args['title']!,
      description: args['description']!, clientOperationId: args['clientOperationId']!,
    ), throwsA(isA<DatabaseException>()));

    final otherUserRepo = LocalPendingCaseRepository(
      databaseProvider: () async => db,
      userIdProvider: () => 'user-b',
    );
    expect(await otherUserRepo.getPending(), isEmpty);
  });
}

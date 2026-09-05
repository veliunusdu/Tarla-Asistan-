import 'dart:async';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/features/cases/data/local_pending_case_repository.dart';
import 'package:mobile/features/cases/data/pending_case_sync_service.dart';
import 'package:mobile/services/api_client.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

class _Client extends http.BaseClient {
  final calls = <String>[];
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    calls.add(request.url.path);
    final body = request.url.path.endsWith('/cases')
        ? '{"id":"case-1"}'
        : '{"id":"media-1"}';
    return http.StreamedResponse(Stream.value(body.codeUnits), 201, headers: {'content-type': 'application/json'});
  }
}

class _ConflictClient extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    return http.StreamedResponse(
      Stream.value('{"detail":"Bu client_operation_id farklı bir işlem için kullanılmış."}'.codeUnits),
      409,
      headers: {'content-type': 'application/json'},
    );
  }
}

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  test('sync sends media before case and removes the pending row', () async {
    final db = await databaseFactory.openDatabase(inMemoryDatabasePath);
    await db.execute('''CREATE TABLE pending_case_submissions (
      id TEXT PRIMARY KEY, user_id TEXT NOT NULL, farm_id TEXT NOT NULL,
      category TEXT NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL,
      client_operation_id TEXT NOT NULL, local_image_path TEXT,
      uploaded_media_id TEXT, created_at_utc TEXT NOT NULL,
      attempt_count INTEGER NOT NULL DEFAULT 0, last_attempt_at_utc TEXT,
      last_error_code INTEGER, state TEXT NOT NULL DEFAULT 'pending')''');
    final repo = LocalPendingCaseRepository(databaseProvider: () async => db, userIdProvider: () => 'user-a');
    await repo.enqueue(
      farmId: 'farm-a', category: 'DISEASE', title: 'T', description: 'D',
      clientOperationId: 'op-a',
      imageBytes: Uint8List.fromList([1, 2, 3]),
    );
    final client = _Client();
    final api = ApiClient(httpClient: client, idTokenProvider: () async => 'token');
    final synced = await PendingCaseSyncService(api, repo).syncPending();

    expect(synced, 1);
    expect(client.calls, containsAllInOrder(['/api/v1/media', '/api/v1/cases']));
    expect(await repo.getPending(), isEmpty);
    await db.execute('DROP TABLE pending_case_submissions');
    await db.close();
    api.close();
  });

  test('409 idempotency conflict becomes terminal and is not retried forever', () async {
    final db = await databaseFactory.openDatabase(inMemoryDatabasePath);
    await db.execute('''CREATE TABLE pending_case_submissions (
      id TEXT PRIMARY KEY, user_id TEXT NOT NULL, farm_id TEXT NOT NULL,
      category TEXT NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL,
      client_operation_id TEXT NOT NULL, local_image_path TEXT,
      uploaded_media_id TEXT, created_at_utc TEXT NOT NULL,
      attempt_count INTEGER NOT NULL DEFAULT 0, last_attempt_at_utc TEXT,
      last_error_code INTEGER, state TEXT NOT NULL DEFAULT 'pending')''');
    final repo = LocalPendingCaseRepository(databaseProvider: () async => db, userIdProvider: () => 'user-a');
    await repo.enqueue(
      farmId: 'farm-a', category: 'DISEASE', title: 'T', description: 'D',
      clientOperationId: 'op-a',
    );
    final api = ApiClient(httpClient: _ConflictClient(), idTokenProvider: () async => 'token');

    await PendingCaseSyncService(api, repo).syncPending();

    expect(await repo.getPending(), isEmpty);
    expect((await db.query('pending_case_submissions')).single['state'], 'failed');
    api.close();
    await db.close();
  });
}

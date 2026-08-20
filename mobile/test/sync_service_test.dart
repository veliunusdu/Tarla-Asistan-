import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/sync_operation.dart';
import 'package:mobile/services/api_client.dart';
import 'package:mobile/services/sync_service.dart';

void main() {
  test('keeps an operation queued when the connection is weak', () async {
    final store = _MemoryStore();
    final api = _FakeApiClient(failRetryable: true);
    final service = SyncService(api, database: store);

    await service.syncNow();

    expect(store.operations, hasLength(1));
    expect(store.failureCount, 1);
    expect(service.state.value.phase, SyncPhase.offline);
    expect(service.state.value.pendingCount, 1);
    await service.dispose();
    api.close();
  });

  test('removes a queued operation after a successful retry', () async {
    final store = _MemoryStore();
    final api = _FakeApiClient();
    final service = SyncService(api, database: store);

    await service.syncNow();

    expect(store.operations, isEmpty);
    expect(api.sentIds, ['operation-1']);
    expect(service.state.value.phase, SyncPhase.idle);
    expect(service.state.value.pendingCount, 0);
    await service.dispose();
    api.close();
  });
}

class _FakeApiClient extends ApiClient {
  _FakeApiClient({this.failRetryable = false});

  final bool failRetryable;
  final sentIds = <String>[];

  @override
  Future<void> sendQueued({
    required String method,
    required String endpoint,
    required Map<String, dynamic> body,
  }) async {
    if (failRetryable) {
      throw const ApiException('Zayıf bağlantı', retryable: true);
    }
    sentIds.add(body['client_operation_id'] as String);
  }
}

class _MemoryStore implements SyncOperationStore {
  final operations = <SyncOperation>[
    SyncOperation(
      id: 'operation-1',
      method: 'POST',
      endpoint: '/farms/farm-1/activities',
      payload: const {
        'client_operation_id': 'operation-1',
        'activity_type': 'OTHER',
        'description': 'Saha kontrolü',
      },
      attempts: 0,
      createdAt: DateTime.utc(2026, 8, 5),
    ),
  ];
  int failureCount = 0;

  @override
  Future<List<SyncOperation>> getPendingSyncOperations({
    int limit = 20,
  }) async => operations.take(limit).toList();

  @override
  Future<int> getPendingSyncCount() async => operations.length;

  @override
  Future<void> markSyncCompleted(String id) async {
    operations.removeWhere((operation) => operation.id == id);
  }

  @override
  Future<void> markSyncFailed(String id, String error) async {
    failureCount += 1;
  }
}

import 'dart:convert';

class SyncOperation {
  const SyncOperation({
    required this.id,
    required this.method,
    required this.endpoint,
    required this.payload,
    required this.attempts,
    required this.createdAt,
  });

  final String id;
  final String method;
  final String endpoint;
  final Map<String, dynamic> payload;
  final int attempts;
  final DateTime createdAt;

  factory SyncOperation.fromJson(Map<String, dynamic> json) => SyncOperation(
    id: json['id'] as String,
    method: json['method'] as String,
    endpoint: json['endpoint'] as String,
    payload: jsonDecode(json['payload'] as String) as Map<String, dynamic>,
    attempts: json['attempts'] as int? ?? 0,
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}

abstract interface class SyncOperationStore {
  Future<List<SyncOperation>> getPendingSyncOperations({int limit = 20});
  Future<int> getPendingSyncCount();
  Future<void> markSyncCompleted(String id);
  Future<void> markSyncFailed(String id, String error);
}

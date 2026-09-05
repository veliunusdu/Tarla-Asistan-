import 'package:flutter/foundation.dart';

/// Offline iken kuyruğa alınan görev aksiyon türü.
enum TaskActionType {
  complete,
  notApplied;

  String toJson() => switch (this) {
        TaskActionType.complete => 'COMPLETE',
        TaskActionType.notApplied => 'NOT_APPLIED',
      };

  static TaskActionType fromJson(Object? raw) {
    final value = raw?.toString().toUpperCase() ?? '';
    return switch (value) {
      'COMPLETE' => TaskActionType.complete,
      'NOT_APPLIED' || 'NOTAPPLIED' => TaskActionType.notApplied,
      _ => throw ArgumentError.value(raw, 'actionType', 'Bilinmeyen görev aksiyon türü'),
    };
  }
}

/// SQLite üzerinde bekleyen çevrimdışı görev aksiyonunu temsil eden domain model.
@immutable
class PendingTaskAction {
  const PendingTaskAction({
    required this.id,
    required this.farmId,
    required this.taskId,
    required this.actionType,
    this.reason,
    this.note,
    required this.createdAtUtc,
    this.attemptCount = 0,
    this.lastAttemptAtUtc,
    this.lastErrorCode,
    this.userId,
  });

  /// Tekil işlem kimliği (UUID).
  final String id;

  /// İşlemin ait olduğu tarla kimliği.
  final String farmId;

  /// İşlemin hedeflediği görev kimliği.
  final String taskId;

  /// Aksiyon türü (complete / notApplied).
  final TaskActionType actionType;

  /// Uygulamama gerekçesi (notApplied için zorunlu, complete için null).
  final String? reason;

  /// Tamamlama notu (complete için opsiyonel).
  final String? note;

  /// İşlemin oluşturulma zamanı (UTC). FIFO sıralaması için kullanılır.
  final DateTime createdAtUtc;

  /// Gönderim deneme sayısı.
  final int attemptCount;

  /// Son deneme zamanı (UTC).
  final DateTime? lastAttemptAtUtc;

  /// Son alınan HTTP hata kodu (örneğin 503, 500 vb.).
  final int? lastErrorCode;

  /// İşlemi gerçekleştiren kullanıcının kimliği (kullanıcı izolasyonu için).
  final String? userId;

  Map<String, dynamic> toMap() => {
        'id': id,
        'farm_id': farmId,
        'task_id': taskId,
        'action_type': actionType.toJson(),
        'reason': reason,
        'note': note,
        'created_at_utc': createdAtUtc.toUtc().toIso8601String(),
        'attempt_count': attemptCount,
        'last_attempt_at_utc': lastAttemptAtUtc?.toUtc().toIso8601String(),
        'last_error_code': lastErrorCode,
        'user_id': userId,
      };

  factory PendingTaskAction.fromMap(Map<String, dynamic> map) {
    return PendingTaskAction(
      id: map['id'] as String,
      farmId: map['farm_id'] as String,
      taskId: map['task_id'] as String,
      actionType: TaskActionType.fromJson(map['action_type']),
      reason: map['reason'] as String?,
      note: map['note'] as String?,
      createdAtUtc: DateTime.parse(map['created_at_utc'] as String).toUtc(),
      attemptCount: (map['attempt_count'] as num?)?.toInt() ?? 0,
      lastAttemptAtUtc: map['last_attempt_at_utc'] != null
          ? DateTime.tryParse(map['last_attempt_at_utc'] as String)?.toUtc()
          : null,
      lastErrorCode: (map['last_error_code'] as num?)?.toInt(),
      userId: map['user_id'] as String?,
    );
  }

  PendingTaskAction copyWith({
    String? id,
    String? farmId,
    String? taskId,
    TaskActionType? actionType,
    String? reason,
    String? note,
    DateTime? createdAtUtc,
    int? attemptCount,
    DateTime? lastAttemptAtUtc,
    int? lastErrorCode,
    String? userId,
  }) {
    return PendingTaskAction(
      id: id ?? this.id,
      farmId: farmId ?? this.farmId,
      taskId: taskId ?? this.taskId,
      actionType: actionType ?? this.actionType,
      reason: reason ?? this.reason,
      note: note ?? this.note,
      createdAtUtc: createdAtUtc ?? this.createdAtUtc,
      attemptCount: attemptCount ?? this.attemptCount,
      lastAttemptAtUtc: lastAttemptAtUtc ?? this.lastAttemptAtUtc,
      lastErrorCode: lastErrorCode ?? this.lastErrorCode,
      userId: userId ?? this.userId,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is PendingTaskAction &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          farmId == other.farmId &&
          taskId == other.taskId &&
          actionType == other.actionType &&
          reason == other.reason &&
          note == other.note &&
          attemptCount == other.attemptCount &&
          lastErrorCode == other.lastErrorCode &&
          userId == other.userId;

  @override
  int get hashCode => Object.hash(
        id,
        farmId,
        taskId,
        actionType,
        reason,
        note,
        attemptCount,
        lastErrorCode,
        userId,
      );
}

/// Senkronizasyon işleminin sonucunu temsil eder.
@immutable
class SyncResult {
  const SyncResult({
    this.syncedCount = 0,
    this.conflictCount = 0,
    this.failedCount = 0,
    this.hasAuthError = false,
  });

  final int syncedCount;
  final int conflictCount;
  final int failedCount;
  final bool hasAuthError;

  bool get hasChanges => syncedCount > 0 || conflictCount > 0;
  bool get isSuccessful => failedCount == 0 && !hasAuthError;
}

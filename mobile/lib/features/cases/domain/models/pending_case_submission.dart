import 'package:flutter/foundation.dart';

enum PendingCaseState { pending, failed }

@immutable
class PendingCaseSubmission {
  const PendingCaseSubmission({
    required this.id,
    required this.userId,
    required this.farmId,
    required this.category,
    required this.title,
    required this.description,
    required this.clientOperationId,
    required this.createdAtUtc,
    this.localImagePath,
    this.uploadedMediaId,
    this.localAudioPath,
    this.uploadedAudioMediaId,
    this.attemptCount = 0,
    this.lastAttemptAtUtc,
    this.lastErrorCode,
    this.state = PendingCaseState.pending,
  });

  final String id;
  final String userId;
  final String farmId;
  final String category;
  final String title;
  final String description;
  final String clientOperationId;
  final String? localImagePath;
  final String? uploadedMediaId;
  final String? localAudioPath;
  final String? uploadedAudioMediaId;
  final DateTime createdAtUtc;
  final int attemptCount;
  final DateTime? lastAttemptAtUtc;
  final int? lastErrorCode;
  final PendingCaseState state;

  Map<String, dynamic> toMap() => {
    'id': id,
    'user_id': userId,
    'farm_id': farmId,
    'category': category,
    'title': title,
    'description': description,
    'client_operation_id': clientOperationId,
    'local_image_path': localImagePath,
    'uploaded_media_id': uploadedMediaId,
    'local_audio_path': localAudioPath,
    'uploaded_audio_media_id': uploadedAudioMediaId,
    'created_at_utc': createdAtUtc.toUtc().toIso8601String(),
    'attempt_count': attemptCount,
    'last_attempt_at_utc': lastAttemptAtUtc?.toUtc().toIso8601String(),
    'last_error_code': lastErrorCode,
    'state': state.name,
  };

  factory PendingCaseSubmission.fromMap(Map<String, dynamic> map) =>
      PendingCaseSubmission(
        id: map['id'] as String,
        userId: map['user_id'] as String,
        farmId: map['farm_id'] as String,
        category: map['category'] as String,
        title: map['title'] as String,
        description: map['description'] as String,
        clientOperationId: map['client_operation_id'] as String,
        localImagePath: map['local_image_path'] as String?,
        uploadedMediaId: map['uploaded_media_id'] as String?,
        localAudioPath: map['local_audio_path'] as String?,
        uploadedAudioMediaId: map['uploaded_audio_media_id'] as String?,
        createdAtUtc: DateTime.parse(map['created_at_utc'] as String).toUtc(),
        attemptCount: (map['attempt_count'] as num?)?.toInt() ?? 0,
        lastAttemptAtUtc: map['last_attempt_at_utc'] == null
            ? null
            : DateTime.tryParse(map['last_attempt_at_utc'] as String)?.toUtc(),
        lastErrorCode: (map['last_error_code'] as num?)?.toInt(),
        state: PendingCaseState.values.firstWhere(
          (s) => s.name == map['state'],
          orElse: () => PendingCaseState.pending,
        ),
      );

  PendingCaseSubmission copyWith({
    String? uploadedMediaId,
    String? uploadedAudioMediaId,
    int? attemptCount,
    DateTime? lastAttemptAtUtc,
    int? lastErrorCode,
    PendingCaseState? state,
  }) => PendingCaseSubmission(
    id: id,
    userId: userId,
    farmId: farmId,
    category: category,
    title: title,
    description: description,
    clientOperationId: clientOperationId,
    localImagePath: localImagePath,
    uploadedMediaId: uploadedMediaId ?? this.uploadedMediaId,
    localAudioPath: localAudioPath,
    uploadedAudioMediaId: uploadedAudioMediaId ?? this.uploadedAudioMediaId,
    createdAtUtc: createdAtUtc,
    attemptCount: attemptCount ?? this.attemptCount,
    lastAttemptAtUtc: lastAttemptAtUtc ?? this.lastAttemptAtUtc,
    lastErrorCode: lastErrorCode ?? this.lastErrorCode,
    state: state ?? this.state,
  );
}

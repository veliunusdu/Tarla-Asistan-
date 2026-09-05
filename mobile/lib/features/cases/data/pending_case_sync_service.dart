import 'dart:io';
import '../../../services/api_client.dart';
import '../domain/models/pending_case_submission.dart';
import 'local_pending_case_repository.dart';

class PendingCaseSyncService {
  const PendingCaseSyncService(this._api, this._repository);

  final ApiClient _api;
  final LocalPendingCaseRepository _repository;

  Future<int> syncPending() async {
    final pending = await _repository.getPending();
    var synced = 0;
    for (var submission in pending) {
      try {
        if (submission.uploadedMediaId == null &&
            submission.localImagePath != null) {
          final bytes = await File(submission.localImagePath!).readAsBytes();
          final response = await _api.postMultipart(
            '/media',
            files: [
              ApiMultipartFile(
                field: 'file',
                bytes: bytes,
                filename: '${submission.id}.jpg',
                contentType: 'image/jpeg',
              ),
            ],
          );
          final mediaId = response['id']?.toString();
          if (mediaId == null || mediaId.isEmpty)
            throw const ApiException('Fotoğraf kimliği alınamadı.');
          submission = submission.copyWith(uploadedMediaId: mediaId);
          await _repository.update(submission);
        }

        if (submission.uploadedAudioMediaId == null &&
            submission.localAudioPath != null) {
          final bytes = await File(submission.localAudioPath!).readAsBytes();
          final response = await _api.postMultipart(
            '/media',
            files: [
              ApiMultipartFile(
                field: 'file',
                bytes: bytes,
                filename: '${submission.id}.m4a',
                contentType: 'audio/mp4',
              ),
            ],
          );
          final mediaId = response['id']?.toString();
          if (mediaId == null || mediaId.isEmpty)
            throw const ApiException(
              'Ses yüklendi ancak medya kimliği alınamadı.',
            );
          submission = submission.copyWith(uploadedAudioMediaId: mediaId);
          await _repository.update(submission);
        }

        final mediaIds = <String>[
          if (submission.uploadedMediaId != null) submission.uploadedMediaId!,
          if (submission.uploadedAudioMediaId != null)
            submission.uploadedAudioMediaId!,
        ];
        await _api.postJson('/cases', {
          'farm_id': submission.farmId,
          'category': submission.category,
          'title': submission.title,
          'description': submission.description,
          'media_ids': mediaIds.isEmpty ? null : mediaIds,
          'client_operation_id': submission.clientOperationId,
        });
        await _repository.remove(submission);
        synced++;
      } on ApiException catch (error) {
        final updated = submission.copyWith(
          attemptCount: submission.attemptCount + 1,
          lastAttemptAtUtc: DateTime.now().toUtc(),
          lastErrorCode: error.statusCode,
          state: _isTerminal(error)
              ? PendingCaseState.failed
              : PendingCaseState.pending,
        );
        await _repository.update(updated);
        if (!_isTerminal(error) ||
            error.statusCode == 401 ||
            error.statusCode == 403)
          return synced;
      } on FileSystemException {
        final updated = submission.copyWith(
          attemptCount: submission.attemptCount + 1,
          lastAttemptAtUtc: DateTime.now().toUtc(),
          state: PendingCaseState.failed,
        );
        await _repository.update(updated);
      }
    }
    return synced;
  }

  bool _isTerminal(ApiException error) =>
      error.statusCode == 400 ||
      error.statusCode == 404 ||
      error.statusCode == 409;
}

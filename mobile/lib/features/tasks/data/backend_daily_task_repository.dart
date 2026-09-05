// ignore_for_file: prefer_initializing_formals
import 'package:flutter/foundation.dart';

import '../../../services/api_client.dart';
import '../domain/farm_task.dart';
import '../domain/pending_task_action.dart';
import 'daily_task_repository.dart';
import 'local_daily_task_repository.dart';

/// Implements [DailyTaskRepository] by calling
/// `GET /api/v1/farms/{farmId}/tasks`.
///
/// Uses the project-standard [ApiClient] for authentication and error
/// handling.  No new HTTP client or auth mechanism is introduced.
///
/// The backend endpoint signature:
///   GET /api/v1/farms/{farmId}/tasks?date=yyyy-MM-dd
///
/// When [date] is omitted the backend defaults to today (UTC).
/// The response is a `DailyTaskListDto` with:
///   - items             : `List<TaskDto>`  (max 3, priority-sorted)
///   - criticalWeatherAlerts : `List<TaskDto>`
///   - overdue           : `List<TaskDto>`  (max 20)
///   - visibleLimit      : int (always 3)
///   - date              : DateOnly (string)
///
/// When the remote request succeeds, the result is saved to [LocalDailyTaskRepository].
/// If the remote request fails with a transient error, cached data for [farmId] is returned as fallback
/// with [DailyTaskList.isFromCache] set to `true`.
/// For non-transient errors (400, 401, 403, 404), the exception is rethrown and cache is not returned.
class BackendDailyTaskRepository implements DailyTaskRepository {
  BackendDailyTaskRepository({
    required ApiClient apiClient,
    LocalDailyTaskRepository localRepo = const LocalDailyTaskRepository(),
  })  : _api = apiClient,
        _localRepo = localRepo;

  final ApiClient _api;
  final LocalDailyTaskRepository _localRepo;
  bool _isSyncing = false;

  bool _isTransientError(Object error) {
    if (error is ApiException) {
      final code = error.statusCode;
      if (code == 400 || code == 401 || code == 403 || code == 404) {
        return false;
      }
    }
    return true;
  }

  Future<DailyTaskList> _decorateWithPendingActions(
    String farmId,
    DailyTaskList list,
  ) async {
    try {
      final pendingMap = await _localRepo.getPendingActionsMap(farmId: farmId);
      if (pendingMap.isEmpty) return list;

      FarmTask decorate(FarmTask task) {
        final pending = pendingMap[task.id];
        if (pending != null) {
          return task.copyWith(pendingAction: pending);
        }
        return task;
      }

      return DailyTaskList(
        date: list.date,
        items: list.items.map(decorate).toList(),
        criticalWeatherAlerts: list.criticalWeatherAlerts.map(decorate).toList(),
        overdue: list.overdue.map(decorate).toList(),
        visibleLimit: list.visibleLimit,
        isFromCache: list.isFromCache,
        cachedAt: list.cachedAt,
      );
    } catch (e) {
      debugPrint('BackendDailyTaskRepository: decorate failed: $e');
      return list;
    }
  }

  @override
  Future<DailyTaskList> getDailyTasks(String farmId) async {
    // Uzak servise gitmeden önce bu tarla için bekleyen çevrimdışı işlemleri senkronize etmeyi dene
    try {
      await syncPendingTaskActions(farmId: farmId);
    } catch (e) {
      debugPrint('BackendDailyTaskRepository: auto-sync failed: $e');
    }

    try {
      final raw = await _api.getJson('/farms/$farmId/tasks');
      final taskList = DailyTaskList.fromJson(raw);
      try {
        await _localRepo.cacheTasks(farmId: farmId, tasks: taskList);
      } catch (e) {
        debugPrint('BackendDailyTaskRepository: cache write failed: $e');
      }
      return await _decorateWithPendingActions(farmId, taskList);
    } catch (error) {
      if (!_isTransientError(error)) {
        rethrow;
      }
      try {
        final cached = await _localRepo.getCachedTasks(farmId);
        if (cached != null) {
          return await _decorateWithPendingActions(farmId, cached);
        }
      } catch (e) {
        debugPrint('BackendDailyTaskRepository: cache read failed: $e');
      }
      rethrow;
    }
  }

  @override
  Future<void> completeTask({
    required String farmId,
    required String taskId,
    String? note,
  }) async {
    await _api.postJson(
      '/tasks/$taskId/complete',
      {
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
    );
  }

  @override
  Future<void> markTaskNotApplied({
    required String farmId,
    required String taskId,
    required String reason,
  }) async {
    final trimmedReason = reason.trim();
    if (trimmedReason.isEmpty) {
      throw ArgumentError.value(
        reason,
        'reason',
        'Uygulanmama gerekçesi boş veya yalnızca boşluklardan oluşamaz.',
      );
    }

    await _api.patchJson(
      '/tasks/$taskId/status',
      {
        'status': 'NOT_APPLIED',
        'notAppliedReason': trimmedReason,
      },
    );
  }

  @override
  Future<void> enqueueTaskAction(PendingTaskAction action) async {
    await _localRepo.enqueuePendingAction(action);
  }

  @override
  Future<List<PendingTaskAction>> getPendingActions({String? farmId}) async {
    return await _localRepo.getPendingActions(farmId: farmId);
  }

  @override
  Future<SyncResult> syncPendingTaskActions({String? farmId}) async {
    if (_isSyncing) {
      return const SyncResult();
    }
    _isSyncing = true;
    var syncedCount = 0;
    var conflictCount = 0;
    var failedCount = 0;
    var hasAuthError = false;

    try {
      final actions = await _localRepo.getPendingActions(farmId: farmId);
      for (final action in actions) {
        try {
          if (action.actionType == TaskActionType.complete) {
            await _api.postJson(
              '/tasks/${action.taskId}/complete',
              {
                if (action.note != null && action.note!.trim().isNotEmpty)
                  'note': action.note!.trim(),
              },
            );
          } else if (action.actionType == TaskActionType.notApplied) {
            final reason = action.reason?.trim() ?? '';
            if (reason.isEmpty) {
              await _localRepo.removePendingAction(action.id);
              failedCount++;
              continue;
            }
            await _api.patchJson(
              '/tasks/${action.taskId}/status',
              {
                'status': 'NOT_APPLIED',
                'notAppliedReason': reason,
              },
            );
          }

          // Başarılı (200 / 204): Kuyruktan kaldır
          await _localRepo.removePendingAction(action.id);
          syncedCount++;
        } on ApiException catch (e) {
          final code = e.statusCode;
          if (code == 409) {
            // Conflict: Görev sunucuda zaten sonuçlandırılmış
            await _localRepo.removePendingAction(action.id);
            conflictCount++;
          } else if (code == 404 || code == 400) {
            // Terminal hata: Görev silinmiş veya geçersiz veri
            await _localRepo.removePendingAction(action.id);
            failedCount++;
          } else if (code == 401 || code == 403) {
            // Yetkilendirme hatası: Kuyrukta TUT, senkronizasyonu durdur
            hasAuthError = true;
            failedCount++;
            break;
          } else {
            // 5xx veya geçici sunucu hataları: Kuyrukta TUT, attempt güncelle, durdur
            final updated = action.copyWith(
              attemptCount: action.attemptCount + 1,
              lastAttemptAtUtc: DateTime.now().toUtc(),
              lastErrorCode: code,
            );
            await _localRepo.updatePendingAction(updated);
            failedCount++;
            break;
          }
        } catch (e) {
          // Ağ / Soket / Zaman aşımı hatası: Kuyrukta TUT, attempt güncelle, durdur
          final updated = action.copyWith(
            attemptCount: action.attemptCount + 1,
            lastAttemptAtUtc: DateTime.now().toUtc(),
          );
          await _localRepo.updatePendingAction(updated);
          failedCount++;
          break;
        }
      }
    } finally {
      _isSyncing = false;
    }

    return SyncResult(
      syncedCount: syncedCount,
      conflictCount: conflictCount,
      failedCount: failedCount,
      hasAuthError: hasAuthError,
    );
  }
}

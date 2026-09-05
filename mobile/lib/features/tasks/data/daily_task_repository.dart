import '../domain/farm_task.dart';
import '../domain/pending_task_action.dart';

/// Repository abstraction for the daily-task feature.
///
/// Follows the same interface-per-file convention used by
/// [WeatherRepository] and [FaaliyetRepository] in this project.
///
/// The backend endpoint returns all task categories in one call, so a
/// single [getDailyTasks] method is sufficient for this feature.
abstract interface class DailyTaskRepository {
  /// Fetches today's task list for [farmId].
  ///
  /// Calls `GET /api/v1/farms/{farmId}/tasks` and returns a [DailyTaskList]
  /// that contains regular tasks, critical weather alerts, and overdue tasks.
  ///
  /// Throws [ApiException] on network or server errors (propagated from
  /// [ApiClient] without wrapping, consistent with the rest of the project).
  Future<DailyTaskList> getDailyTasks(String farmId);

  /// Marks the task [taskId] on [farmId] as completed.
  ///
  /// Optionally attaches a [note] to the task completion record.
  ///
  /// Calls `POST /api/v1/tasks/{taskId}/complete`.
  /// Throws [ApiException] on network or server errors.
  Future<void> completeTask({
    required String farmId,
    required String taskId,
    String? note,
  });

  /// Marks the task [taskId] on [farmId] as not applied with a valid [reason].
  ///
  /// Calls `PATCH /api/v1/tasks/{taskId}/status` with `NOT_APPLIED`.
  /// Throws [ArgumentError] if [reason] is empty or contains only whitespace.
  /// Throws [ApiException] on network or server errors.
  Future<void> markTaskNotApplied({
    required String farmId,
    required String taskId,
    required String reason,
  });

  /// Enqueues a task action locally in SQLite for offline persistence and later synchronization.
  Future<void> enqueueTaskAction(PendingTaskAction action);

  /// Retrieves any pending task actions queued locally.
  /// If [farmId] is provided, only actions for that farm are returned.
  Future<List<PendingTaskAction>> getPendingActions({String? farmId});

  /// Synchronizes pending task actions with backend in FIFO order.
  /// If [farmId] is provided, only actions for that farm are synchronized.
  Future<SyncResult> syncPendingTaskActions({String? farmId});
}


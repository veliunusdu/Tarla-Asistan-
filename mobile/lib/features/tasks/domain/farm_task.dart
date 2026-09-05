import 'pending_task_action.dart';
import 'task_enums.dart';

/// Domain model for a single agronomic task returned by
/// `GET /api/v1/farms/{farmId}/tasks`.
///
/// Field names follow the project''s Dart/Flutter camelCase convention.
/// The [fromJson] factory handles the backend''s camelCase JSON serialisation.
///
/// All optional backend fields are represented as nullable — a missing or null
/// JSON value must not produce a fake default.
class FarmTask {
  const FarmTask({
    required this.id,
    required this.farmId,
    required this.title,
    required this.description,
    required this.reason,
    required this.priority,
    required this.status,
    required this.source,
    required this.confidence,
    required this.dueDate,
    required this.expertReviewRecommended,
    this.cropPeriodId,
    this.createdById,
    this.notAppliedReason,
    this.completionNote,
    this.photoUrl,
    this.viewedAtUtc,
    this.completedAtUtc,
    this.createdAtUtc,
    this.updatedAtUtc,
    this.pendingAction,
  });

  final String id;
  final String farmId;

  /// Short task title shown to the farmer (max 160 chars on backend).
  final String title;

  /// Longer description / instructions.
  final String description;

  /// Why this task was created — must be shown to the farmer.
  final String reason;

  final TaskPriority priority;
  final TaskStatus status;
  final TaskSource source;
  final TaskConfidence confidence;

  /// Due date (date part only — time is midnight UTC on backend).
  final DateTime? dueDate;

  /// True when confidence is Low — UI should suggest expert review.
  final bool expertReviewRecommended;

  // Optional / nullable backend fields
  final String? cropPeriodId;
  final String? createdById;
  final String? notAppliedReason;
  final String? completionNote;
  final String? photoUrl;
  final DateTime? viewedAtUtc;
  final DateTime? completedAtUtc;
  final DateTime? createdAtUtc;
  final DateTime? updatedAtUtc;

  /// Bu görev için yerel SQLite kuyruğunda bekleyen çevrimdışı işlem (varsa).
  final PendingTaskAction? pendingAction;

  /// Bu görev için gönderilmeyi bekleyen bir çevrimdışı işlem olup olmadığını belirtir.
  bool get hasPendingAction => pendingAction != null;

  /// Constructs a [FarmTask] from the backend JSON response.
  ///
  /// The backend serialises enums as PascalCase strings (e.g. `"High"`,
  /// `"CropCalendar"`) because ASP.NET uses [System.Text.Json] with default
  /// settings.  [TaskPriority.fromJson] etc. normalise to uppercase before
  /// matching, so both `"HIGH"` and `"High"` are handled correctly.
  ///
  /// A [FormatException] or null from [DateTime.tryParse] is treated as null
  /// rather than crashing the whole list parse.
  factory FarmTask.fromJson(Map<String, dynamic> json) {
    return FarmTask(
      id: (json['id'] ?? '').toString(),
      farmId: (json['farmId'] ?? json['farm_id'] ?? '').toString(),
      title: (json['title'] ?? '').toString(),
      description: (json['description'] ?? '').toString(),
      reason: (json['reason'] ?? '').toString(),
      priority: TaskPriority.fromJson(json['priority']),
      status: TaskStatus.fromJson(json['status']),
      source: TaskSource.fromJson(json['source']),
      confidence: TaskConfidence.fromJson(json['confidence']),
      dueDate: _parseDate(json['dueDate'] ?? json['due_date']),
      expertReviewRecommended:
          (json['expertReviewRecommended'] ?? json['expert_review_recommended'])
              as bool? ??
          (TaskConfidence.fromJson(json['confidence']) == TaskConfidence.low),
      cropPeriodId:
          (json['cropPeriodId'] ?? json['crop_period_id'])?.toString(),
      createdById: (json['createdById'] ?? json['created_by_id'])?.toString(),
      notAppliedReason:
          (json['notAppliedReason'] ?? json['not_applied_reason'])?.toString(),
      completionNote:
          (json['completionNote'] ?? json['completion_note'])?.toString(),
      photoUrl: (json['photoUrl'] ?? json['photo_url'])?.toString(),
      viewedAtUtc: _parseUtc(json['viewedAtUtc'] ?? json['viewed_at_utc']),
      completedAtUtc:
          _parseUtc(json['completedAtUtc'] ?? json['completed_at_utc']),
      createdAtUtc: _parseUtc(json['createdAtUtc'] ?? json['created_at_utc']),
      updatedAtUtc: _parseUtc(json['updatedAtUtc'] ?? json['updated_at_utc']),
    );
  }

  /// Returns a copy of this [FarmTask] with updated values.
  FarmTask copyWith({
    String? id,
    String? farmId,
    String? title,
    String? description,
    String? reason,
    TaskPriority? priority,
    TaskStatus? status,
    TaskSource? source,
    TaskConfidence? confidence,
    DateTime? dueDate,
    bool? expertReviewRecommended,
    String? cropPeriodId,
    String? createdById,
    String? notAppliedReason,
    String? completionNote,
    String? photoUrl,
    DateTime? viewedAtUtc,
    DateTime? completedAtUtc,
    DateTime? createdAtUtc,
    DateTime? updatedAtUtc,
    PendingTaskAction? pendingAction,
    bool clearPendingAction = false,
  }) {
    return FarmTask(
      id: id ?? this.id,
      farmId: farmId ?? this.farmId,
      title: title ?? this.title,
      description: description ?? this.description,
      reason: reason ?? this.reason,
      priority: priority ?? this.priority,
      status: status ?? this.status,
      source: source ?? this.source,
      confidence: confidence ?? this.confidence,
      dueDate: dueDate ?? this.dueDate,
      expertReviewRecommended:
          expertReviewRecommended ?? this.expertReviewRecommended,
      cropPeriodId: cropPeriodId ?? this.cropPeriodId,
      createdById: createdById ?? this.createdById,
      notAppliedReason: notAppliedReason ?? this.notAppliedReason,
      completionNote: completionNote ?? this.completionNote,
      photoUrl: photoUrl ?? this.photoUrl,
      viewedAtUtc: viewedAtUtc ?? this.viewedAtUtc,
      completedAtUtc: completedAtUtc ?? this.completedAtUtc,
      createdAtUtc: createdAtUtc ?? this.createdAtUtc,
      updatedAtUtc: updatedAtUtc ?? this.updatedAtUtc,
      pendingAction:
          clearPendingAction ? null : (pendingAction ?? this.pendingAction),
    );
  }

  /// Converts [FarmTask] to a JSON-compatible map for caching.
  Map<String, dynamic> toJson() => {
        'id': id,
        'farmId': farmId,
        'title': title,
        'description': description,
        'reason': reason,
        'priority': priority.toJson(),
        'status': status.toJson(),
        'source': source.toJson(),
        'confidence': confidence.toJson(),
        'dueDate': dueDate != null
            ? '${dueDate!.year.toString().padLeft(4, '0')}-${dueDate!.month.toString().padLeft(2, '0')}-${dueDate!.day.toString().padLeft(2, '0')}'
            : null,
        'expertReviewRecommended': expertReviewRecommended,
        'cropPeriodId': cropPeriodId,
        'createdById': createdById,
        'notAppliedReason': notAppliedReason,
        'completionNote': completionNote,
        'photoUrl': photoUrl,
        'viewedAtUtc': viewedAtUtc?.toUtc().toIso8601String(),
        'completedAtUtc': completedAtUtc?.toUtc().toIso8601String(),
        'createdAtUtc': createdAtUtc?.toUtc().toIso8601String(),
        'updatedAtUtc': updatedAtUtc?.toUtc().toIso8601String(),
      };

  /// Parses a `DateOnly` style string (`"2026-09-05"`) as a midnight local
  /// [DateTime].  Returns null when the value is null or unparseable.
  static DateTime? _parseDate(Object? raw) {
    if (raw == null) return null;
    // DateOnly arrives as "yyyy-MM-dd"; DateTime.tryParse handles this.
    return DateTime.tryParse(raw.toString());
  }

  /// Parses a UTC ISO-8601 timestamp and converts to local time.
  static DateTime? _parseUtc(Object? raw) {
    if (raw == null) return null;
    final parsed = DateTime.tryParse(raw.toString());
    return parsed?.toLocal();
  }
}

/// Represents the full daily-task response from
/// `GET /api/v1/farms/{farmId}/tasks`.
///
/// [items] — up to 3 regular tasks, ordered by priority descending.
/// [criticalWeatherAlerts] — critical weather tasks shown above the 3-limit.
/// [overdue] — tasks whose due date is in the past (up to 20).
/// [visibleLimit] — always 3 per backend contract.
class DailyTaskList {
  const DailyTaskList({
    required this.date,
    required this.items,
    required this.criticalWeatherAlerts,
    required this.overdue,
    this.visibleLimit = 3,
    this.isFromCache = false,
    this.cachedAt,
  });

  final DateTime date;
  final List<FarmTask> items;
  final List<FarmTask> criticalWeatherAlerts;
  final List<FarmTask> overdue;
  final int visibleLimit;

  /// True when this data was loaded from local offline cache instead of live API.
  final bool isFromCache;

  /// The timestamp when this data was cached locally.
  final DateTime? cachedAt;

  /// Parses the [DailyTaskListDto] JSON from the backend or local cache.
  factory DailyTaskList.fromJson(Map<String, dynamic> json) {
    final cachedAtRaw = json['cachedAt'] ?? json['cached_at_utc'];
    final cachedAt = cachedAtRaw != null
        ? DateTime.tryParse(cachedAtRaw.toString())?.toLocal()
        : null;

    return DailyTaskList(
      date: DateTime.tryParse(
                (json['date'] ?? '').toString(),
              ) ??
          DateTime.now(),
      items: _parseTasks(json['items']),
      criticalWeatherAlerts: _parseTasks(
        json['criticalWeatherAlerts'] ?? json['critical_weather_alerts'],
      ),
      overdue: _parseTasks(json['overdue']),
      visibleLimit: (json['visibleLimit'] ?? json['visible_limit'] ?? 3) as int,
      isFromCache:
          (json['isFromCache'] ?? json['is_from_cache']) as bool? ?? false,
      cachedAt: cachedAt,
    );
  }

  /// Serialises the daily task list for offline caching.
  Map<String, dynamic> toJson() => {
        'date':
            '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}',
        'items': items.map((t) => t.toJson()).toList(),
        'criticalWeatherAlerts':
            criticalWeatherAlerts.map((t) => t.toJson()).toList(),
        'overdue': overdue.map((t) => t.toJson()).toList(),
        'visibleLimit': visibleLimit,
        'isFromCache': isFromCache,
        'cachedAt': cachedAt?.toUtc().toIso8601String(),
      };

  /// Returns a copy of [DailyTaskList] with optional updated fields.
  DailyTaskList copyWith({
    DateTime? date,
    List<FarmTask>? items,
    List<FarmTask>? criticalWeatherAlerts,
    List<FarmTask>? overdue,
    int? visibleLimit,
    bool? isFromCache,
    DateTime? cachedAt,
  }) {
    return DailyTaskList(
      date: date ?? this.date,
      items: items ?? this.items,
      criticalWeatherAlerts:
          criticalWeatherAlerts ?? this.criticalWeatherAlerts,
      overdue: overdue ?? this.overdue,
      visibleLimit: visibleLimit ?? this.visibleLimit,
      isFromCache: isFromCache ?? this.isFromCache,
      cachedAt: cachedAt ?? this.cachedAt,
    );
  }

  static List<FarmTask> _parseTasks(Object? raw) {
    if (raw == null) return const [];
    final list = raw as List<dynamic>;
    final result = <FarmTask>[];
    for (final item in list) {
      try {
        result.add(FarmTask.fromJson(item as Map<String, dynamic>));
      } catch (_) {
        // Skip individual malformed task entries rather than crashing the list.
        // Errors are intentionally not silenced in debug — rethrow in tests.
        assert(false, 'FarmTask.fromJson failed for entry: $item');
      }
    }
    return result;
  }

  /// True when there are no tasks to show — used by UI empty-state logic.
  bool get isEmpty =>
      items.isEmpty &&
      criticalWeatherAlerts.isEmpty &&
      overdue.isEmpty;
}

// ---------------------------------------------------------------------------
// Type aliases matching the Turkish domain terms specified in the requirements
// ---------------------------------------------------------------------------

/// Type alias for [FarmTask] representing a single daily task.
typedef GunlukGorev = FarmTask;

/// Type alias for [FarmTask] representing a critical weather alert.
///
/// In the backend (`GET /api/v1/farms/{farmId}/tasks`), critical weather
/// alerts are returned as a list of [TaskDto] where `Source == TaskSource.Weather`
/// and `Priority == TaskPriority.Critical`.
typedef KritikHavaUyarisi = FarmTask;

/// Type alias for [DailyTaskList] representing the complete daily task payload.
typedef GunlukGorevListesi = DailyTaskList;

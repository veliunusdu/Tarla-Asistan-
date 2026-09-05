/// Type-safe enumerations for the daily task feature.
///
/// Each enum provides a [fromJson] factory that performs case-insensitive
/// string matching and returns a safe [unknown] sentinel when an unrecognised
/// value arrives from the backend.  This makes the mobile app forward-
/// compatible with future backend additions.
library;

// ---------------------------------------------------------------------------
// TaskPriority
// ---------------------------------------------------------------------------

enum TaskPriority {
  low,
  medium,
  high,
  critical,

  /// Sentinel for unrecognised values from the backend.
  unknown;

  /// Parses a backend priority string (case-insensitive).
  ///
  /// Examples: `"Low"`, `"MEDIUM"`, `"Critical"` -> corresponding variant.
  /// Any unrecognised string -> [TaskPriority.unknown].
  static TaskPriority fromJson(Object? raw) {
    final value = raw?.toString().toUpperCase() ?? '';
    return switch (value) {
      'LOW' => TaskPriority.low,
      'MEDIUM' => TaskPriority.medium,
      'HIGH' => TaskPriority.high,
      'CRITICAL' => TaskPriority.critical,
      _ => TaskPriority.unknown,
    };
  }

  /// Returns a backend-compatible uppercase string representation.
  String toJson() => switch (this) {
        TaskPriority.low => 'LOW',
        TaskPriority.medium => 'MEDIUM',
        TaskPriority.high => 'HIGH',
        TaskPriority.critical => 'CRITICAL',
        TaskPriority.unknown => 'UNKNOWN',
      };

  /// Returns a display-friendly Turkish label.
  String get label => switch (this) {
        TaskPriority.low => 'Düşük',
        TaskPriority.medium => 'Orta',
        TaskPriority.high => 'Yüksek',
        TaskPriority.critical => 'Kritik',
        TaskPriority.unknown => '',
      };

  /// True for critical tasks that appear above the regular 3-task limit.
  bool get isCritical => this == TaskPriority.critical;
}

// ---------------------------------------------------------------------------
// TaskSource
// ---------------------------------------------------------------------------

enum TaskSource {
  system,
  cropCalendar,
  weather,
  expert,
  manual,

  /// Sentinel for unrecognised values.
  unknown;

  static TaskSource fromJson(Object? raw) {
    final value = raw?.toString().toUpperCase() ?? '';
    return switch (value) {
      'SYSTEM' => TaskSource.system,
      'CROPCALENDAR' || 'CROP_CALENDAR' => TaskSource.cropCalendar,
      'WEATHER' => TaskSource.weather,
      'EXPERT' => TaskSource.expert,
      'MANUAL' => TaskSource.manual,
      _ => TaskSource.unknown,
    };
  }

  /// Returns a backend-compatible uppercase string representation.
  String toJson() => switch (this) {
        TaskSource.system => 'SYSTEM',
        TaskSource.cropCalendar => 'CROPCALENDAR',
        TaskSource.weather => 'WEATHER',
        TaskSource.expert => 'EXPERT',
        TaskSource.manual => 'MANUAL',
        TaskSource.unknown => 'UNKNOWN',
      };

  /// Display label in Turkish.
  String get label => switch (this) {
        TaskSource.system => 'Sistem',
        TaskSource.cropCalendar => 'Ekim takvimi',
        TaskSource.weather => 'Hava durumu',
        TaskSource.expert => 'Uzman',
        TaskSource.manual => 'Manuel',
        TaskSource.unknown => '',
      };
}

// ---------------------------------------------------------------------------
// TaskStatus
// ---------------------------------------------------------------------------

enum TaskStatus {
  newTask,
  viewed,
  planned,
  completed,
  notApplied,
  overdue,
  cancelled,

  /// Sentinel for unrecognised values.
  unknown;

  static TaskStatus fromJson(Object? raw) {
    final value = raw?.toString().toUpperCase() ?? '';
    return switch (value) {
      'NEW' || 'NEWTASK' => TaskStatus.newTask,
      'VIEWED' => TaskStatus.viewed,
      'PLANNED' => TaskStatus.planned,
      'COMPLETED' => TaskStatus.completed,
      'NOTAPPLIED' || 'NOT_APPLIED' => TaskStatus.notApplied,
      'OVERDUE' => TaskStatus.overdue,
      'CANCELLED' => TaskStatus.cancelled,
      _ => TaskStatus.unknown,
    };
  }

  /// Returns a backend-compatible uppercase string representation.
  String toJson() => switch (this) {
        TaskStatus.newTask => 'NEW',
        TaskStatus.viewed => 'VIEWED',
        TaskStatus.planned => 'PLANNED',
        TaskStatus.completed => 'COMPLETED',
        TaskStatus.notApplied => 'NOT_APPLIED',
        TaskStatus.overdue => 'OVERDUE',
        TaskStatus.cancelled => 'CANCELLED',
        TaskStatus.unknown => 'UNKNOWN',
      };

  bool get isTerminal =>
      this == TaskStatus.completed ||
      this == TaskStatus.notApplied ||
      this == TaskStatus.cancelled;

  bool get isActive =>
      this == TaskStatus.newTask ||
      this == TaskStatus.viewed ||
      this == TaskStatus.planned;
}

// ---------------------------------------------------------------------------
// TaskConfidence
// ---------------------------------------------------------------------------

enum TaskConfidence {
  low,
  medium,
  high,

  /// Sentinel for unrecognised values.
  unknown;

  static TaskConfidence fromJson(Object? raw) {
    final value = raw?.toString().toUpperCase() ?? '';
    return switch (value) {
      'LOW' => TaskConfidence.low,
      'MEDIUM' => TaskConfidence.medium,
      'HIGH' => TaskConfidence.high,
      _ => TaskConfidence.unknown,
    };
  }

  /// Returns a backend-compatible uppercase string representation.
  String toJson() => switch (this) {
        TaskConfidence.low => 'LOW',
        TaskConfidence.medium => 'MEDIUM',
        TaskConfidence.high => 'HIGH',
        TaskConfidence.unknown => 'UNKNOWN',
      };
}

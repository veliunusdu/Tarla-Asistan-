enum NotificationTargetType { task, supportCase, advisory, weather, unknown }

class NotificationTarget {
  const NotificationTarget({
    required this.type,
    required this.resourceId,
    this.farmId,
  });

  final NotificationTargetType type;
  final String resourceId;
  final String? farmId;

  static NotificationTarget? fromData(Map<String, dynamic> data) {
    final deepLink = data['deep_link']?.toString();
    if (deepLink != null && deepLink.isNotEmpty) {
      final parsed = tryParse(deepLink);
      if (parsed != null) return parsed;
    }
    final caseId = data['case_id']?.toString();
    if (caseId != null && caseId.isNotEmpty) {
      return NotificationTarget(
        type: NotificationTargetType.supportCase,
        resourceId: caseId,
      );
    }
    final taskId = data['task_id']?.toString();
    if (taskId != null && taskId.isNotEmpty) {
      return NotificationTarget(
        type: NotificationTargetType.task,
        resourceId: taskId,
        farmId: data['farm_id']?.toString(),
      );
    }
    final advisoryId = data['advisory_id']?.toString();
    if (advisoryId != null && advisoryId.isNotEmpty) {
      return NotificationTarget(
        type: NotificationTargetType.advisory,
        resourceId: advisoryId,
        farmId: data['farm_id']?.toString(),
      );
    }
    return null;
  }

  static NotificationTarget? tryParse(String value) {
    final uri = Uri.tryParse(value);
    if (uri == null || uri.scheme != 'tarla-asistani') return null;
    final segments = uri.pathSegments;
    if (uri.host == 'cases' && segments.isNotEmpty) {
      return NotificationTarget(
        type: NotificationTargetType.supportCase,
        resourceId: segments.first,
      );
    }
    if (uri.host == 'farms' && segments.length >= 2) {
      final farmId = segments[0];
      if (segments[1] == 'tasks' && segments.length >= 3) {
        return NotificationTarget(
          type: NotificationTargetType.task,
          resourceId: segments[2],
          farmId: farmId,
        );
      }
      if (segments[1] == 'weather') {
        return NotificationTarget(
          type: NotificationTargetType.weather,
          resourceId: farmId,
          farmId: farmId,
        );
      }
      if (segments[1] == 'advisories' && segments.length >= 3) {
        return NotificationTarget(
          type: NotificationTargetType.advisory,
          resourceId: segments[2],
          farmId: farmId,
        );
      }
    }
    return const NotificationTarget(
      type: NotificationTargetType.unknown,
      resourceId: '',
    );
  }
}

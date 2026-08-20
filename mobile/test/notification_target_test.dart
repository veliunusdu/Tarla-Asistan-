import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/notification_target.dart';

void main() {
  group('NotificationTarget', () {
    test('parses task deep link', () {
      final target = NotificationTarget.tryParse(
        'tarla-asistani://farms/farm-1/tasks/task-1',
      );

      expect(target?.type, NotificationTargetType.task);
      expect(target?.farmId, 'farm-1');
      expect(target?.resourceId, 'task-1');
    });

    test('parses case deep link', () {
      final target = NotificationTarget.fromData({
        'deep_link': 'tarla-asistani://cases/case-1',
      });

      expect(target?.type, NotificationTargetType.supportCase);
      expect(target?.resourceId, 'case-1');
    });

    test('parses weather deep link and falls back to payload ids', () {
      final weather = NotificationTarget.tryParse(
        'tarla-asistani://farms/farm-1/weather?task_id=task-1',
      );
      final task = NotificationTarget.fromData({
        'farm_id': 'farm-2',
        'task_id': 'task-2',
      });

      expect(weather?.type, NotificationTargetType.weather);
      expect(weather?.farmId, 'farm-1');
      expect(task?.type, NotificationTargetType.task);
      expect(task?.resourceId, 'task-2');
    });

    test('rejects external links', () {
      expect(
        NotificationTarget.tryParse('https://example.com/tasks/1'),
        isNull,
      );
    });
  });
}

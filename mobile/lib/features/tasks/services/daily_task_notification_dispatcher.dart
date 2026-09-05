import 'package:flutter/material.dart';

import '../../../models/notification_target.dart';

abstract class DailyTaskNotificationDispatcher {
  Future<void> showNotification({
    required int id,
    required String title,
    required String body,
    NotificationTarget? target,
  });

  Future<void> scheduleDaily({
    required int id,
    required TimeOfDay time,
    required String title,
    required String body,
    NotificationTarget? target,
  });

  Future<void> cancel(int id);
}

class InAppNotificationDispatcher implements DailyTaskNotificationDispatcher {
  InAppNotificationDispatcher({
    this.messengerKey,
    this.navigatorKey,
  });

  final GlobalKey<ScaffoldMessengerState>? messengerKey;
  final GlobalKey<NavigatorState>? navigatorKey;

  @override
  Future<void> showNotification({
    required int id,
    required String title,
    required String body,
    NotificationTarget? target,
  }) async {
    messengerKey?.currentState
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 2),
              Text(body),
            ],
          ),
          action: target == null
              ? null
              : SnackBarAction(
                  label: 'Aç',
                  onPressed: () {
                    navigatorKey?.currentState?.pushNamed(
                      '/notification-target',
                      arguments: target,
                    );
                  },
                ),
        ),
      );
  }

  @override
  Future<void> scheduleDaily({
    required int id,
    required TimeOfDay time,
    required String title,
    required String body,
    NotificationTarget? target,
  }) async {
    // In mobile without external OS workers, schedule config is preserved in preferences.
  }

  @override
  Future<void> cancel(int id) async {
    // Cancel schedule
  }
}

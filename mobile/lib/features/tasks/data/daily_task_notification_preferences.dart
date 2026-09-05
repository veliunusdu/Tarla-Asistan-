import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class DailyTaskNotificationPreferences {
  static const String keyDailyTasksNotificationEnabled =
      'daily_tasks_notification_enabled';
  static const String keyDailyTaskNotificationTime =
      'daily_task_notification_time';
  static const String keyLastDailyNotificationDate =
      'last_daily_notification_date';
  static const String keyNotifiedCriticalAlerts =
      'notified_critical_alerts';
  static const String keyLastSelectedFarmId =
      'last_selected_farm_id';

  Future<bool> isDailyTasksNotificationEnabled() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool(keyDailyTasksNotificationEnabled) ?? true;
  }

  Future<void> setDailyTasksNotificationEnabled(bool enabled) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(keyDailyTasksNotificationEnabled, enabled);
  }

  Future<TimeOfDay> getReminderTime() async {
    final prefs = await SharedPreferences.getInstance();
    final timeStr = prefs.getString(keyDailyTaskNotificationTime) ?? '08:00';
    final parts = timeStr.split(':');
    if (parts.length == 2) {
      final hour = int.tryParse(parts[0]);
      final minute = int.tryParse(parts[1]);
      if (hour != null && minute != null) {
        return TimeOfDay(hour: hour, minute: minute);
      }
    }
    return const TimeOfDay(hour: 8, minute: 0);
  }

  Future<void> setReminderTime(TimeOfDay time) async {
    final prefs = await SharedPreferences.getInstance();
    final hourStr = time.hour.toString().padLeft(2, '0');
    final minStr = time.minute.toString().padLeft(2, '0');
    await prefs.setString(keyDailyTaskNotificationTime, '$hourStr:$minStr');
  }

  Future<String?> getLastDailyNotificationDate() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(keyLastDailyNotificationDate);
  }

  Future<void> setLastDailyNotificationDate(String dateStr) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(keyLastDailyNotificationDate, dateStr);
  }

  Future<Set<String>> getNotifiedCriticalAlertKeys() async {
    final prefs = await SharedPreferences.getInstance();
    final list = prefs.getStringList(keyNotifiedCriticalAlerts) ?? [];
    return list.toSet();
  }

  Future<void> markCriticalAlertNotified(String alertKey) async {
    final prefs = await SharedPreferences.getInstance();
    final list = prefs.getStringList(keyNotifiedCriticalAlerts) ?? [];
    if (!list.contains(alertKey)) {
      list.add(alertKey);
      // Keep only last 100 alerts to prevent unbounded growth
      if (list.length > 100) {
        list.removeRange(0, list.length - 100);
      }
      await prefs.setStringList(keyNotifiedCriticalAlerts, list);
    }
  }

  Future<bool> isCriticalAlertNotified(String alertKey) async {
    final keys = await getNotifiedCriticalAlertKeys();
    return keys.contains(alertKey);
  }

  Future<String?> getLastSelectedFarmId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(keyLastSelectedFarmId);
  }

  Future<void> setLastSelectedFarmId(String farmId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(keyLastSelectedFarmId, farmId);
  }

  Future<void> clearAll() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(keyDailyTasksNotificationEnabled);
    await prefs.remove(keyDailyTaskNotificationTime);
    await prefs.remove(keyLastDailyNotificationDate);
    await prefs.remove(keyNotifiedCriticalAlerts);
    await prefs.remove(keyLastSelectedFarmId);
  }
}

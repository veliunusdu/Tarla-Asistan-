import 'dart:convert';

import 'package:flutter/material.dart';

import '../models/notification_target.dart';
import '../services/api_client.dart';
import 'notification_target_screen.dart';

class BildirimlerEkrani extends StatefulWidget {
  const BildirimlerEkrani({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<BildirimlerEkrani> createState() => _BildirimlerEkraniState();
}

class _BildirimlerEkraniState extends State<BildirimlerEkrani> {
  late Future<List<Map<String, dynamic>>> _notifications;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() => _notifications = _loadNotifications();

  Future<List<Map<String, dynamic>>> _loadNotifications() async {
    final response = await widget.apiClient.getJson('/notifications');
    final items = response['items'];
    if (items is! List) return const [];
    return items.whereType<Map>().map(Map<String, dynamic>.from).toList();
  }

  Future<void> _openNotification(Map<String, dynamic> notification) async {
    final id = notification['id']?.toString();
    if (id != null && id.isNotEmpty) {
      try {
        await widget.apiClient.postJson('/notifications/$id/read', {});
      } catch (_) {
        // Navigation remains available when read-state persistence is delayed.
      }
    }
    final target = NotificationTarget.fromData(_targetData(notification));
    if (!mounted) return;
    if (target == null || target.type == NotificationTargetType.unknown) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Bu bildirimin açılabilir bir hedefi yok.')),
      );
      return;
    }
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => NotificationTargetScreen(
          target: target,
          apiClient: widget.apiClient,
        ),
      ),
    );
    if (mounted) setState(_reload);
  }

  Map<String, dynamic> _targetData(Map<String, dynamic> notification) {
    final data = <String, dynamic>{
      'deep_link': notification['deepLink'] ?? notification['deep_link'],
    };
    final rawData = notification['data'];
    if (rawData is String && rawData.isNotEmpty) {
      try {
        final decoded = jsonDecode(rawData);
        if (decoded is Map) data.addAll(Map<String, dynamic>.from(decoded));
      } on FormatException {
        // The deep link remains usable when legacy data is malformed.
      }
    }
    return data;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Bildirim Merkezi')),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: _notifications,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) return _NotificationError(onRetry: () => setState(_reload));
          final notifications = snapshot.data ?? const [];
          if (notifications.isEmpty) return const Center(child: Text('Henüz bildiriminiz yok.'));
          return RefreshIndicator(
            onRefresh: () async {
              setState(_reload);
              await _notifications;
            },
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(vertical: 8),
              itemCount: notifications.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) {
                final notification = notifications[index];
                final unread = notification['readAtUtc'] == null && notification['read_at_utc'] == null;
                return ListTile(
                  leading: Icon(unread ? Icons.notifications_active_outlined : Icons.notifications_none_outlined),
                  title: Text(notification['title']?.toString() ?? 'Bildirim', style: unread ? const TextStyle(fontWeight: FontWeight.w700) : null),
                  subtitle: Text(notification['body']?.toString() ?? ''),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _openNotification(notification),
                );
              },
            ),
          );
        },
      ),
    );
  }
}

class _NotificationError extends StatelessWidget {
  const _NotificationError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.cloud_off_outlined, size: 48),
        const SizedBox(height: 12),
        const Text('Bildirimler yüklenemedi.'),
        const SizedBox(height: 12),
        FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('Tekrar dene')),
      ],
    ),
  );
}

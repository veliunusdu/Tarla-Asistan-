import 'package:flutter/material.dart';

import '../features/cases/data/backend_case_repository.dart';
import '../features/cases/data/case_repository.dart';
import '../features/cases/presentation/vaka_detay_ekrani.dart';
import '../models/notification_target.dart';
import '../services/api_client.dart';

class NotificationTargetScreen extends StatefulWidget {
  const NotificationTargetScreen({
    super.key,
    required this.target,
    required this.apiClient,
    this.caseRepository,
  });

  final NotificationTarget target;
  final ApiClient apiClient;
  final CaseRepository? caseRepository;

  @override
  State<NotificationTargetScreen> createState() =>
      _NotificationTargetScreenState();
}

class _NotificationTargetScreenState extends State<NotificationTargetScreen> {
  late Future<Map<String, dynamic>> _content;

  @override
  void initState() {
    super.initState();
    if (widget.target.type != NotificationTargetType.supportCase ||
        widget.target.resourceId.isEmpty) {
      _reload();
    }
  }

  void _reload() {
    _content = widget.apiClient.getJson(_endpoint);
  }

  String get _endpoint => switch (widget.target.type) {
    NotificationTargetType.task => '/tasks/${widget.target.resourceId}',
    NotificationTargetType.supportCase => '/cases/${widget.target.resourceId}',
    NotificationTargetType.weather => '/farms/${widget.target.farmId}/weather',
    NotificationTargetType.unknown => '/notifications',
  };

  String get _screenTitle => switch (widget.target.type) {
    NotificationTargetType.task => 'İş Detayı',
    NotificationTargetType.supportCase => 'Vaka detayı',
    NotificationTargetType.weather => 'Hava uyarısı',
    NotificationTargetType.unknown => 'Bildirim',
  };

  @override
  Widget build(BuildContext context) {
    if (widget.target.type == NotificationTargetType.supportCase &&
        widget.target.resourceId.isNotEmpty) {
      return VakaDetayEkrani(
        caseId: widget.target.resourceId,
        caseRepository: widget.caseRepository ??
            BackendCaseRepository(apiClient: widget.apiClient),
      );
    }

    return Scaffold(
      appBar: AppBar(title: Text(_screenTitle)),
      body: FutureBuilder<Map<String, dynamic>>(
        future: _content,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return Center(
              child: Semantics(
                label: 'İçerik yükleniyor',
                child: const CircularProgressIndicator(),
              ),
            );
          }
          if (snapshot.hasError) {
            return _ErrorState(
              message: snapshot.error.toString(),
              onRetry: () => setState(_reload),
            );
          }
          final data = snapshot.data ?? <String, dynamic>{};
          return RefreshIndicator(
            onRefresh: () async {
              setState(_reload);
              await _content;
            },
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: _contentWidgets(context, data),
            ),
          );
        },
      ),
    );
  }

  List<Widget> _contentWidgets(
    BuildContext context,
    Map<String, dynamic> data,
  ) {
    if (widget.target.type == NotificationTargetType.task) {
      return [
        _Header(title: data['title']?.toString() ?? 'İş'),
        _Info(label: 'Durum', value: data['status']),
        _Info(label: 'Öncelik', value: data['priority']),
        _Info(label: 'Son tarih', value: data['due_date']),
        _Section(title: 'Açıklama', body: data['description']),
        _Section(title: 'Neden', body: data['reason']),
      ];
    }
    if (widget.target.type == NotificationTargetType.supportCase) {
      final messages = data['messages'] is List
          ? data['messages'] as List
          : const [];
      return [
        _Header(title: data['title']?.toString() ?? 'Vaka'),
        _Info(label: 'Durum', value: data['status']),
        _Info(label: 'Öncelik', value: data['priority']),
        _Section(title: 'Açıklama', body: data['description']),
        Semantics(
          header: true,
          child: Text(
            'Mesajlar',
            style: Theme.of(context).textTheme.titleLarge,
          ),
        ),
        const SizedBox(height: 8),
        if (messages.isEmpty) const Text('Henüz mesaj yok.'),
        for (final message in messages.whereType<Map>())
          Card(
            child: ListTile(
              title: Text(message['sender_name']?.toString() ?? 'Kullanıcı'),
              subtitle: Text(message['body']?.toString() ?? ''),
            ),
          ),
      ];
    }
    if (widget.target.type == NotificationTargetType.weather) {
      final risks = data['risks'] is List ? data['risks'] as List : const [];
      return [
        const _Header(title: 'Güncel hava riskleri'),
        _Info(label: 'Sağlayıcı', value: data['provider']),
        _Info(
          label: 'Veri durumu',
          value: data['is_stale'] == true ? 'Güncel değil' : 'Güncel',
        ),
        if (risks.isEmpty) const Text('Aktif hava riski bulunmuyor.'),
        for (final risk in risks.whereType<Map>())
          Card(
            child: ListTile(
              leading: const Icon(Icons.warning_amber_rounded),
              title: Text(risk['message']?.toString() ?? 'Hava uyarısı'),
              subtitle: Text(risk['suggested_action']?.toString() ?? ''),
            ),
          ),
      ];
    }
    return const [Text('Bildirim hedefi bulunamadı.')];
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.title});
  final String title;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Semantics(
      header: true,
      child: Text(title, style: Theme.of(context).textTheme.headlineSmall),
    ),
  );
}

class _Info extends StatelessWidget {
  const _Info({required this.label, required this.value});
  final String label;
  final Object? value;

  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    title: Text(label),
    trailing: Text(value?.toString() ?? '—'),
  );
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.body});
  final String title;
  final Object? body;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 12, bottom: 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          header: true,
          child: Text(title, style: Theme.of(context).textTheme.titleMedium),
        ),
        const SizedBox(height: 4),
        Text(body?.toString() ?? '—'),
      ],
    ),
  );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.cloud_off_outlined, size: 48),
          const SizedBox(height: 12),
          Semantics(
            liveRegion: true,
            child: Text(message, textAlign: TextAlign.center),
          ),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('Tekrar dene'),
          ),
        ],
      ),
    ),
  );
}

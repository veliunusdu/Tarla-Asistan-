import 'package:flutter/material.dart';

import '../services/database_helper.dart';
import '../services/api_client.dart';
import '../models/tarla.dart';
import '../services/sync_service.dart';
import 'tarla_listesi_ekrani.dart';

class OzetEkrani extends StatefulWidget {
  const OzetEkrani({
    super.key,
    required this.syncService,
    required this.apiClient,
    required this.onLogout,
  });

  final SyncService syncService;
  final ApiClient apiClient;
  final Future<void> Function() onLogout;

  @override
  State<OzetEkrani> createState() => _OzetEkraniState();
}

class _OzetEkraniState extends State<OzetEkrani> {
  int _tarlaSayisi = 0;
  double _toplamDonum = 0;
  bool _loading = true;
  String? _loadWarning;

  @override
  void initState() {
    super.initState();
    _loadVeriler();
  }

  Future<void> _loadVeriler() async {
    String? warning;
    try {
      final response = await widget.apiClient.getJson(
        '/farms?limit=100&offset=0',
      );
      final items = response['items'] is List
          ? response['items'] as List
          : const [];
      final farms = items
          .whereType<Map>()
          .map((item) => Tarla.fromApi(Map<String, dynamic>.from(item)))
          .toList();
      await DatabaseHelper.instance.upsertTarlalar(farms);
    } on ApiException catch (error) {
      warning = '${error.message} Cihazdaki son tarla listesi gösteriliyor.';
    }
    final sayi = await DatabaseHelper.instance.getTarlaSayisi();
    final donum = await DatabaseHelper.instance.getToplamDonum();
    if (!mounted) return;
    setState(() {
      _tarlaSayisi = sayi;
      _toplamDonum = donum;
      _loading = false;
      _loadWarning = warning;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Tarla Asistanı'),
        actions: [
          IconButton(
            tooltip: 'Çıkış yap',
            onPressed: widget.onLogout,
            icon: const Icon(Icons.logout),
          ),
        ],
      ),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: () async {
            await _loadVeriler();
            await widget.syncService.syncNow();
          },
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Semantics(
                header: true,
                child: Text(
                  'Bugünkü tarla özeti',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'Kayıtlarınız bağlantı olmasa da cihazda saklanır ve daha sonra gönderilir.',
              ),
              const SizedBox(height: 16),
              if (_loadWarning != null) ...[
                Semantics(
                  liveRegion: true,
                  child: Card(
                    color: Theme.of(context).colorScheme.tertiaryContainer,
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Text(_loadWarning!),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
              ],
              ValueListenableBuilder<SyncState>(
                valueListenable: widget.syncService.state,
                builder: (context, state, _) => _SyncBanner(
                  state: state,
                  onRetry: widget.syncService.syncNow,
                ),
              ),
              const SizedBox(height: 16),
              if (_loading)
                const Center(child: CircularProgressIndicator())
              else
                LayoutBuilder(
                  builder: (context, constraints) {
                    final vertical = constraints.maxWidth < 360;
                    final cards = [
                      _StatCard(
                        label: 'Toplam tarla',
                        value: '$_tarlaSayisi',
                        icon: Icons.landscape_outlined,
                      ),
                      _StatCard(
                        label: 'Toplam alan',
                        value: '${_toplamDonum.toStringAsFixed(1)} dönüm',
                        icon: Icons.straighten,
                      ),
                    ];
                    if (vertical) {
                      return Column(
                        children: [
                          cards[0],
                          const SizedBox(height: 12),
                          cards[1],
                        ],
                      );
                    }
                    return Row(
                      children: [
                        Expanded(child: cards[0]),
                        const SizedBox(width: 12),
                        Expanded(child: cards[1]),
                      ],
                    );
                  },
                ),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: () async {
                  await Navigator.push<void>(
                    context,
                    MaterialPageRoute(
                      builder: (_) => TarlaListesiEkrani(
                        apiClient: widget.apiClient,
                        syncService: widget.syncService,
                      ),
                    ),
                  );
                  await _loadVeriler();
                },
                icon: const Icon(Icons.grass),
                label: const Text('Tarlalarımı görüntüle'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({
    required this.label,
    required this.value,
    required this.icon,
  });

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Semantics(
    label: '$label: $value',
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Icon(icon, size: 32),
            const SizedBox(height: 8),
            Text(value, style: Theme.of(context).textTheme.titleLarge),
            Text(label),
          ],
        ),
      ),
    ),
  );
}

class _SyncBanner extends StatelessWidget {
  const _SyncBanner({required this.state, required this.onRetry});

  final SyncState state;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    final visible = state.phase != SyncPhase.idle || state.pendingCount > 0;
    if (!visible) return const SizedBox.shrink();
    final isError = state.phase == SyncPhase.failed;
    return Semantics(
      liveRegion: true,
      child: Material(
        color: isError
            ? Theme.of(context).colorScheme.errorContainer
            : Theme.of(context).colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Icon(
                state.phase == SyncPhase.syncing
                    ? Icons.sync
                    : state.phase == SyncPhase.offline
                    ? Icons.cloud_off_outlined
                    : Icons.info_outline,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  state.message ?? '${state.pendingCount} kayıt bekliyor.',
                ),
              ),
              if (state.phase != SyncPhase.syncing)
                IconButton(
                  tooltip: 'Şimdi eşitle',
                  onPressed: onRetry,
                  icon: const Icon(Icons.refresh),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

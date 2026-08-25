import 'dart:async';

import 'package:flutter/material.dart';

import '../services/api_client.dart';
import '../services/firestore_farm_repository.dart';
import '../models/tarla.dart';
import '../services/sync_service.dart';
import 'tarla_listesi_ekrani.dart';

class OzetEkrani extends StatefulWidget {
  const OzetEkrani({
    super.key,
    required this.syncService,
    required this.apiClient,
    required this.onLogout,
    required this.repository,
  });

  final SyncService syncService;
  final ApiClient apiClient;
  final Future<void> Function() onLogout;
  final FirestoreFarmRepository repository;

  @override
  State<OzetEkrani> createState() => _OzetEkraniState();
}

class _OzetEkraniState extends State<OzetEkrani> {
  StreamSubscription<FirestoreWriteFailure>? _writeFailureSubscription;

  @override
  void initState() {
    super.initState();
    _writeFailureSubscription = widget.repository.writeFailures.listen((_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Çevrimdışı kaydedilen işlem reddedildi. Bağlantınızı ve yetkinizi kontrol edip yeniden deneyin.',
          ),
        ),
      );
    });
  }

  @override
  void dispose() {
    _writeFailureSubscription?.cancel();
    super.dispose();
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
              ValueListenableBuilder<SyncState>(
                valueListenable: widget.syncService.state,
                builder: (context, state, _) => _SyncBanner(
                  state: state,
                  onRetry: widget.syncService.syncNow,
                ),
              ),
              const SizedBox(height: 16),
              StreamBuilder<List<Tarla>>(
                stream: widget.repository.watchFarms(widget.repository.uid),
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return const Text(
                      'Tarla özeti yüklenemedi. Yetkinizi ve bağlantınızı kontrol edin.',
                    );
                  }
                  final farms = snapshot.data ?? const <Tarla>[];
                  final count = farms.length;
                  final total = farms.fold<double>(
                    0,
                    (sum, farm) => sum + (farm.size ?? 0.0),
                  );
                  return LayoutBuilder(
                    builder: (context, constraints) {
                      final vertical = constraints.maxWidth < 360;
                      final cards = [
                        _StatCard(
                          label: 'Toplam tarla',
                          value: '$count',
                          icon: Icons.landscape_outlined,
                        ),
                        _StatCard(
                          label: 'Toplam alan',
                          value: '${total.toStringAsFixed(1)} hektar',
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
                  );
                },
              ),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: () async {
                  await Navigator.push<void>(
                    context,
                    MaterialPageRoute(
                      builder: (_) => const TarlaListesiEkrani(),
                    ),
                  );
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

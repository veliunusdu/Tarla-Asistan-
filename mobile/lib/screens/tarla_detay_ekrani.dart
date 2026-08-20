import 'package:flutter/material.dart';

import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../services/firestore_farm_repository.dart';
import '../services/sync_service.dart';
import 'faaliyet_ekleme_ekrani.dart';

class TarlaDetayEkrani extends StatefulWidget {
  const TarlaDetayEkrani({
    super.key,
    required this.tarla,
    required this.syncService,
    required this.repository,
  });

  final Tarla tarla;
  final SyncService syncService;
  final FirestoreFarmRepository repository;

  @override
  State<TarlaDetayEkrani> createState() => _TarlaDetayEkraniState();
}

class _TarlaDetayEkraniState extends State<TarlaDetayEkrani> {
  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.tarla.name),
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Geçmiş', icon: Icon(Icons.history)),
              Tab(text: 'Yapılacak', icon: Icon(Icons.event_note)),
            ],
          ),
        ),
        body: StreamBuilder<List<Faaliyet>>(
          stream: widget.repository.watchActivities(widget.tarla.id),
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: const Text('Faaliyetler yüklenemedi. Yetkinizi ve bağlantınızı kontrol edin.'),
                ),
              );
            }
            final activities = snapshot.data ?? const <Faaliyet>[];
            final completed = activities
                .where((item) => item.isCompleted)
                .toList();
            final planned = activities
                .where((item) => !item.isCompleted)
                .toList();
            return TabBarView(
              children: [
                _ActivityList(
                  items: completed,
                  emptyMessage: 'Henüz kayıtlı faaliyet yok.',
                ),
                _ActivityList(
                  items: planned,
                  emptyMessage: 'Planlanan faaliyet yok.',
                ),
              ],
            );
          },
        ),
        floatingActionButton: FloatingActionButton.extended(
          tooltip: 'Yeni faaliyet ekle',
          onPressed: () async {
            await Navigator.push<bool>(
              context,
              MaterialPageRoute(
                builder: (_) => FaaliyetEklemeEkrani(
                  tarlaId: widget.tarla.id,
                  syncService: widget.syncService,
                  repository: widget.repository,
                ),
              ),
            );
          },
          icon: const Icon(Icons.add),
          label: const Text('Faaliyet ekle'),
        ),
      ),
    );
  }
}

class _ActivityList extends StatelessWidget {
  const _ActivityList({required this.items, required this.emptyMessage});

  final List<Faaliyet> items;
  final String emptyMessage;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(emptyMessage, textAlign: TextAlign.center),
        ),
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
      itemCount: items.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final activity = items[index];
        return Card(
          child: ListTile(
            leading: Icon(
              activity.isCompleted
                  ? Icons.check_circle_outline
                  : Icons.schedule,
            ),
            title: Text(activity.type),
            subtitle: Text(activity.note),
          ),
        );
      },
    );
  }
}

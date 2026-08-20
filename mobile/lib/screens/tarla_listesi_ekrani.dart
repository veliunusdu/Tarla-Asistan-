import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';

import '../models/tarla.dart';
import '../services/firestore_farm_repository.dart';
import '../services/sync_service.dart';
import 'tarla_detay_ekrani.dart';
import 'tarla_ekleme_ekrani.dart';

class TarlaListesiEkrani extends StatefulWidget {
  TarlaListesiEkrani({
    super.key,
    required this.syncService,
    FirestoreFarmRepository? repository,
  }) : repository = repository ?? FirestoreFarmRepository(uid: FirebaseAuth.instance.currentUser!.uid);

  final SyncService syncService;
  final FirestoreFarmRepository repository;

  @override
  State<TarlaListesiEkrani> createState() => _TarlaListesiEkraniState();
}

class _TarlaListesiEkraniState extends State<TarlaListesiEkrani> {
  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: Colors.green.shade50,
    appBar: AppBar(title: const Text('Tarlalarım'), backgroundColor: Colors.green.shade700),
    body: StreamBuilder<List<Tarla>>(
      stream: widget.repository.watchFarms(widget.repository.uid),
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
        if (snapshot.hasError) return const Center(child: Text('Tarlalar yüklenemedi. Yetkinizi ve bağlantınızı kontrol edin.', textAlign: TextAlign.center));
        final tarlalar = snapshot.data ?? const <Tarla>[];
        if (tarlalar.isEmpty) return const Center(child: Padding(padding: EdgeInsets.all(24), child: Text('Henüz tarla yok. Sağ alttaki ekle düğmesiyle ilk tarlanızı oluşturun.', textAlign: TextAlign.center)));
        return ListView.builder(
          padding: const EdgeInsets.all(12), itemCount: tarlalar.length,
          itemBuilder: (context, index) {
            final tarla = tarlalar[index];
            return Card(
              color: Colors.white.withValues(alpha: 0.8), elevation: 2,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              child: ListTile(
                leading: Icon(Icons.grass, color: Colors.green.shade800),
                title: Text(tarla.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                subtitle: Text('${tarla.cropType} • ${tarla.size.toStringAsFixed(1)} hektar'),
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => TarlaDetayEkrani(tarla: tarla, syncService: widget.syncService, repository: widget.repository))),
              ),
            );
          },
        );
      },
    ),
    floatingActionButton: FloatingActionButton(
      tooltip: 'Yeni tarla ekle', backgroundColor: Colors.green.shade700, child: const Icon(Icons.add),
      onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => TarlaEklemeEkrani(repository: widget.repository))),
    ),
  );
}

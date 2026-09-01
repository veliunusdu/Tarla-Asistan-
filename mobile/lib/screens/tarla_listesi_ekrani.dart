import 'package:flutter/material.dart';

import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../models/tarla.dart';
import '../shared/widgets/app_empty_view.dart';
import '../shared/widgets/app_error_view.dart';
import '../shared/widgets/app_loading_view.dart';
import 'tarla_detay_ekrani.dart';
import 'tarla_ekleme_ekrani.dart';

class TarlaListesiEkrani extends StatefulWidget {
  const TarlaListesiEkrani({
    super.key,
    TarlaRepository? repository,
    FaaliyetRepository? faaliyetRepository,
    this.onDataChanged,
  }) : _repository = repository ?? const LocalTarlaRepository(),
       _faaliyetRepository =
           faaliyetRepository ?? const LocalFaaliyetRepository();

  final TarlaRepository _repository;
  final FaaliyetRepository _faaliyetRepository;

  @visibleForTesting
  FaaliyetRepository get faaliyetRepositoryForTesting => _faaliyetRepository;

  /// Tarla başarıyla eklendiğinde çağrılır (örn. Ana Sayfa'yı uyarmak için).
  final VoidCallback? onDataChanged;

  @override
  State<TarlaListesiEkrani> createState() => _TarlaListesiEkraniState();
}

class _TarlaListesiEkraniState extends State<TarlaListesiEkrani> {
  late Future<List<Tarla>> _tarlalar;

  @override
  void initState() {
    super.initState();
    _tarlalar = widget._repository.getTarlalar();
  }

  void _yenile() {
    setState(() {
      _tarlalar = widget._repository.getTarlalar();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.green.shade50,
      appBar: AppBar(
        title: const Text('Tarlalarım'),
        backgroundColor: Colors.green.shade700,
      ),
      body: FutureBuilder<List<Tarla>>(
        future: _tarlalar,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const AppLoadingView(message: 'Tarlalar yükleniyor…');
          }

          if (snapshot.hasError) {
            return AppErrorView(onRetry: _yenile);
          }

          final tarlalar = snapshot.data ?? [];

          if (tarlalar.isEmpty) {
            return AppEmptyView(
              icon: Icons.grass,
              title: 'Henüz tarla eklemediniz',
              description: 'İlk tarlanızı ekleyerek başlayın.',
              actionLabel: 'Tarla Ekle',
              onAction: () async {
                final result = await Navigator.push<bool>(
                  context,
                  MaterialPageRoute(
                    builder: (context) =>
                        TarlaEklemeEkrani(repository: widget._repository),
                  ),
                );
                if (result == true) {
                  _yenile();
                  widget.onDataChanged?.call();
                }
              },
            );
          }

          return ListView.builder(
            padding: const EdgeInsets.all(12),
            itemCount: tarlalar.length,
            itemBuilder: (context, index) {
              final tarla = tarlalar[index];
              return Card(
                color: Colors.white.withValues(alpha: 0.8),
                elevation: 2,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
                child: ListTile(
                  leading: Icon(Icons.grass, color: Colors.green.shade800),
                  title: Text(
                    tarla.name,
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  subtitle: Text(
                    '${tarla.cropType ?? 'Ürün bilgisi yok'} • ${tarla.size != null ? '${tarla.size} dönüm' : 'Alan bilinmiyor'}',
                  ),
                  onTap: () async {
                    await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (context) => TarlaDetayEkrani(
                          tarla: tarla,
                          faaliyetRepository: widget._faaliyetRepository,
                          onEdit: widget._repository is TarlaUpdateRepository
                              ? () => Navigator.push<bool>(
                                  context,
                                  MaterialPageRoute(
                                    builder: (context) => TarlaEklemeEkrani(
                                      repository: widget._repository,
                                      editingTarla: tarla,
                                    ),
                                  ),
                                ).then((result) => result ?? false)
                              : null,
                          onArchive:
                              widget._repository is TarlaArchiveRepository
                              ? () =>
                                    (widget._repository
                                            as TarlaArchiveRepository)
                                        .archiveTarla(tarla.id)
                              : null,
                        ),
                      ),
                    );
                    _yenile();
                  },
                ),
              );
            },
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: Colors.green.shade700,
        onPressed: () async {
          final result = await Navigator.push<bool>(
            context,
            MaterialPageRoute(
              builder: (context) =>
                  TarlaEklemeEkrani(repository: widget._repository),
            ),
          );
          if (result == true) {
            _yenile();
            widget.onDataChanged?.call();
          }
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}

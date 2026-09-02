import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../features/fields/data/farm_summary_repository.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../shared/utils/date_formatter.dart';
import '../shared/widgets/app_empty_view.dart';
import '../shared/widgets/app_error_view.dart';
import '../shared/widgets/app_loading_view.dart';
import 'tarla_detay_ekrani.dart';
import 'tarla_ekleme_ekrani.dart';

String _formatTarih(DateTime dt, {bool isPlanlanan = false}) {
  final now = DateTime.now();
  final bugun = DateTime(now.year, now.month, now.day);
  final hedef = DateTime(dt.year, dt.month, dt.day);
  final fark = hedef.difference(bugun).inDays;

  if (isPlanlanan && fark < 0) {
    return 'Gecikti';
  }

  if (fark == 0) return 'Bugün';
  if (fark == 1) return 'Yarın';
  if (fark == -1) return 'Dün';

  if (dt.year != now.year) {
    return '${dt.day} ${trAylar[dt.month - 1]} ${dt.year}';
  }
  return '${dt.day} ${trAylar[dt.month - 1]}';
}

Faaliyet? _bulSiradakiIs(List<Faaliyet> faaliyetler) {
  final acikIsler = faaliyetler
      .where((f) => !f.isCompleted && f.dueDate != null)
      .toList();
  if (acikIsler.isEmpty) return null;
  acikIsler.sort((a, b) => a.dueDate!.compareTo(b.dueDate!));
  return acikIsler.first;
}

Faaliyet? _bulSonIs(List<Faaliyet> faaliyetler) {
  final tamamlananlar = faaliyetler
      .where((f) => f.isCompleted)
      .toList();
  if (tamamlananlar.isEmpty) return null;
  tamamlananlar.sort((a, b) => b.timestamp.compareTo(a.timestamp));
  return tamamlananlar.first;
}

class _TarlaListesiVerisi {
  const _TarlaListesiVerisi({
    required this.tarlalar,
    required this.faaliyetlerByTarla,
  });

  final List<Tarla> tarlalar;
  final Map<String, List<Faaliyet>> faaliyetlerByTarla;
}

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
  late Future<_TarlaListesiVerisi> _veri;

  @override
  void initState() {
    super.initState();
    _veri = _yukle();
  }

  Future<_TarlaListesiVerisi> _yukle() async {
    final summaryRepo = widget._repository is FarmSummaryRepository
        ? widget._repository as FarmSummaryRepository
        : null;

    if (summaryRepo != null) {
      final summary = await summaryRepo.getFarmSummary();
      final tarlalar = summary.farms.map((f) => f.tarla).toList();
      final byTarla = <String, List<Faaliyet>>{};
      for (final f in summary.farms) {
        final list = <Faaliyet>[];
        if (f.nextTask != null) list.add(f.nextTask!);
        if (f.lastActivity != null) list.add(f.lastActivity!);
        byTarla[f.tarla.id] = list;
      }
      return _TarlaListesiVerisi(
        tarlalar: tarlalar,
        faaliyetlerByTarla: byTarla,
      );
    }

    final tarlalar = await widget._repository.getTarlalar();
    final repo = widget._faaliyetRepository;

    List<Faaliyet> faaliyetler = [];
    try {
      faaliyetler = await repo.getTumFaaliyetler();
    } catch (_) {
      // Degrade graceful: faaliyet verisi alınamazsa tarlalar yine gösterilir
    }

    List<Faaliyet> planliGorevler = [];
    if (repo is PlanliGorevRepository) {
      try {
        planliGorevler =
            await (repo as PlanliGorevRepository).getPlanliGorevler();
      } catch (_) {
        // Degrade graceful: planlı görev verisi alınamazsa tarlalar yine gösterilir
      }
    }

    final islerMap = <String, Faaliyet>{};
    for (final f in faaliyetler) {
      islerMap[f.id] = f;
    }
    for (final f in planliGorevler) {
      islerMap[f.id] = f;
    }

    final byTarla = <String, List<Faaliyet>>{};
    for (final f in islerMap.values) {
      byTarla.putIfAbsent(f.tarlaId, () => []).add(f);
    }

    return _TarlaListesiVerisi(
      tarlalar: tarlalar,
      faaliyetlerByTarla: byTarla,
    );
  }

  void _yenile() {
    setState(() {
      _veri = _yukle();
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
      body: FutureBuilder<_TarlaListesiVerisi>(
        future: _veri,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const AppLoadingView(message: 'Tarlalar yükleniyor…');
          }

          if (snapshot.hasError) {
            return AppErrorView(onRetry: _yenile);
          }

          final veri = snapshot.data;
          final tarlalar = veri?.tarlalar ?? [];
          final faaliyetlerByTarla = veri?.faaliyetlerByTarla ?? {};

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
              final tarlaFaaliyetleri =
                  faaliyetlerByTarla[tarla.id] ?? const [];
              final siradaki = _bulSiradakiIs(tarlaFaaliyetleri);
              final son = _bulSonIs(tarlaFaaliyetleri);

              return Card(
                color: Colors.white.withValues(alpha: 0.8),
                elevation: 2,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 8,
                  ),
                  leading: Icon(Icons.grass, color: Colors.green.shade800),
                  title: Text(
                    tarla.name,
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${tarla.cropType ?? 'Ürün bilgisi yok'} • ${tarla.size != null ? '${tarla.size} dönüm' : 'Alan bilinmiyor'}',
                          style: TextStyle(
                            color: Colors.grey.shade700,
                            fontSize: 13,
                          ),
                        ),
                        const SizedBox(height: 6),
                        if (siradaki != null || son != null) ...[
                          if (siradaki != null)
                            Padding(
                              padding: const EdgeInsets.only(bottom: 2),
                              child: Row(
                                children: [
                                  const Icon(
                                    Icons.schedule,
                                    size: 14,
                                    color: AppColors.warning,
                                  ),
                                  const SizedBox(width: 4),
                                  Expanded(
                                    child: Text(
                                      'Sıradaki: ${siradaki.type} • ${_formatTarih(siradaki.dueDate!, isPlanlanan: true)}',
                                      style: const TextStyle(
                                        color: AppColors.textPrimary,
                                        fontSize: 12,
                                        fontWeight: FontWeight.w500,
                                      ),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          if (son != null)
                            Row(
                              children: [
                                const Icon(
                                  Icons.check_circle_outline,
                                  size: 14,
                                  color: AppColors.primary,
                                ),
                                const SizedBox(width: 4),
                                Expanded(
                                  child: Text(
                                    'Son: ${son.type} • ${_formatTarih(son.timestamp)}',
                                    style: const TextStyle(
                                      color: AppColors.textSecondary,
                                      fontSize: 12,
                                    ),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ],
                            ),
                        ] else
                          const Text(
                            'Henüz iş kaydı yok',
                            style: TextStyle(
                              color: AppColors.textDisabled,
                              fontSize: 12,
                              fontStyle: FontStyle.italic,
                            ),
                          ),
                      ],
                    ),
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

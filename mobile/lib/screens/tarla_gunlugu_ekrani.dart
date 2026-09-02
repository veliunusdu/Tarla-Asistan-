import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../shared/utils/date_formatter.dart';
import '../shared/widgets/app_empty_view.dart';
import '../shared/widgets/app_error_view.dart';
import '../shared/widgets/app_loading_view.dart';
import '../shared/widgets/tarla_secim_bottom_sheet.dart';
import 'faaliyet_ekleme_ekrani.dart';
import 'tarla_detay_ekrani.dart';
import 'tarla_ekleme_ekrani.dart';

// ---------------------------------------------------------------------------
// Tarih yardımcıları
// ---------------------------------------------------------------------------

DateTime _todayDate() {
  final now = DateTime.now();
  return DateTime(now.year, now.month, now.day);
}

bool _bugunMu(DateTime dt) {
  final today = _todayDate();
  return dt.year == today.year &&
      dt.month == today.month &&
      dt.day == today.day;
}

// ---------------------------------------------------------------------------
// Filtre
// ---------------------------------------------------------------------------

enum _Filtre { bugun, yaklasan, geciken, tumu }

// ---------------------------------------------------------------------------
// Günlük kayıt — faaliyet + eşleşen tarla adı
// ---------------------------------------------------------------------------

class _GunlukKayit {
  const _GunlukKayit({
    required this.faaliyet,
    required this.tarlaAdi,
    this.tarla,
  });

  final Faaliyet faaliyet;
  final String tarlaAdi;
  final Tarla? tarla;

  DateTime get referansTarih => faaliyet.isCompleted
      ? faaliyet.timestamp
      : (faaliyet.dueDate ?? faaliyet.timestamp);
}

// ---------------------------------------------------------------------------
// Ekran
// ---------------------------------------------------------------------------

class TarlaGunluguEkrani extends StatefulWidget {
  const TarlaGunluguEkrani({
    super.key,
    TarlaRepository? tarlaRepository,
    FaaliyetRepository? faaliyetRepository,
    this.onDataChanged,
  }) : _tarlaRepo = tarlaRepository ?? const LocalTarlaRepository(),
       _faaliyetRepo = faaliyetRepository ?? const LocalFaaliyetRepository();

  final TarlaRepository _tarlaRepo;
  final FaaliyetRepository _faaliyetRepo;

  @visibleForTesting
  FaaliyetRepository get faaliyetRepositoryForTesting => _faaliyetRepo;

  /// Görev başarıyla eklendiğinde çağrılır (örn. Ana Sayfa'yı uyarmak için).
  final VoidCallback? onDataChanged;

  @override
  State<TarlaGunluguEkrani> createState() => _TarlaGunluguEkraniState();
}

class _TarlaGunluguEkraniState extends State<TarlaGunluguEkrani> {
  // ── Veri ──────────────────────────────────────────────────────────────────
  late Future<(List<Tarla>, List<_GunlukKayit>)> _veri;
  List<Tarla> _tarlalarCache = [];

  // ── Tamamlama durumu ──────────────────────────────────────────────────────
  String? _tamamlananGorevId;

  // ── Filtre ────────────────────────────────────────────────────────────────
  _Filtre _filtre = _Filtre.bugun;

  // ── Scroll ────────────────────────────────────────────────────────────────
  final _scrollCtrl = ScrollController();

  // ---------------------------------------------------------------------------

  @override
  void initState() {
    super.initState();
    _veri = _yukle();
  }

  @override
  void dispose() {
    _scrollCtrl.dispose();
    super.dispose();
  }

  Future<(List<Tarla>, List<_GunlukKayit>)> _yukle() async {
    final repository = widget._faaliyetRepo;
    final tarlalarFuture = widget._tarlaRepo.getTarlalar();
    final planliGorevlerFuture = repository is PlanliGorevRepository
        ? (repository as PlanliGorevRepository).getPlanliGorevler()
        : Future.value(<Faaliyet>[]);

    final tarlalar = await tarlalarFuture;
    _tarlalarCache = tarlalar;

    // İş Planım yalnızca açık planlı görevleri gösterir. Tamamlanan işler
    // backend tarafından faaliyet geçmişine taşınır ve burada tekrar edilmez.
    final faaliyetler = await planliGorevlerFuture;

    final tarlaMap = {for (final t in tarlalar) t.id: t};

    final kayitlar = faaliyetler.map((f) {
      final tarla = tarlaMap[f.tarlaId];
      return _GunlukKayit(
        faaliyet: f,
        tarlaAdi: tarla?.name ?? 'Bilinmeyen tarla',
        tarla: tarla,
      );
    }).toList();

    return (tarlalar, kayitlar);
  }

  void _yenile() {
    setState(() {
      _veri = _yukle();
    });
  }

  // ---------------------------------------------------------------------------
  // Filtreleme + sıralama
  // ---------------------------------------------------------------------------

  List<_GunlukKayit> _filtrele(List<_GunlukKayit> kayitlar) {
    switch (_filtre) {
      case _Filtre.bugun:
        return kayitlar.where((k) {
            if (k.faaliyet.isCompleted) return false;
            final due = k.faaliyet.dueDate;
            if (due == null) return false;
            return _bugunMu(due);
          }).toList()
          ..sort((a, b) => a.faaliyet.dueDate!.compareTo(b.faaliyet.dueDate!));

      case _Filtre.yaklasan:
        return kayitlar.where((k) {
            final due = k.faaliyet.dueDate;
            return due != null && due.isAfter(_todayDate());
          }).toList()
          ..sort((a, b) {
            final da = a.faaliyet.dueDate ?? a.faaliyet.timestamp;
            final db = b.faaliyet.dueDate ?? b.faaliyet.timestamp;
            return da.compareTo(db);
          });

      case _Filtre.geciken:
        return kayitlar.where((k) {
            final due = k.faaliyet.dueDate;
            return due != null && due.isBefore(_todayDate());
          }).toList()
          ..sort((a, b) => a.faaliyet.dueDate!.compareTo(b.faaliyet.dueDate!));

      case _Filtre.tumu:
        return List<_GunlukKayit>.from(kayitlar)
          ..sort((a, b) => a.referansTarih.compareTo(b.referansTarih));
    }
  }

  // ---------------------------------------------------------------------------
  // Yeni İş Ekleme Akışı
  // ---------------------------------------------------------------------------

  Future<void> _isEkle(List<Tarla> tarlalar) async {
    if (tarlalar.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text(
            'İş eklemek için önce en az bir tarla eklemelisiniz.',
          ),
          action: SnackBarAction(
            label: 'Tarla Ekle',
            onPressed: () async {
              final result = await Navigator.push<bool>(
                context,
                MaterialPageRoute(
                  builder: (_) =>
                      TarlaEklemeEkrani(repository: widget._tarlaRepo),
                ),
              );
              if (result == true && mounted) _yenile();
            },
          ),
        ),
      );
      return;
    }

    Tarla? secilen;
    if (tarlalar.length == 1) {
      secilen = tarlalar.first;
    } else {
      secilen = await TarlaSecimBottomSheet.show(
        context,
        tarlalar: tarlalar,
      );
    }

    if (secilen == null || !mounted) return;

    final result = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => FaaliyetEklemeEkrani(
          tarlaId: secilen!.id,
          faaliyetRepository: widget._faaliyetRepo,
          initialIsCompleted: false,
          initialSelectedDate: _todayDate(),
        ),
      ),
    );

    if (result == true && mounted) {
      _yenile();
      widget.onDataChanged?.call();
    }
  }

  // ---------------------------------------------------------------------------
  // Görev tamamlama
  // ---------------------------------------------------------------------------

  Future<void> _goreviTamamla(Faaliyet gorev) async {
    final repository = widget._faaliyetRepo;
    if (repository is! PlanliGorevCompletionRepository) return;

    setState(() => _tamamlananGorevId = gorev.id);
    try {
      await (repository as PlanliGorevCompletionRepository).completePlanliGorev(
        gorev.id,
        note: gorev.note,
      );
      if (!mounted) return;
      _yenile();
      widget.onDataChanged?.call();
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('İş tamamlandı.')));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('İş tamamlanamadı. Lütfen tekrar deneyin.'),
        ),
      );
    } finally {
      if (mounted) setState(() => _tamamlananGorevId = null);
    }
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('İş Planım')),
      body: FutureBuilder<(List<Tarla>, List<_GunlukKayit>)>(
        future: _veri,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const AppLoadingView(message: 'Kayıtlar yükleniyor…');
          }
          if (snapshot.hasError) {
            return AppErrorView(onRetry: _yenile);
          }

          final (tarlalar, tumKayitlar) = snapshot.data!;
          final gosterilen = _filtrele(tumKayitlar);

          return ListView(
            controller: _scrollCtrl,
            padding: const EdgeInsets.all(AppSpacing.md),
            children: [
              _aciklama(context),
              const SizedBox(height: AppSpacing.sm),

              // Tarla yok durumu
              if (tarlalar.isEmpty) ...[
                _TarlaYokDurumu(
                  onTarlaEkle: () async {
                    final result = await Navigator.push<bool>(
                      context,
                      MaterialPageRoute(
                        builder: (_) =>
                            TarlaEklemeEkrani(repository: widget._tarlaRepo),
                      ),
                    );
                    if (result == true && mounted) _yenile();
                  },
                ),
                const SizedBox(height: AppSpacing.md),
              ],

              // Filtre çubuğu
              _FiltreCubugu(
                secili: _filtre,
                onSecildi: (f) => setState(() => _filtre = f),
              ),
              const SizedBox(height: AppSpacing.md),

              // Bölüm başlığı
              Text(
                _listeBasligi(),
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.sm),

              // Kayıtlar
              if (gosterilen.isEmpty)
                _bosState()
              else
                ...gosterilen.map(
                  (k) => _GunlukKayitKarti(
                    kayit: k,
                    completing: _tamamlananGorevId == k.faaliyet.id,
                    onComplete:
                        !k.faaliyet.isCompleted &&
                            widget._faaliyetRepo
                                is PlanliGorevCompletionRepository
                        ? () => _goreviTamamla(k.faaliyet)
                        : null,
                    onTap: k.tarla != null
                        ? () => Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (_) => TarlaDetayEkrani(
                                tarla: k.tarla!,
                                faaliyetRepository: widget._faaliyetRepo,
                              ),
                            ),
                          )
                        : null,
                  ),
                ),

              const SizedBox(height: 80),
            ],
          );
        },
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _isEkle(_tarlalarCache),
        icon: const Icon(Icons.add),
        label: const Text('İş Ekle'),
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
      ),
    );
  }

  // ---------------------------------------------------------------------------
  // Yardımcı widget üretici metotlar
  // ---------------------------------------------------------------------------

  Widget _aciklama(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
      child: Text(
        'Planladığın işleri buradan takip edebilirsin.',
        style: theme.textTheme.bodyMedium?.copyWith(
          color: AppColors.textSecondary,
        ),
      ),
    );
  }

  String _listeBasligi() {
    return switch (_filtre) {
      _Filtre.bugun => 'Bugünün İşleri',
      _Filtre.yaklasan => 'Yaklaşan İşler',
      _Filtre.geciken => 'Geciken İşler',
      _Filtre.tumu => 'Tüm Planlı İşler',
    };
  }

  Widget _bosState() {
    return switch (_filtre) {
      _Filtre.bugun => const AppEmptyView(
        icon: Icons.today,
        title: 'Bugün için planlanmış iş bulunmuyor.',
        description: 'Yeni bir iş planlayarak günlük planını oluşturabilirsin.',
      ),
      _Filtre.yaklasan => const AppEmptyView(
        icon: Icons.event_available,
        title: 'Yaklaşan iş bulunmuyor.',
        description: 'Yeni bir iş ekleyerek planlama yapabilirsin.',
      ),
      _Filtre.geciken => const AppEmptyView(
        icon: Icons.event_busy_outlined,
        title: 'Geciken iş bulunmuyor.',
        description: 'Planındaki işlerin tamamı güncel.',
      ),
      _Filtre.tumu => const AppEmptyView(
        icon: Icons.event_note_outlined,
        title: 'Henüz planlı iş bulunmuyor.',
        description: 'İş planlayarak başlayabilirsin.',
      ),
    };
  }
}

// ---------------------------------------------------------------------------
// "Tarla yok" durumu
// ---------------------------------------------------------------------------

class _TarlaYokDurumu extends StatelessWidget {
  const _TarlaYokDurumu({required this.onTarlaEkle});

  final VoidCallback onTarlaEkle;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.terrain, color: AppColors.textDisabled, size: 48),
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'İş eklemek için önce bir tarla oluşturmalısın.',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppColors.textSecondary),
            ),
            const SizedBox(height: AppSpacing.md),
            OutlinedButton.icon(
              onPressed: onTarlaEkle,
              icon: const Icon(Icons.add),
              label: const Text('Tarla Ekle'),
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Filtre çubuğu — yatay kaydırılabilir FilterChip'ler
// ---------------------------------------------------------------------------

class _FiltreCubugu extends StatelessWidget {
  const _FiltreCubugu({required this.secili, required this.onSecildi});

  final _Filtre secili;
  final ValueChanged<_Filtre> onSecildi;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          _chip(_Filtre.bugun, 'Bugün'),
          const SizedBox(width: AppSpacing.sm),
          _chip(_Filtre.yaklasan, 'Yaklaşan'),
          const SizedBox(width: AppSpacing.sm),
          _chip(_Filtre.geciken, 'Geciken'),
          const SizedBox(width: AppSpacing.sm),
          _chip(_Filtre.tumu, 'Tümü'),
        ],
      ),
    );
  }

  Widget _chip(_Filtre value, String label) {
    return FilterChip(
      label: Text(label),
      selected: secili == value,
      onSelected: (_) => onSecildi(value),
    );
  }
}

// ---------------------------------------------------------------------------
// Günlük kayıt kartı
// ---------------------------------------------------------------------------

class _GunlukKayitKarti extends StatelessWidget {
  const _GunlukKayitKarti({
    required this.kayit,
    required this.completing,
    this.onTap,
    this.onComplete,
  });

  final _GunlukKayit kayit;
  final bool completing;
  final VoidCallback? onTap;
  final VoidCallback? onComplete;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final f = kayit.faaliyet;
    final tamamlandi = f.isCompleted;
    final ikonRenk = tamamlandi ? AppColors.success : AppColors.warning;

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Durum ikonu — sabit boyut
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: ikonRenk.withValues(alpha: 0.15),
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  tamamlandi ? Icons.check_circle : Icons.schedule,
                  color: ikonRenk,
                  size: 20,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),

              // İçerik — Expanded ile overflow önlenir
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Tarla adı + rozet aynı satırda
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(
                            kayit.tarlaAdi,
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: AppColors.textSecondary,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        _DurumRozeti(tamamlandi: tamamlandi),
                      ],
                    ),
                    const SizedBox(height: 2),
                    // İş türü / başlık
                    Text(
                      f.type,
                      style: theme.textTheme.titleMedium,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    // Not
                    if (f.note.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        f.note,
                        style: theme.textTheme.bodyMedium,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                    const SizedBox(height: AppSpacing.xs),
                    // Tarih
                    Text(
                      formatTarih(kayit.referansTarih),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: tamamlandi ? AppColors.textSecondary : ikonRenk,
                      ),
                    ),
                    if (onComplete != null) ...[
                      const SizedBox(height: AppSpacing.xs),
                      Align(
                        alignment: Alignment.centerRight,
                        child: TextButton.icon(
                          onPressed: completing ? null : onComplete,
                          icon: completing
                              ? const SizedBox.square(
                                  dimension: 16,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Icon(Icons.check, size: 18),
                          label: const Text('Tamamla'),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Durum rozeti
// ---------------------------------------------------------------------------

class _DurumRozeti extends StatelessWidget {
  const _DurumRozeti({required this.tamamlandi});

  final bool tamamlandi;

  @override
  Widget build(BuildContext context) {
    final renk = tamamlandi ? AppColors.success : AppColors.warning;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: 3,
      ),
      decoration: BoxDecoration(
        color: renk.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        tamamlandi ? 'Tamamlandı' : 'Planlandı',
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: renk,
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../shared/widgets/app_empty_view.dart';
import '../shared/widgets/app_error_view.dart';
import '../shared/widgets/app_loading_view.dart';
import 'tarla_detay_ekrani.dart';
import 'tarla_ekleme_ekrani.dart';

// ---------------------------------------------------------------------------
// Tarih yardımcıları
// ---------------------------------------------------------------------------

const List<String> _trAylar = [
  'Oca',
  'Şub',
  'Mar',
  'Nis',
  'May',
  'Haz',
  'Tem',
  'Ağu',
  'Eyl',
  'Eki',
  'Kas',
  'Ara',
];
String _tarihStr(DateTime dt) =>
    '${dt.day} ${_trAylar[dt.month - 1]} ${dt.year}';

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

enum _Filtre { bugun, planlanan, tamamlanan, tumu }

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

  // ── Form ──────────────────────────────────────────────────────────────────
  final _formKey = GlobalKey<FormState>();
  final _gorevCtrl = TextEditingController();
  final _notCtrl = TextEditingController();
  final _gorevFocus = FocusNode();
  Tarla? _seciliTarla;
  late DateTime _planliTarih;
  bool _kaydediliyor = false;
  String? _tamamlananGorevId;

  // ── Filtre ────────────────────────────────────────────────────────────────
  _Filtre _filtre = _Filtre.bugun;

  // ── Scroll ────────────────────────────────────────────────────────────────
  final _scrollCtrl = ScrollController();

  // ---------------------------------------------------------------------------

  @override
  void initState() {
    super.initState();
    _planliTarih = _todayDate();
    _veri = _yukle();
  }

  @override
  void dispose() {
    _gorevCtrl.dispose();
    _notCtrl.dispose();
    _gorevFocus.dispose();
    _scrollCtrl.dispose();
    super.dispose();
  }

  Future<(List<Tarla>, List<_GunlukKayit>)> _yukle() async {
    final repository = widget._faaliyetRepo;
    final tarlalarFuture = widget._tarlaRepo.getTarlalar();
    final faaliyetlerFuture = repository.getTumFaaliyetler();
    final planliGorevlerFuture = repository is PlanliGorevRepository
        ? (repository as PlanliGorevRepository).getPlanliGorevler()
        : Future.value(<Faaliyet>[]);

    final tarlalar = await tarlalarFuture;
    final faaliyetler = [
      ...await faaliyetlerFuture,
      ...await planliGorevlerFuture,
    ];

    // Seçili tarla silinmişse sıfırla
    if (_seciliTarla != null &&
        !tarlalar.any((t) => t.id == _seciliTarla!.id)) {
      _seciliTarla = null;
    }

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

      case _Filtre.planlanan:
        return kayitlar.where((k) => !k.faaliyet.isCompleted).toList()
          ..sort((a, b) {
            final da = a.faaliyet.dueDate ?? a.faaliyet.timestamp;
            final db = b.faaliyet.dueDate ?? b.faaliyet.timestamp;
            return da.compareTo(db);
          });

      case _Filtre.tamamlanan:
        return kayitlar.where((k) => k.faaliyet.isCompleted).toList()..sort(
          (a, b) => b.faaliyet.timestamp.compareTo(a.faaliyet.timestamp),
        );

      case _Filtre.tumu:
        return List<_GunlukKayit>.from(kayitlar)
          ..sort((a, b) => b.referansTarih.compareTo(a.referansTarih));
    }
  }

  // ---------------------------------------------------------------------------
  // Kaydetme
  // ---------------------------------------------------------------------------

  Future<void> _kaydet() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() => _kaydediliyor = true);

    try {
      final faaliyet = Faaliyet(
        id: DateTime.now().millisecondsSinceEpoch.toString(),
        tarlaId: _seciliTarla!.id,
        type: _gorevCtrl.text.trim(),
        note: _notCtrl.text.trim(),
        timestamp: DateTime.now(),
        isCompleted: false,
        dueDate: _planliTarih,
      );

      final repository = widget._faaliyetRepo;
      if (repository is PlanliGorevRepository) {
        await (repository as PlanliGorevRepository).addPlanliGorev(faaliyet);
      } else {
        await repository.addFaaliyet(faaliyet);
      }

      if (!mounted) return;

      _gorevCtrl.clear();
      _notCtrl.clear();
      setState(() => _planliTarih = _todayDate());

      _yenile();
      widget.onDataChanged?.call();

      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Görev eklendi.')));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Görev eklenirken bir hata oluştu. Lütfen tekrar deneyin.',
          ),
        ),
      );
    } finally {
      if (mounted) setState(() => _kaydediliyor = false);
    }
  }

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
      ).showSnackBar(const SnackBar(content: Text('Görev tamamlandı.')));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Görev tamamlanamadı. Lütfen tekrar deneyin.'),
        ),
      );
    } finally {
      if (mounted) setState(() => _tamamlananGorevId = null);
    }
  }

  // ---------------------------------------------------------------------------
  // Tarih seçici
  // ---------------------------------------------------------------------------

  Future<void> _tarihSec() async {
    final today = _todayDate();
    final init = _planliTarih.isBefore(today) ? today : _planliTarih;
    final picked = await showDatePicker(
      context: context,
      initialDate: init,
      firstDate: today,
      lastDate: DateTime(2100),
    );
    if (picked != null && mounted) {
      setState(() => _planliTarih = picked);
    }
  }

  // ---------------------------------------------------------------------------
  // Form alanına kaydır
  // ---------------------------------------------------------------------------

  void _scrollToForm() {
    _scrollCtrl.animateTo(
      0,
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeOut,
    );
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Günlüğüm')),
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
              const SizedBox(height: AppSpacing.md),

              // Form veya "tarla yok" durumu
              if (tarlalar.isEmpty)
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
                )
              else
                _YeniGorevKarti(
                  formKey: _formKey,
                  tarlalar: tarlalar,
                  seciliTarla: _seciliTarla,
                  onTarlaSecildi: (t) => setState(() => _seciliTarla = t),
                  gorevCtrl: _gorevCtrl,
                  gorevFocus: _gorevFocus,
                  notCtrl: _notCtrl,
                  planliTarih: _planliTarih,
                  onTarihSec: _tarihSec,
                  kaydediliyor: _kaydediliyor,
                  onKaydet: _kaydet,
                ),

              const SizedBox(height: AppSpacing.md),

              // Filtre çubuğu
              _FiltreCubugu(
                secili: _filtre,
                onSecildi: (f) => setState(() => _filtre = f),
              ),
              const SizedBox(height: AppSpacing.sm),

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

              const SizedBox(height: AppSpacing.xl),
            ],
          );
        },
      ),
    );
  }

  // ---------------------------------------------------------------------------
  // Yardımcı widget üretici metotlar
  // ---------------------------------------------------------------------------

  Widget _aciklama(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Günlük işlerini planla ve faaliyet geçmişini takip et.',
          style: theme.textTheme.bodyMedium,
        ),
        const SizedBox(height: AppSpacing.sm),
        Card(
          elevation: 0,
          color: theme.colorScheme.secondaryContainer.withValues(alpha: 0.6),
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.sm,
              vertical: AppSpacing.xs,
            ),
            child: Row(
              children: [
                Icon(
                  Icons.info_outline,
                  size: 16,
                  color: theme.colorScheme.onSecondaryContainer,
                ),
                const SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    'Planlı görevler ve tamamlanan faaliyetler hesabınızla eşitlenir.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSecondaryContainer,
                    ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  String _listeBasligi() {
    return switch (_filtre) {
      _Filtre.bugun => 'Bugünün Görevleri',
      _Filtre.planlanan => 'Planlanan Görevler',
      _Filtre.tamamlanan => 'Tamamlanan Görevler',
      _Filtre.tumu => 'Tüm Faaliyetler',
    };
  }

  Widget _bosState() {
    return switch (_filtre) {
      _Filtre.bugun => AppEmptyView(
        icon: Icons.today,
        title: 'Bugün için görev bulunmuyor.',
        description:
            'Yeni bir görev ekleyerek günlük planını oluşturabilirsin.',
        actionLabel: 'Görev Ekle',
        onAction: _scrollToForm,
      ),
      _Filtre.planlanan => const AppEmptyView(
        icon: Icons.event_available,
        title: 'Planlanmış görev bulunmuyor.',
        description: 'Yeni bir görev ekleyerek planlama yapabilirsiniz.',
      ),
      _Filtre.tamamlanan => const AppEmptyView(
        icon: Icons.check_circle_outline,
        title: 'Henüz tamamlanmış faaliyet bulunmuyor.',
        description: 'Tamamladığınız görevler burada görünecek.',
      ),
      _Filtre.tumu => const AppEmptyView(
        icon: Icons.book_outlined,
        title: 'Henüz faaliyet kaydı bulunmuyor.',
        description: 'Görev ekleyerek başlayabilirsiniz.',
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
            Text(
              'Görev eklemek için önce bir tarla oluşturmalısın.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textSecondary),
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
// Yeni görev formu
// ---------------------------------------------------------------------------

class _YeniGorevKarti extends StatelessWidget {
  const _YeniGorevKarti({
    required this.formKey,
    required this.tarlalar,
    required this.seciliTarla,
    required this.onTarlaSecildi,
    required this.gorevCtrl,
    required this.gorevFocus,
    required this.notCtrl,
    required this.planliTarih,
    required this.onTarihSec,
    required this.kaydediliyor,
    required this.onKaydet,
  });

  final GlobalKey<FormState> formKey;
  final List<Tarla> tarlalar;
  final Tarla? seciliTarla;
  final ValueChanged<Tarla?> onTarlaSecildi;
  final TextEditingController gorevCtrl;
  final FocusNode gorevFocus;
  final TextEditingController notCtrl;
  final DateTime planliTarih;
  final VoidCallback onTarihSec;
  final bool kaydediliyor;
  final VoidCallback onKaydet;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Yeni Görev', style: theme.textTheme.titleMedium),
            const SizedBox(height: AppSpacing.md),
            Form(
              key: formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Tarla seçimi
                  DropdownButtonFormField<Tarla>(
                    initialValue: seciliTarla,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Tarla',
                      hintText: 'Tarla seç',
                    ),
                    items: tarlalar
                        .map(
                          (t) => DropdownMenuItem<Tarla>(
                            value: t,
                            child: Text(
                              t.name,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        )
                        .toList(),
                    onChanged: kaydediliyor ? null : onTarlaSecildi,
                    validator: (v) =>
                        v == null ? 'Tarla seçimi zorunludur.' : null,
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Görev adı
                  TextFormField(
                    controller: gorevCtrl,
                    focusNode: gorevFocus,
                    enabled: !kaydediliyor,
                    decoration: const InputDecoration(
                      labelText: 'Görev',
                      hintText: 'Örneğin: Sulama yap',
                    ),
                    textCapitalization: TextCapitalization.sentences,
                    validator: (v) => (v == null || v.trim().isEmpty)
                        ? 'Görev adı boş olamaz.'
                        : null,
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Not (isteğe bağlı)
                  TextFormField(
                    controller: notCtrl,
                    enabled: !kaydediliyor,
                    decoration: const InputDecoration(
                      labelText: 'Not (isteğe bağlı)',
                      hintText: 'Görevle ilgili kısa bir not ekle',
                    ),
                    maxLines: 3,
                    minLines: 1,
                    textCapitalization: TextCapitalization.sentences,
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Planlanan tarih
                  InkWell(
                    onTap: kaydediliyor ? null : onTarihSec,
                    borderRadius: BorderRadius.circular(12),
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'Planlanan Tarih',
                        suffixIcon: Icon(Icons.calendar_today),
                      ),
                      child: Text(
                        _tarihStr(planliTarih),
                        style: theme.textTheme.bodyLarge,
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Kaydet butonu
                  ElevatedButton.icon(
                    onPressed: kaydediliyor ? null : onKaydet,
                    icon: kaydediliyor
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Icon(Icons.add_task),
                    label: const Text('Görevi Ekle'),
                  ),
                ],
              ),
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
          _chip(_Filtre.planlanan, 'Planlanan'),
          const SizedBox(width: AppSpacing.sm),
          _chip(_Filtre.tamamlanan, 'Tamamlanan'),
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
                    // Faaliyet türü
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
                      _tarihStr(kayit.referansTarih),
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

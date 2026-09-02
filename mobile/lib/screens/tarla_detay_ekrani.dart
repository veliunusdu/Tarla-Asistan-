import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../shared/widgets/app_empty_view.dart';
import '../shared/widgets/app_error_view.dart';
import '../shared/widgets/app_loading_view.dart';
import 'faaliyet_ekleme_ekrani.dart';

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

bool _konumYok(double? lat, double? lng) =>
    lat == null || lng == null || (lat == 0.0 && lng == 0.0);

class _TarlaDetayVerisi {
  const _TarlaDetayVerisi({
    required this.faaliyetler,
    required this.planliGorevler,
  });

  final List<Faaliyet> faaliyetler;
  final List<Faaliyet> planliGorevler;
}

class TarlaDetayEkrani extends StatefulWidget {
  const TarlaDetayEkrani({
    super.key,
    required this.tarla,
    FaaliyetRepository? faaliyetRepository,
    this.onArchive,
    this.onEdit,
  }) : _faaliyetRepository =
           faaliyetRepository ?? const LocalFaaliyetRepository();

  final Tarla tarla;
  final FaaliyetRepository _faaliyetRepository;
  final Future<void> Function()? onArchive;
  final Future<bool> Function()? onEdit;

  @visibleForTesting
  FaaliyetRepository get repositoryForTesting => _faaliyetRepository;

  @override
  State<TarlaDetayEkrani> createState() => _TarlaDetayEkraniState();
}

class _TarlaDetayEkraniState extends State<TarlaDetayEkrani>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late Future<_TarlaDetayVerisi> _veri;
  bool _archiving = false;
  String? _tamamlananGorevId;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _veri = _yukle();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  void _yenile() {
    setState(() {
      _veri = _yukle();
    });
  }

  Future<_TarlaDetayVerisi> _yukle() async {
    final repository = widget._faaliyetRepository;
    final faaliyetlerFuture = repository.getFaaliyetler(widget.tarla.id);
    final planliGorevlerFuture = repository is PlanliGorevRepository
        ? (repository as PlanliGorevRepository).getPlanliGorevler()
        : Future.value(<Faaliyet>[]);

    final faaliyetler = await faaliyetlerFuture;
    final planliGorevler = (await planliGorevlerFuture)
        .where((gorev) => gorev.tarlaId == widget.tarla.id)
        .toList();

    return _TarlaDetayVerisi(
      faaliyetler: faaliyetler.where((faaliyet) => faaliyet.isCompleted).toList(),
      planliGorevler: planliGorevler,
    );
  }

  Future<void> _deleteFaaliyet(String id) async {
    final repository = widget._faaliyetRepository;
    if (repository is! FaaliyetDeleteRepository) return;
    try {
      await (repository as FaaliyetDeleteRepository).deleteFaaliyet(id);
      if (mounted) _yenile();
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Faaliyet silinemedi. Lütfen tekrar deneyin.'),
        ),
      );
    }
  }

  Future<void> _goreviTamamla(Faaliyet gorev) async {
    final repository = widget._faaliyetRepository;
    if (repository is! PlanliGorevCompletionRepository) return;

    setState(() => _tamamlananGorevId = gorev.id);
    try {
      await (repository as PlanliGorevCompletionRepository)
          .completePlanliGorev(gorev.id, note: gorev.note);
      if (!mounted) return;
      _yenile();
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

  Future<void> _archive() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Tarlayı arşivle?'),
        content: const Text('Tarla aktif listenizden kaldırılacak.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Arşivle'),
          ),
        ],
      ),
    );
    if (confirmed != true || widget.onArchive == null) return;
    setState(() => _archiving = true);
    try {
      await widget.onArchive!();
      if (mounted) Navigator.pop(context, true);
    } catch (_) {
      if (!mounted) return;
      setState(() => _archiving = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Tarla arşivlenemedi. Lütfen tekrar deneyin.'),
        ),
      );
    }
  }

  Future<void> _edit() async {
    if (widget.onEdit == null) return;
    final updated = await widget.onEdit!();
    if (updated && mounted) Navigator.pop(context, true);
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.tarla.name),
        actions: [
          if (widget.onEdit != null)
            IconButton(
              tooltip: 'Tarlayı düzenle',
              icon: const Icon(Icons.edit_outlined),
              onPressed: _edit,
            ),
          if (widget.onArchive != null)
            IconButton(
              tooltip: 'Tarlayı arşivle',
              icon: _archiving
                  ? const SizedBox.square(
                      dimension: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.archive_outlined),
              onPressed: _archiving ? null : _archive,
            ),
        ],
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(text: 'Planlı İşler', icon: Icon(Icons.event_note)),
            Tab(text: 'Faaliyet Geçmişi', icon: Icon(Icons.history)),
          ],
        ),
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Flexible(
            child: SingleChildScrollView(
              child: _TarlaBilgiKarti(tarla: widget.tarla),
            ),
          ),
          Expanded(child: _faaliyetTabView()),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
        onPressed: () async {
          final result = await Navigator.push<bool>(
            context,
            MaterialPageRoute(
              builder: (context) => FaaliyetEklemeEkrani(
                tarlaId: widget.tarla.id,
                faaliyetRepository: widget._faaliyetRepository,
              ),
            ),
          );
          if (result == true) _yenile();
        },
        icon: const Icon(Icons.add),
        label: const Text('İşlem Kaydet'),
      ),
    );
  }

  Widget _faaliyetTabView() {
    return FutureBuilder<_TarlaDetayVerisi>(
      future: _veri,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const AppLoadingView(message: 'Faaliyetler yükleniyor…');
        }

        if (snapshot.hasError) {
          return AppErrorView(onRetry: _yenile);
        }

        final veri = snapshot.data;
        if (veri == null) return const SizedBox.shrink();

        return TabBarView(
          controller: _tabController,
          children: [
            _FaaliyetListesi(
              faaliyetler: veri.planliGorevler,
              isGecmis: false,
              onDelete: null,
              completingId: _tamamlananGorevId,
              onComplete: widget._faaliyetRepository
                      is PlanliGorevCompletionRepository
                  ? _goreviTamamla
                  : null,
            ),
            _FaaliyetListesi(
              faaliyetler: veri.faaliyetler,
              isGecmis: true,
              onDelete: widget._faaliyetRepository is FaaliyetDeleteRepository
                  ? _deleteFaaliyet
                  : null,
            ),
          ],
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------
// Tarla bilgi kartı
// ---------------------------------------------------------------------------

class _TarlaBilgiKarti extends StatelessWidget {
  const _TarlaBilgiKarti({required this.tarla});

  final Tarla tarla;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final lat = tarla.latitude;
    final lng = tarla.longitude;
    final konumYok = _konumYok(lat, lng);
    final konumMetin = konumYok
        ? 'Konum eklenmedi'
        : '${lat!.toStringAsFixed(5)}, ${lng!.toStringAsFixed(5)}';

    return Card(
      margin: const EdgeInsets.all(AppSpacing.md),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(tarla.name, style: theme.textTheme.titleLarge),
            const SizedBox(height: AppSpacing.sm),
            _InfoSatiri(
              icon: Icons.grass,
              label: 'Ürün',
              deger: tarla.cropType ?? 'Ürün bilgisi yok',
            ),
            _InfoSatiri(
              icon: Icons.straighten,
              label: 'Büyüklük',
              deger: tarla.size != null
                  ? '${tarla.size} dönüm'
                  : 'Alan bilgisi yok',
            ),
            _InfoSatiri(
              icon: Icons.calendar_today,
              label: 'Ekim Tarihi',
              deger: tarla.plantingDate != null
                  ? _tarihStr(tarla.plantingDate!)
                  : 'Ekim tarihi yok',
            ),
            _InfoSatiri(
              icon: Icons.location_on,
              label: 'Konum',
              deger: konumMetin,
              renkli: konumYok,
            ),
          ],
        ),
      ),
    );
  }
}

class _InfoSatiri extends StatelessWidget {
  const _InfoSatiri({
    required this.icon,
    required this.label,
    required this.deger,
    this.renkli = false,
  });

  final IconData icon;
  final String label;
  final String deger;
  final bool renkli;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final degerStyle = theme.textTheme.bodyMedium?.copyWith(
      color: renkli ? AppColors.textDisabled : AppColors.textSecondary,
      fontStyle: renkli ? FontStyle.italic : FontStyle.normal,
    );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: AppColors.primary),
          const SizedBox(width: AppSpacing.sm),
          Text(
            '$label: ',
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          Expanded(child: Text(deger, style: degerStyle)),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Faaliyet listesi
// ---------------------------------------------------------------------------

class _FaaliyetListesi extends StatelessWidget {
  const _FaaliyetListesi({
    required this.faaliyetler,
    required this.isGecmis,
    required this.onDelete,
    this.onComplete,
    this.completingId,
  });

  final List<Faaliyet> faaliyetler;
  final bool isGecmis;
  final Future<void> Function(String id)? onDelete;
  final Future<void> Function(Faaliyet faaliyet)? onComplete;
  final String? completingId;

  @override
  Widget build(BuildContext context) {
    if (faaliyetler.isEmpty) {
      return AppEmptyView(
        icon: isGecmis ? Icons.history : Icons.event_note,
        title: isGecmis
            ? 'Henüz geçmiş faaliyet yok.'
            : 'Planlı iş yok.',
        description: isGecmis
            ? 'Tamamlanan faaliyetler burada görünecek.'
            : 'İş Planım ekranından yeni iş ekleyebilirsiniz.',
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.sm,
      ),
      itemCount: faaliyetler.length,
      itemBuilder: (context, index) {
        return _FaaliyetKarti(
          faaliyet: faaliyetler[index],
          isGecmis: isGecmis,
          onDelete: onDelete,
          onComplete: onComplete,
          completing: completingId == faaliyetler[index].id,
        );
      },
    );
  }
}

class _FaaliyetKarti extends StatelessWidget {
  const _FaaliyetKarti({
    required this.faaliyet,
    required this.isGecmis,
    required this.onDelete,
    this.onComplete,
    this.completing = false,
  });

  final Faaliyet faaliyet;
  final bool isGecmis;
  final Future<void> Function(String id)? onDelete;
  final Future<void> Function(Faaliyet faaliyet)? onComplete;
  final bool completing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.xs,
        ),
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: isGecmis
                ? AppColors.success.withValues(alpha: 0.15)
                : AppColors.warning.withValues(alpha: 0.15),
            shape: BoxShape.circle,
          ),
          child: Icon(
            isGecmis ? Icons.check_circle : Icons.schedule,
            color: isGecmis ? AppColors.success : AppColors.warning,
            size: 20,
          ),
        ),
        title: Text(faaliyet.type, style: theme.textTheme.titleMedium),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (faaliyet.note.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(faaliyet.note, style: theme.textTheme.bodyMedium),
              ),
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                _tarihStr(faaliyet.timestamp),
                style: theme.textTheme.bodyMedium?.copyWith(fontSize: 13),
              ),
            ),
            if (faaliyet.dueDate != null)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(
                  'Plan: ${_tarihStr(faaliyet.dueDate!)}',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: AppColors.warning,
                    fontSize: 13,
                  ),
                ),
              ),
          ],
        ),
        trailing: isGecmis
            ? onDelete == null
                  ? null
                  : IconButton(
                      tooltip: 'Faaliyeti sil',
                      icon: const Icon(Icons.delete_outline),
                      color: AppColors.textDisabled,
                      onPressed: () => onDelete!(faaliyet.id),
                    )
            : onComplete == null
            ? null
            : TextButton.icon(
                onPressed: completing ? null : () => onComplete!(faaliyet),
                icon: completing
                    ? const SizedBox.square(
                        dimension: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: const Text('Tamamla'),
              ),
      ),
    );
  }
}

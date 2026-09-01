import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../models/faaliyet.dart';
import '../models/tarla.dart';
import '../services/database_helper.dart';
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

class TarlaDetayEkrani extends StatefulWidget {
  const TarlaDetayEkrani({
    super.key,
    required this.tarla,
    FaaliyetRepository? faaliyetRepository,
    this.onArchive,
  }) : _faaliyetRepository =
           faaliyetRepository ?? const LocalFaaliyetRepository();

  final Tarla tarla;
  final FaaliyetRepository _faaliyetRepository;
  final Future<void> Function()? onArchive;

  @visibleForTesting
  FaaliyetRepository get repositoryForTesting => _faaliyetRepository;

  @override
  State<TarlaDetayEkrani> createState() => _TarlaDetayEkraniState();
}

class _TarlaDetayEkraniState extends State<TarlaDetayEkrani>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late Future<List<Faaliyet>> _faaliyetler;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _faaliyetler = widget._faaliyetRepository.getFaaliyetler(widget.tarla.id);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  void _yenile() {
    setState(() {
      _faaliyetler = widget._faaliyetRepository.getFaaliyetler(widget.tarla.id);
    });
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
    await widget.onArchive!();
    if (mounted) Navigator.pop(context, true);
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
          if (widget.onArchive != null)
            IconButton(
              tooltip: 'Tarlayı arşivle',
              icon: const Icon(Icons.archive_outlined),
              onPressed: _archive,
            ),
        ],
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(text: 'Yapılacaklar', icon: Icon(Icons.event_note)),
            Tab(text: 'Geçmiş', icon: Icon(Icons.history)),
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
      floatingActionButton: FloatingActionButton(
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
        child: const Icon(Icons.add),
      ),
    );
  }

  Widget _faaliyetTabView() {
    return FutureBuilder<List<Faaliyet>>(
      future: _faaliyetler,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const AppLoadingView(message: 'Faaliyetler yükleniyor…');
        }

        if (snapshot.hasError) {
          return AppErrorView(onRetry: _yenile);
        }

        final all = snapshot.data ?? [];
        final yapilacaklar = all.where((f) => !f.isCompleted).toList();
        final gecmis = all.where((f) => f.isCompleted).toList();

        return TabBarView(
          controller: _tabController,
          children: [
            _FaaliyetListesi(
              faaliyetler: yapilacaklar,
              isGecmis: false,
              onDelete: (id) async {
                await DatabaseHelper.instance.deleteFaaliyet(id);
                _yenile();
              },
            ),
            _FaaliyetListesi(
              faaliyetler: gecmis,
              isGecmis: true,
              onDelete: (id) async {
                await DatabaseHelper.instance.deleteFaaliyet(id);
                _yenile();
              },
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
  });

  final List<Faaliyet> faaliyetler;
  final bool isGecmis;
  final Future<void> Function(String id) onDelete;

  @override
  Widget build(BuildContext context) {
    if (faaliyetler.isEmpty) {
      return AppEmptyView(
        icon: isGecmis ? Icons.history : Icons.event_note,
        title: isGecmis
            ? 'Henüz geçmiş faaliyet yok.'
            : 'Planlanmış faaliyet yok.',
        description: isGecmis
            ? 'Tamamlanan faaliyetler burada görünecek.'
            : 'Faaliyet eklemek için + butonuna dokunun.',
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
  });

  final Faaliyet faaliyet;
  final bool isGecmis;
  final Future<void> Function(String id) onDelete;

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
        trailing: IconButton(
          icon: const Icon(Icons.delete_outline),
          color: AppColors.textDisabled,
          onPressed: () => onDelete(faaliyet.id),
        ),
      ),
    );
  }
}

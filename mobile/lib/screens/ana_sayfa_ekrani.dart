import 'package:flutter/material.dart';

import '../app/theme/app_colors.dart';
import '../app/theme/app_spacing.dart';
import '../features/activities/data/faaliyet_repository.dart';
import '../features/activities/data/local_faaliyet_repository.dart';
import '../features/fields/data/farm_summary_repository.dart';
import '../features/fields/data/local_tarla_repository.dart';
import '../features/fields/data/tarla_location_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../features/location/data/location_service.dart';
import '../features/weather/data/unavailable_weather_repository.dart';
import '../features/weather/data/backend_weather_repository.dart';
import '../features/weather/data/weather_repository.dart';
import '../features/weather/domain/weather_summary.dart';
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
import 'tarla_konum_duzenleme_ekrani.dart';
import 'tarla_listesi_ekrani.dart';

// ---------------------------------------------------------------------------
// Ekran widget'ı
// ---------------------------------------------------------------------------

class AnaSayfaEkrani extends StatefulWidget {
  const AnaSayfaEkrani({
    super.key,
    TarlaRepository? tarlaRepository,
    FaaliyetRepository? faaliyetRepository,
    WeatherRepository? weatherRepository,
    this.locationService,
    this.locationPicker,
    this.onTarlalarimSekme,
    this.onGunlukSekme,
    this.refreshNotifier,
  }) : _tarlaRepo = tarlaRepository ?? const LocalTarlaRepository(),
       _faaliyetRepo = faaliyetRepository ?? const LocalFaaliyetRepository(),
       _weatherRepo = weatherRepository ?? const UnavailableWeatherRepository();

  final TarlaRepository _tarlaRepo;
  final FaaliyetRepository _faaliyetRepo;
  final WeatherRepository _weatherRepo;
  final LocationService? locationService;
  final FieldLocationPicker? locationPicker;

  @visibleForTesting
  FaaliyetRepository get faaliyetRepositoryForTesting => _faaliyetRepo;

  /// Tarlalarım sekmesine geçmek için AnaEkran'dan gelen callback.
  final VoidCallback? onTarlalarimSekme;

  /// Günlük sekmesine geçmek için AnaEkran'dan gelen callback.
  final VoidCallback? onGunlukSekme;

  /// Diğer sekmelerde veri değiştiğinde artırılan sinyal.
  /// Değiştiğinde hava durumu hariç tarlalar ve görevler yenilenir.
  final ValueNotifier<int>? refreshNotifier;

  @override
  State<AnaSayfaEkrani> createState() => _AnaSayfaEkraniState();
}

class _AnaSayfaEkraniState extends State<AnaSayfaEkrani> {
  late Future<List<Tarla>> _tarlalar;
  late Future<List<Faaliyet>> _faaliyetler;
  late Future<WeatherSummary> _weather;
  late Future<(List<Tarla>, List<Faaliyet>)> _gorevVerisi;

  /// Hızlı işlemler için senkron tarla listesi önbelleği.
  List<Tarla> _tarlalarCache = [];

  @override
  void initState() {
    super.initState();
    _initData();
    widget.refreshNotifier?.addListener(_yenileTarlaVeFaaliyetler);
  }

  @override
  void dispose() {
    widget.refreshNotifier?.removeListener(_yenileTarlaVeFaaliyetler);
    super.dispose();
  }

  Future<List<Faaliyet>> _fetchGorevler() async {
    final repo = widget._faaliyetRepo;
    final map = <String, Faaliyet>{};

    if (repo is PlanliGorevRepository) {
      try {
        final planli =
            await (repo as PlanliGorevRepository).getPlanliGorevler();
        for (final f in planli) {
          if (!f.isCompleted && f.dueDate != null) {
            map[f.id] = f;
          }
        }
      } catch (_) {}
    }

    try {
      final tum = await repo.getTumFaaliyetler();
      for (final f in tum) {
        if (!f.isCompleted && f.dueDate != null) {
          map[f.id] = f;
        }
      }
    } catch (_) {}

    return map.values.toList();
  }

  void _initData() {
    final summaryRepo = widget._tarlaRepo is FarmSummaryRepository
        ? widget._tarlaRepo as FarmSummaryRepository
        : null;

    if (summaryRepo != null) {
      final summaryFuture = summaryRepo.getFarmSummary();
      _tarlalar = summaryFuture.then((s) {
        final list = s.farms.map((f) => f.tarla).toList();
        _tarlalarCache = list;
        return list;
      });
      _faaliyetler = summaryFuture.then((s) => s.upcomingTasks);
    } else {
      _tarlalar = widget._tarlaRepo.getTarlalar().then((list) {
        _tarlalarCache = list;
        return list;
      });
      _faaliyetler = _fetchGorevler();
    }
    _weather = _fetchWeather();
    _gorevVerisi = Future.wait([
      _tarlalar,
      _faaliyetler,
    ]).then((r) => (r[0] as List<Tarla>, r[1] as List<Faaliyet>));
  }

  Future<WeatherSummary> _fetchWeather() async {
    String? farmId;
    try {
      final tarlalar = await _tarlalar;
      final tarlaWithLocation = tarlalar.cast<Tarla?>().firstWhere(
        (t) => t != null && t.latitude != null && t.longitude != null,
        orElse: () => null,
      );
      if (tarlaWithLocation != null) {
        farmId = tarlaWithLocation.id;
      }
    } catch (_) {}
    return widget._weatherRepo.getWeather(farmId: farmId);
  }

  /// Tarla/görev verisi değiştiğinde çağrılır.
  /// Hava durumu gereksiz yere yeniden istenmez.
  void _yenileTarlaVeFaaliyetler() {
    setState(() {
      final summaryRepo = widget._tarlaRepo is FarmSummaryRepository
          ? widget._tarlaRepo as FarmSummaryRepository
          : null;

      if (summaryRepo != null) {
        final summaryFuture = summaryRepo.getFarmSummary();
        _tarlalar = summaryFuture.then((s) {
          final list = s.farms.map((f) => f.tarla).toList();
          _tarlalarCache = list;
          return list;
        });
        _faaliyetler = summaryFuture.then((s) => s.upcomingTasks);
      } else {
        _tarlalar = widget._tarlaRepo.getTarlalar().then((list) {
          _tarlalarCache = list;
          return list;
        });
        _faaliyetler = _fetchGorevler();
      }
      _gorevVerisi = Future.wait([
        _tarlalar,
        _faaliyetler,
      ]).then((r) => (r[0] as List<Tarla>, r[1] as List<Faaliyet>));
    });
  }

  void _yenile() {
    setState(_initData);
  }

  void _yenileHava() {
    setState(() {
      _weather = _fetchWeather();
    });
  }

  // ---------------------------------------------------------------------------
  // Hızlı işlemler
  // ---------------------------------------------------------------------------

  Future<void> _tarlaEkle() async {
    final result = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => TarlaEklemeEkrani(repository: widget._tarlaRepo),
      ),
    );
    if (result == true && mounted) _yenile();
  }

  Future<void> _konumEkle() async {
    final tarlaRepo = widget._tarlaRepo;
    if (tarlaRepo is! TarlaLocationRepository) {
      return _tarlaEkle();
    }

    final tarla = _tarlalarCache.cast<Tarla?>().firstWhere(
      (t) => t?.latitude == null || t?.longitude == null,
      orElse: () => _tarlalarCache.isNotEmpty ? _tarlalarCache.first : null,
    );

    if (tarla == null) {
      return _tarlaEkle();
    }

    final result = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => TarlaKonumDuzenlemeEkrani(
          tarla: tarla,
          repository: tarlaRepo as TarlaLocationRepository,
          locationService: widget.locationService,
          locationPicker: widget.locationPicker,
        ),
      ),
    );
    if (result == true && mounted) {
      _yenile();
    }
  }

  Future<void> _isEkle({bool isCompleted = true}) async {
    if (_tarlalarCache.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text(
            'İş eklemek için önce en az bir tarla eklemelisiniz.',
          ),
          action: SnackBarAction(label: 'Tarla Ekle', onPressed: _tarlaEkle),
        ),
      );
      return;
    }

    Tarla? secilen;

    if (_tarlalarCache.length == 1) {
      secilen = _tarlalarCache.first;
    } else {
      secilen = await TarlaSecimBottomSheet.show(
        context,
        tarlalar: _tarlalarCache,
      );
    }

    if (secilen == null || !mounted) return;

    final result = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => FaaliyetEklemeEkrani(
          tarlaId: secilen!.id,
          faaliyetRepository: widget._faaliyetRepo,
          initialIsCompleted: isCompleted,
          initialSelectedDate: isCompleted ? null : DateTime.now(),
        ),
      ),
    );
    if (result == true && mounted) _yenile();
  }

  // ---------------------------------------------------------------------------
  // Build
  // ---------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Tarım Asistanı')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // ── Karşılama ────────────────────────────────────────────────
              Text(
                'Bugün tarlanızda ne yapmalısınız?',
                style: theme.textTheme.headlineSmall,
              ),
              const SizedBox(height: AppSpacing.lg),

              // ── Hava durumu ───────────────────────────────────────────────
              _HavaDurumuSection(
                future: _weather,
                onRetry: _yenileHava,
                onTarlaEkle: _tarlaEkle,
                onKonumEkle: _konumEkle,
              ),
              const SizedBox(height: AppSpacing.lg),

              // ── Tarla istatistikleri ──────────────────────────────────────
              _TarlaIstatistikSection(
                future: _tarlalar,
                onRetry: _yenile,
                onTarlaEkle: _tarlaEkle,
              ),
              const SizedBox(height: AppSpacing.lg),

              // ── Yaklaşan işler ───────────────────────────────────────────
              _YaklasanGorevlerSection(
                gorevVerisi: _gorevVerisi,
                onRetry: _yenile,
                onFaaliyetPlanla: () => _isEkle(isCompleted: false),
              ),
              const SizedBox(height: AppSpacing.lg),

              // ── Hızlı işlemler ────────────────────────────────────────────
              _HizliIslemlerSection(
                onTarlaEkle: _tarlaEkle,
                onFaaliyetEkle: () => _isEkle(isCompleted: true),
                onTarlalarim:
                    widget.onTarlalarimSekme ??
                    () => Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) =>
                            TarlaListesiEkrani(repository: widget._tarlaRepo),
                      ),
                    ),
                onTumFaaliyetler: widget.onGunlukSekme,
              ),
              const SizedBox(height: AppSpacing.xl),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Hava durumu bölümü
// ---------------------------------------------------------------------------

class _HavaDurumuSection extends StatelessWidget {
  const _HavaDurumuSection({
    required this.future,
    required this.onRetry,
    required this.onTarlaEkle,
    required this.onKonumEkle,
  });

  final Future<WeatherSummary> future;
  final VoidCallback onRetry;
  final VoidCallback onTarlaEkle;
  final VoidCallback onKonumEkle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Hava Durumu', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        FutureBuilder<WeatherSummary>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const AppLoadingView(message: 'Hava durumu yükleniyor…');
            }
            if (snapshot.hasError) {
              if (snapshot.error is WeatherLocationRequiredException) {
                return Card(
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Hava durumu için tarla konumu ekleyin'),
                        const SizedBox(height: AppSpacing.sm),
                        OutlinedButton(
                          onPressed: onKonumEkle,
                          child: const Text('Konum ekle'),
                        ),
                      ],
                    ),
                  ),
                );
              }
              return AppErrorView(
                title: 'Hava durumu alınamadı',
                description:
                    'İnternet bağlantınızı kontrol edip tekrar deneyin.',
                onRetry: onRetry,
              );
            }
            final hava = snapshot.data!;
            final desc = hava.description.isEmpty
                ? ''
                : hava.description[0].toUpperCase() +
                      hava.description.substring(1);

            return Card(
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.md,
                  vertical: AppSpacing.sm,
                ),
                leading: const Icon(
                  Icons.cloud_outlined,
                  color: AppColors.textSecondary,
                  size: 32,
                ),
                title: Text(
                  '${hava.temperature}°C',
                  style: theme.textTheme.titleMedium,
                ),
                subtitle: Text(desc, style: theme.textTheme.bodyMedium),
              ),
            );
          },
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Tarla istatistikleri bölümü
// ---------------------------------------------------------------------------

class _TarlaIstatistikSection extends StatelessWidget {
  const _TarlaIstatistikSection({
    required this.future,
    required this.onRetry,
    required this.onTarlaEkle,
  });

  final Future<List<Tarla>> future;
  final VoidCallback onRetry;
  final VoidCallback onTarlaEkle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Tarla Özeti', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        FutureBuilder<List<Tarla>>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const AppLoadingView(
                message: 'Tarla bilgileri yükleniyor…',
              );
            }
            if (snapshot.hasError) {
              return AppErrorView(onRetry: onRetry);
            }
            final tarlalar = snapshot.data ?? [];
            if (tarlalar.isEmpty) {
              return AppEmptyView(
                icon: Icons.terrain,
                title: 'Henüz tarla eklenmedi.',
                description: 'İlk tarlanızı ekleyerek başlayın.',
                actionLabel: 'Tarla Ekle',
                onAction: onTarlaEkle,
              );
            }
            final toplamAlan = tarlalar.fold<double>(
              0,
              (s, t) => s + (t.size ?? 0.0),
            );
            return Row(
              children: [
                Expanded(
                  child: _StatKarti(
                    baslik: 'Toplam Tarla',
                    deger: '${tarlalar.length}',
                    icon: Icons.terrain,
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: _StatKarti(
                    baslik: 'Toplam Alan',
                    deger: '${toplamAlan.toInt()} dönüm',
                    icon: Icons.straighten,
                  ),
                ),
              ],
            );
          },
        ),
      ],
    );
  }
}

class _StatKarti extends StatelessWidget {
  const _StatKarti({
    required this.baslik,
    required this.deger,
    required this.icon,
  });

  final String baslik;
  final String deger;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: AppColors.primary, size: 28),
            const SizedBox(height: AppSpacing.sm),
            Text(deger, style: theme.textTheme.headlineSmall),
            Text(baslik, style: theme.textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Yaklaşan görevler bölümü
// ---------------------------------------------------------------------------

class _YaklasanGorevlerSection extends StatelessWidget {
  const _YaklasanGorevlerSection({
    required this.gorevVerisi,
    required this.onRetry,
    required this.onFaaliyetPlanla,
  });

  final Future<(List<Tarla>, List<Faaliyet>)> gorevVerisi;
  final VoidCallback onRetry;
  final VoidCallback onFaaliyetPlanla;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Yaklaşan İşler', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        FutureBuilder<(List<Tarla>, List<Faaliyet>)>(
          future: gorevVerisi,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const AppLoadingView(message: 'İşler yükleniyor…');
            }
            if (snapshot.hasError) {
              return AppErrorView(onRetry: onRetry);
            }

            final (tarlalar, faaliyetler) = snapshot.data!;
            final tarlaMap = {for (final t in tarlalar) t.id: t};

            final yaklasanlar =
                faaliyetler
                    .where((f) => !f.isCompleted && f.dueDate != null)
                    .toList()
                  ..sort((a, b) => a.dueDate!.compareTo(b.dueDate!));

            final gosterilen = yaklasanlar.take(3).toList();

            if (gosterilen.isEmpty) {
              return AppEmptyView(
                icon: Icons.event_available,
                title: 'Planlanmış işin yok.',
                description:
                    'Yeni bir iş planlayarak başlayabilirsin.',
                actionLabel: 'İş Planla',
                onAction: onFaaliyetPlanla,
              );
            }

            return Column(
              children: gosterilen.map((f) {
                final tarla = tarlaMap[f.tarlaId];
                return _GorevKarti(
                  faaliyet: f,
                  tarlaAdi: tarla?.name ?? 'Bilinmeyen tarla',
                  tarla: tarla,
                );
              }).toList(),
            );
          },
        ),
      ],
    );
  }
}

class _GorevKarti extends StatelessWidget {
  const _GorevKarti({
    required this.faaliyet,
    required this.tarlaAdi,
    this.tarla,
  });

  final Faaliyet faaliyet;
  final String tarlaAdi;
  final Tarla? tarla;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: InkWell(
        onTap: tarla != null
            ? () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => TarlaDetayEkrani(tarla: tarla!),
                ),
              )
            : null,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              // İkon konteyner
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.15),
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.schedule,
                  color: AppColors.warning,
                  size: 22,
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      tarlaAdi,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                    Text(faaliyet.type, style: theme.textTheme.titleMedium),
                    Text(
                      formatTarih(faaliyet.dueDate!),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.warning,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: AppColors.textDisabled),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Hızlı işlemler bölümü
// ---------------------------------------------------------------------------

class _HizliIslemlerSection extends StatelessWidget {
  const _HizliIslemlerSection({
    required this.onTarlaEkle,
    required this.onFaaliyetEkle,
    required this.onTarlalarim,
    this.onTumFaaliyetler,
  });

  final VoidCallback onTarlaEkle;
  final VoidCallback onFaaliyetEkle;
  final VoidCallback onTarlalarim;
  final VoidCallback? onTumFaaliyetler;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Hızlı İşlemler', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.sm),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: AppSpacing.sm,
          crossAxisSpacing: AppSpacing.sm,
          childAspectRatio: 2.2,
          children: [
            _HizliIslemButonu(
              icon: Icons.add_location_alt,
              label: 'Tarla Ekle',
              onTap: onTarlaEkle,
            ),
            _HizliIslemButonu(
              icon: Icons.add_task,
              label: 'İş Ekle',
              onTap: onFaaliyetEkle,
            ),
            _HizliIslemButonu(
              icon: Icons.grass,
              label: 'Tarlalarım',
              onTap: onTarlalarim,
            ),
            _HizliIslemButonu(
              icon: Icons.event_note,
              label: 'İş Planım',
              onTap: onTumFaaliyetler,
            ),
          ],
        ),
      ],
    );
  }
}

class _HizliIslemButonu extends StatelessWidget {
  const _HizliIslemButonu({
    required this.icon,
    required this.label,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs = Theme.of(context).colorScheme;

    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: AppSpacing.sm,
          ),
          child: Row(
            children: [
              Icon(icon, color: cs.primary, size: 22),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  label,
                  style: theme.textTheme.labelLarge?.copyWith(
                    color: AppColors.textPrimary,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

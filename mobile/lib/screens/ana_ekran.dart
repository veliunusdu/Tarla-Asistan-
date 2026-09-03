import 'package:flutter/material.dart';

import '../features/activities/data/faaliyet_repository.dart';
import '../features/ai_assistant/data/ai_assistant_repository.dart';
import '../features/cases/data/case_repository.dart';
import '../features/fields/data/tarla_repository.dart';
import '../features/profile/data/profile_repository.dart';
import '../features/weather/data/weather_repository.dart';
import '../services/database_helper.dart';
import 'ai_asistan_ekrani.dart';
import 'ana_sayfa_ekrani.dart';
import 'profil_ekrani.dart';
import 'tarla_gunlugu_ekrani.dart';
import 'tarla_listesi_ekrani.dart';

// ---------------------------------------------------------------------------
// Sekme tanımları — raw indeks yerine tip güvenli enum
// ---------------------------------------------------------------------------

enum _Sekme { anaSayfa, gunlugum, tarlalarim, asistan, profil }

// ---------------------------------------------------------------------------
// AnaEkran
// ---------------------------------------------------------------------------

class AnaEkran extends StatefulWidget {
  const AnaEkran({
    super.key,
    TarlaRepository? tarlaRepository,
    FaaliyetRepository? faaliyetRepository,
    WeatherRepository? weatherRepository,
    AiAssistantRepository? aiRepository,
    ProfileRepository? profileRepository,
    CaseRepository? caseRepository,
    Future<void> Function()? onLogout,
  }) : _tarlaRepo = tarlaRepository,
       _faaliyetRepo = faaliyetRepository,
       _weatherRepo = weatherRepository,
       _aiRepo = aiRepository,
       _profileRepo = profileRepository,
       _caseRepo = caseRepository,
       _onLogout = onLogout;

  final TarlaRepository? _tarlaRepo;
  final FaaliyetRepository? _faaliyetRepo;
  final WeatherRepository? _weatherRepo;
  final AiAssistantRepository? _aiRepo;
  final ProfileRepository? _profileRepo;
  final CaseRepository? _caseRepo;
  final Future<void> Function()? _onLogout;

  @override
  State<AnaEkran> createState() => _AnaEkranState();
}

class _AnaEkranState extends State<AnaEkran> {
  _Sekme _secilen = _Sekme.anaSayfa;

  /// Tarlalar veya görevler değiştiğinde artırılan sinyal.
  final ValueNotifier<int> _refreshNotifier = ValueNotifier(0);

  void _gotoGunlugum() => setState(() => _secilen = _Sekme.gunlugum);
  void _gotoTarlalarim() => setState(() => _secilen = _Sekme.tarlalarim);
  void _onDataChanged() => _refreshNotifier.value++;

  late final List<Widget> _sayfalar;

  @override
  void initState() {
    super.initState();
    _sayfalar = [
      // 0 — Ana Sayfa
      AnaSayfaEkrani(
        tarlaRepository: widget._tarlaRepo,
        faaliyetRepository: widget._faaliyetRepo,
        weatherRepository: widget._weatherRepo,
        caseRepository: widget._caseRepo,
        onTarlalarimSekme: _gotoTarlalarim,
        onGunlukSekme: _gotoGunlugum,
        refreshNotifier: _refreshNotifier,
      ),
      // 1 — İş Planım
      TarlaGunluguEkrani(
        tarlaRepository: widget._tarlaRepo,
        faaliyetRepository: widget._faaliyetRepo,
        onDataChanged: _onDataChanged,
      ),
      // 2 — Tarlalarım
      TarlaListesiEkrani(
        repository: widget._tarlaRepo,
        faaliyetRepository: widget._faaliyetRepo,
        caseRepository: widget._caseRepo,
        onDataChanged: _onDataChanged,
      ),
      // 3 — Asistan
      AiAsistanEkrani(repository: widget._aiRepo),
      // 4 — Profil
      ProfilEkrani(
        repository: widget._profileRepo,
        caseRepository: widget._caseRepo,
        tarlaRepository: widget._tarlaRepo,
        onLogout: widget._onLogout,
      ),
    ];
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _checkOrphanedData();
    });
  }

  Future<void> _checkOrphanedData() async {
    try {
      final summary = await DatabaseHelper.instance.getOrphanedDataSummary();
      if (!summary.hasOrphanedData || !mounted) return;

      final parts = <String>[];
      if (summary.farmCount > 0) parts.add('${summary.farmCount} tarla');
      if (summary.activityCount > 0) parts.add('${summary.activityCount} iş kaydı');
      final details = parts.join(' ve ');

      final shouldClean = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) => AlertDialog(
          title: const Text('Eski / Sahipsiz Veriler Bulundu'),
          content: Text(
            'Bu cihazda önceki oturumdan kalan $details bulundu.\n\n'
            'Güvenlik ve gizlilik nedeniyle, başka bir kullanıcıya ait olabilecek bu veriler mevcut hesabınıza aktarılamaz. '
            'Verileri bu cihazdan temizlemek istiyor musunuz?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Daha Sonra'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Cihazdan Temizle'),
            ),
          ],
        ),
      );

      if (shouldClean == true) {
        await DatabaseHelper.instance.clearOrphanedRecords();
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Sahipsiz yerel veriler cihazdan temizlendi.')),
          );
        }
      }
    } catch (_) {
      // Sessizce yut
    }
  }

  @override
  void dispose() {
    _refreshNotifier.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      // Ana Sayfa dışındaki sekmede geri tuşuna basılınca Ana Sayfa'ya dön
      canPop: _secilen == _Sekme.anaSayfa,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop && _secilen != _Sekme.anaSayfa) {
          setState(() => _secilen = _Sekme.anaSayfa);
        }
      },
      child: Scaffold(
        body: IndexedStack(index: _secilen.index, children: _sayfalar),
        bottomNavigationBar: NavigationBar(
          selectedIndex: _secilen.index,
          onDestinationSelected: (i) =>
              setState(() => _secilen = _Sekme.values[i]),
          destinations: const [
            NavigationDestination(
              icon: Icon(Icons.home_outlined),
              selectedIcon: Icon(Icons.home),
              label: 'Ana Sayfa',
            ),
            NavigationDestination(
              icon: Icon(Icons.event_note_outlined),
              selectedIcon: Icon(Icons.event_note),
              label: 'İş Planım',
            ),
            NavigationDestination(
              icon: Icon(Icons.grass_outlined),
              selectedIcon: Icon(Icons.grass),
              label: 'Tarlalarım',
            ),
            NavigationDestination(
              icon: Icon(Icons.smart_toy_outlined),
              selectedIcon: Icon(Icons.smart_toy),
              label: 'Asistan',
            ),
            NavigationDestination(
              icon: Icon(Icons.person_outline),
              selectedIcon: Icon(Icons.person),
              label: 'Profil',
            ),
          ],
        ),
      ),
    );
  }
}

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/farm_summary_model.dart';
import 'package:mobile/features/fields/data/farm_summary_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/features/location/data/location_service.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
import 'package:mobile/features/weather/data/weather_repository.dart';
import 'package:mobile/features/weather/data/backend_weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';
import 'package:mobile/screens/ana_sayfa_ekrani.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';
import 'package:mobile/screens/tarla_konum_duzenleme_ekrani.dart';
import 'package:mobile/screens/tarla_listesi_ekrani.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeTarlaRepository implements TarlaRepository, TarlaLocationRepository {
  FakeTarlaRepository(this._future);
  final Future<List<Tarla>> _future;

  String? lastUpdatedId;
  TarlaLocation? lastUpdatedLocation;

  @override
  Future<List<Tarla>> getTarlalar() => _future;

  @override
  Future<void> addTarla(Tarla tarla) async {}

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) async {
    lastUpdatedId = id;
    lastUpdatedLocation = location;
  }
}

class FakeWeatherRepository implements WeatherRepository {
  FakeWeatherRepository(this._future);
  final Future<WeatherSummary> _future;

  @override
  Future<WeatherSummary> getWeather({String? farmId}) => _future;
}

class FakeFarmSummaryTarlaRepository
    implements TarlaRepository, FarmSummaryRepository {
  FakeFarmSummaryTarlaRepository(this.summary);
  final FarmSummaryResponse summary;
  int getFarmSummaryCallCount = 0;

  @override
  Future<List<Tarla>> getTarlalar() async =>
      summary.farms.map((f) => f.tarla).toList();

  @override
  Future<void> addTarla(Tarla tarla) async {}

  @override
  Future<FarmSummaryResponse> getFarmSummary({int upcomingLimit = 5}) async {
    getFarmSummaryCallCount++;
    return summary;
  }
}

class FakeLocationService implements LocationService {
  FakeLocationService({this.location, this.error});

  final TarlaLocation? location;
  final Object? error;

  @override
  Future<TarlaLocation> getCurrentLocation() async {
    if (error != null) throw error!;
    return location ??
        const TarlaLocation(latitude: 38.4237, longitude: 27.1428);
  }
}

class FakeFaaliyetRepository
    implements FaaliyetRepository, PlanliGorevRepository {
  FakeFaaliyetRepository({
    Future<List<Faaliyet>>? faaliyetlerFuture,
    Future<List<Faaliyet>>? planliGorevlerFuture,
  })  : _future = faaliyetlerFuture ?? Future.value([]),
        _planliFuture = planliGorevlerFuture;

  final Future<List<Faaliyet>> _future;
  final Future<List<Faaliyet>>? _planliFuture;
  int getTumFaaliyetlerCallCount = 0;
  int getPlanliGorevlerCallCount = 0;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() {
    getTumFaaliyetlerCallCount++;
    return _future;
  }

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {}

  @override
  Future<List<Faaliyet>> getPlanliGorevler() {
    getPlanliGorevlerCallCount++;
    return _planliFuture ?? Future.value([]);
  }

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Widget _wrap({
  required TarlaRepository tarlaRepo,
  required WeatherRepository weatherRepo,
  FaaliyetRepository? faaliyetRepo,
  LocationService? locationService,
  FieldLocationPicker? locationPicker,
  VoidCallback? onTarlalarimSekme,
  VoidCallback? onGunlukSekme,
  ValueNotifier<int>? refreshNotifier,
}) => MaterialApp(
  theme: AppTheme.light,
  home: AnaSayfaEkrani(
    tarlaRepository: tarlaRepo,
    faaliyetRepository: faaliyetRepo ?? FakeFaaliyetRepository(),
    weatherRepository: weatherRepo,
    locationService: locationService,
    locationPicker: locationPicker,
    onTarlalarimSekme: onTarlalarimSekme,
    onGunlukSekme: onGunlukSekme,
    refreshNotifier: refreshNotifier,
  ),
);

Tarla _tarla(String id, {double size = 10, String? name}) => Tarla(
  id: id,
  name: name ?? 'Tarla $id',
  latitude: 0,
  longitude: 0,
  size: size,
  cropType: 'Buğday',
  plantingDate: DateTime(2024),
);

Faaliyet _planliF(String id, {required DateTime dueDate}) => Faaliyet(
  id: id,
  tarlaId: 't1',
  type: 'Sulama',
  note: '',
  timestamp: DateTime(2025),
  isCompleted: false,
  dueDate: dueDate,
);

Faaliyet _tamamlanmisF(String id) => Faaliyet(
  id: id,
  tarlaId: 't1',
  type: 'Gübreleme',
  note: '',
  timestamp: DateTime(2025),
  isCompleted: true,
  dueDate: null,
);

const _hava = WeatherSummary(temperature: 27, description: 'açık');

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('AnaSayfaEkrani', () {
    // ── Tarla istatistikleri ──────────────────────────────────────────────
    group('tarla istatistikleri', () {
      testWidgets('tarla sayısı ve toplam alan doğru hesaplanır', (
        tester,
      ) async {
        final repo = FakeTarlaRepository(
          Future.value([_tarla('1', size: 20), _tarla('2', size: 30)]),
        );
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('2'), findsOneWidget);
        expect(find.textContaining('50'), findsOneWidget);
      });

      testWidgets('tarla yoksa empty state gösterilir', (tester) async {
        final repo = FakeTarlaRepository(Future.value([]));
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.textContaining('Henüz tarla eklenmedi'), findsOneWidget);
      });

      testWidgets('tarla yüklenirken loading gösterilir', (tester) async {
        final completer = Completer<List<Tarla>>();
        final repo = FakeTarlaRepository(completer.future);
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );

        expect(find.byType(CircularProgressIndicator), findsWidgets);
        completer.complete([]);
      });

      testWidgets('tarla hatasında AppErrorView gösterilir ve retry çalışır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        int callCount = 0;
        late final Completer<List<Tarla>> c1;
        c1 = Completer();

        final repo = _CountingTarlaRepo(() {
          callCount++;
          if (callCount == 1) return c1.future;
          return Future.value([_tarla('1')]);
        });

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );

        c1.completeError('db hatası');
        await tester.pump();

        expect(find.textContaining('Tekrar'), findsWidgets);

        await tester.tap(find.textContaining('Tekrar').first);
        await tester.pumpAndSettle();

        expect(callCount, greaterThanOrEqualTo(2));
      });
    });

    // ── Hava durumu ───────────────────────────────────────────────────────
    group('hava durumu', () {
      testWidgets('sıcaklık ve açıklama gösterilir', (tester) async {
        final repo = FakeTarlaRepository(Future.value([_tarla('1')]));
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.textContaining('27'), findsOneWidget);
        // description ilk harfi büyütülür: "açık" → "Açık"
        expect(find.textContaining('çık'), findsOneWidget);
      });

      testWidgets('hava hatasında kullanıcı dostu hata gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final tarlaRepo = FakeTarlaRepository(Future.value([_tarla('1')]));
        final weatherCompleter = Completer<WeatherSummary>();
        final weatherRepo = FakeWeatherRepository(weatherCompleter.future);

        await tester.pumpWidget(
          _wrap(tarlaRepo: tarlaRepo, weatherRepo: weatherRepo),
        );

        weatherCompleter.completeError('network');
        await tester.pump();

        expect(find.textContaining('Hava durumu alınamadı'), findsOneWidget);
      });

      testWidgets('konum eksikliğinde tarla konumu yönlendirmesi gösterilir', (
        tester,
      ) async {
        final weather = Completer<WeatherSummary>();
        weather.future.ignore();
        final field = Tarla(
          id: 'farm-123',
          name: 'Konumsuz Tarla',
          latitude: null,
          longitude: null,
          size: 15,
          cropType: 'Buğday',
          plantingDate: DateTime(2026, 1, 1),
        );
        final tarlaRepo = FakeTarlaRepository(Future.value([field]));

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: tarlaRepo,
            weatherRepo: FakeWeatherRepository(weather.future),
          ),
        );
        weather.completeError(const WeatherLocationRequiredException());
        await tester.pumpAndSettle();

        final weatherCard = find.ancestor(
          of: find.text('Hava durumu için tarla konumu ekleyin'),
          matching: find.byType(Card),
        );
        expect(
          find.descendant(of: weatherCard, matching: find.text('Konum ekle')),
          findsOneWidget,
        );
        expect(
          find.descendant(of: weatherCard, matching: find.text('Tarla Ekle')),
          findsNothing,
        );

        await tester.tap(find.text('Konum ekle'));
        await tester.pumpAndSettle();

        expect(find.byType(TarlaKonumDuzenlemeEkrani), findsOneWidget);
      });

      testWidgets(
        'konum ekleme ekranı başarılı kapandığında hava durumu ve tarlalar yeniden istenir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          int weatherCalls = 0;
          int tarlaCalls = 0;

          final fieldWithoutLoc = Tarla(
            id: 'f-1',
            name: 'Mısır Tarlası',
            latitude: null,
            longitude: null,
            size: 20,
            cropType: 'Mısır',
            plantingDate: DateTime(2026, 2, 1),
          );

          final weatherRepo = _CountingWeatherRepo(() async {
            weatherCalls++;
            return const WeatherSummary(temperature: 24, description: 'Güneşli');
          });

          final tarlaRepo = _CountingTarlaLocationRepo(() async {
            tarlaCalls++;
            return [fieldWithoutLoc];
          });

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: tarlaRepo,
              weatherRepo: weatherRepo,
              locationService: FakeLocationService(
                location: const TarlaLocation(latitude: 39.9, longitude: 32.8),
              ),
            ),
          );
          await tester.pumpAndSettle();

          // Tarlada konum olmadığı için hava durumu API'si baştan çağrılmaz
          expect(weatherCalls, 0);
          expect(tarlaCalls, 1);
          expect(find.text('Konum ekle'), findsOneWidget);

          await tester.tap(find.text('Konum ekle'));
          await tester.pumpAndSettle();

          expect(find.byType(TarlaKonumDuzenlemeEkrani), findsOneWidget);

          // Konumumu kullan'a bas ve Kaydet
          await tester.tap(find.text('Konumumu kullan'));
          await tester.pumpAndSettle();

          await tester.tap(find.widgetWithText(ElevatedButton, 'Kaydet'));
          await tester.pumpAndSettle();

          // Ekran kapandı, ana sayfa yenilendi ve hava durumu çağrıldı
          expect(find.byType(TarlaKonumDuzenlemeEkrani), findsNothing);
          expect(weatherCalls, 1);
          expect(tarlaCalls, 2);
          expect(find.textContaining('24°C'), findsOneWidget);
        },
      );

      testWidgets('hava hatası tarla istatistiklerini gizlemez', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final tarlaRepo = FakeTarlaRepository(
          Future.value([_tarla('1', size: 40)]),
        );
        final weatherCompleter = Completer<WeatherSummary>();
        final weatherRepo = FakeWeatherRepository(weatherCompleter.future);

        await tester.pumpWidget(
          _wrap(tarlaRepo: tarlaRepo, weatherRepo: weatherRepo),
        );

        weatherCompleter.completeError('network');
        await tester.pump();
        await tester.pump();

        expect(find.textContaining('40'), findsOneWidget);
        expect(find.textContaining('Hava durumu alınamadı'), findsOneWidget);
      });

      testWidgets('hava hatasında retry çalışır', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        int weatherCallCount = 0;
        late final Completer<WeatherSummary> c1;
        c1 = Completer();

        final weatherRepo = _CountingWeatherRepo(() {
          weatherCallCount++;
          if (weatherCallCount == 1) return c1.future;
          return Future.value(_hava);
        });

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([_tarla('1')])),
            weatherRepo: weatherRepo,
          ),
        );

        c1.completeError('hata');
        await tester.pump();

        expect(find.textContaining('Tekrar'), findsOneWidget);
        await tester.tap(find.textContaining('Tekrar'));
        await tester.pumpAndSettle();

        expect(weatherCallCount, 2);
        expect(find.textContaining('27'), findsOneWidget);
      });
    });

    // ── Yaklaşan görevler ─────────────────────────────────────────────────
    group('yaklaşan işler', () {
      testWidgets(
        'Yaklaşan İşler başlığı görünür ve eski Yaklaşan Görevler görünmez',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            ),
          );
          await tester.pumpAndSettle();

          expect(find.text('Yaklaşan İşler'), findsOneWidget);
          expect(find.text('Yaklaşan Görevler'), findsNothing);
        },
      );

      testWidgets('planlanmış faaliyet gösterilir', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final tarlaRepo = FakeTarlaRepository(Future.value([_tarla('t1')]));
        final faaliyetRepo = FakeFaaliyetRepository(
          faaliyetlerFuture: Future.value([
            _planliF('f1', dueDate: DateTime(2027, 5, 1)),
          ]),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: tarlaRepo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            faaliyetRepo: faaliyetRepo,
          ),
        );
        await tester.pumpAndSettle();

        // Faaliyet türü "Sulama" görünmeli
        expect(find.text('Sulama'), findsAtLeastNWidgets(1));
      });

      testWidgets(
        'production sözleşmesinde getPlanliGorevler üzerinden gelen açık görev Yaklaşan İşler listesinde gösterilir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final faaliyetRepo = FakeFaaliyetRepository(
            // Production contract: getTumFaaliyetler yalnızca Activity döner
            faaliyetlerFuture: Future.value([
              Faaliyet(
                id: 'a1',
                tarlaId: 't1',
                type: 'Gübreleme',
                note: '',
                timestamp: DateTime(2026, 9, 1),
                dueDate: null,
                isCompleted: true,
              ),
            ]),
            // Production contract: getPlanliGorevler açık Task kayıtlarını döner
            planliGorevlerFuture: Future.value([
              Faaliyet(
                id: 't1',
                tarlaId: 't1',
                type: 'Sulama',
                note: '',
                timestamp: DateTime(2026, 9, 2),
                dueDate: DateTime(2027, 5, 1),
                isCompleted: false,
              ),
            ]),
          );

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([_tarla('t1')])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: faaliyetRepo,
            ),
          );
          await tester.pumpAndSettle();

          // Planlı görev olan Sulama Yaklaşan İşler listesinde gösterilmeli
          expect(find.text('Sulama'), findsAtLeastNWidgets(1));
          expect(faaliyetRepo.getPlanliGorevlerCallCount, greaterThanOrEqualTo(1));
        },
      );

      testWidgets(
        'FarmSummaryRepository sağlandığında tek getFarmSummary çağrısı ile tarlalar ve yaklaşan işler yüklenir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final summary = FarmSummaryResponse(
            farms: [
              FarmWorkSummary(
                tarla: _tarla('t1'),
                nextTask: null,
                lastActivity: null,
              ),
            ],
            upcomingTasks: [
              Faaliyet(
                id: 't1',
                tarlaId: 't1',
                type: 'İlaçlama',
                note: 'Böcek ilacı',
                timestamp: DateTime(2026, 9, 2),
                dueDate: DateTime(2027, 5, 1),
                isCompleted: false,
              ),
            ],
          );

          final summaryRepo = FakeFarmSummaryTarlaRepository(summary);
          final faaliyetRepo = FakeFaaliyetRepository();

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: summaryRepo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: faaliyetRepo,
            ),
          );
          await tester.pumpAndSettle();

          expect(summaryRepo.getFarmSummaryCallCount, 1);
          expect(find.text('İlaçlama'), findsAtLeastNWidgets(1));
          expect(find.text('Tarla t1'), findsWidgets);
        },
      );

      testWidgets('tamamlanmış faaliyet yaklaşan görevlerde gösterilmez', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final faaliyetRepo = FakeFaaliyetRepository(
          faaliyetlerFuture: Future.value([_tamamlanmisF('f1')]),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([_tarla('t1')])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            faaliyetRepo: faaliyetRepo,
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.textContaining('Planlanmış işin yok'),
          findsOneWidget,
        );
        expect(find.text('Gübreleme'), findsNothing);
      });

      testWidgets(
        'iş yokken boş durum ve İş Planla butonu görünür',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          expect(
            find.textContaining('Planlanmış işin yok'),
            findsOneWidget,
          );
          expect(find.text('İş Planla'), findsOneWidget);
          expect(find.text('Faaliyet Planla'), findsNothing);
        },
      );

      testWidgets('planlanmış faaliyet schedule ikonu içerir', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final faaliyetRepo = FakeFaaliyetRepository(
          faaliyetlerFuture: Future.value([
            _planliF('f1', dueDate: DateTime(2027, 5, 1)),
          ]),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([_tarla('t1')])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            faaliyetRepo: faaliyetRepo,
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byIcon(Icons.schedule), findsAtLeastNWidgets(1));
      });
    });

    // ── Hızlı işlemler ────────────────────────────────────────────────────
    group('hızlı işlemler', () {
      testWidgets('Tarla Ekle butonu TarlaEklemeEkrani açar', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tarla Ekle').first);
        await tester.pumpAndSettle();

        expect(find.byType(TarlaEklemeEkrani), findsOneWidget);
      });

      testWidgets('tarla yokken İş Ekle SnackBar gösterir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle(); // tarla listesi yüklensin

        await tester.tap(find.text('İş Ekle').first);
        await tester.pump();

        expect(find.textContaining('önce en az bir tarla'), findsOneWidget);
      });

      testWidgets(
        '1 tarla varken İş Ekle doğrudan FaaliyetEklemeEkrani açar',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaRepo = FakeTarlaRepository(Future.value([_tarla('t1')]));

          final faaliyetRepo = FakeFaaliyetRepository();

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: tarlaRepo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: faaliyetRepo,
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.text('İş Ekle').first);
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
          final ekran = tester.widget<FaaliyetEklemeEkrani>(
            find.byType(FaaliyetEklemeEkrani),
          );
          expect(ekran.tarlaId, 't1');
          expect(ekran.initialIsCompleted, isTrue);
          expect(identical(ekran.repositoryForTesting, faaliyetRepo), isTrue);
        },
      );

      testWidgets('2+ tarla varken İş Ekle seçim ekranı açar', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final tarlaRepo = FakeTarlaRepository(
          Future.value([
            _tarla('t1', name: 'Kuzey Tarla'),
            _tarla('t2', name: 'Güney Tarla'),
          ]),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: tarlaRepo,
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('İş Ekle').first);
        await tester.pumpAndSettle();

        // Bottom sheet içinde tarla isimleri görünmeli
        expect(find.text('Hangi tarla için?'), findsOneWidget);
        expect(
          find.descendant(
            of: find.byType(BottomSheet),
            matching: find.text('Kuzey Tarla'),
          ),
          findsOneWidget,
        );
        expect(
          find.descendant(
            of: find.byType(BottomSheet),
            matching: find.text('Güney Tarla'),
          ),
          findsOneWidget,
        );
      });

      testWidgets('Tarlalarım callback çağrılır', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        bool callbackCalled = false;

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            onTarlalarimSekme: () => callbackCalled = true,
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tarlalarım'));
        await tester.pump();

        expect(callbackCalled, isTrue);
      });

      testWidgets('İş Planım callback çağrılır', (tester) async {
        tester.view.physicalSize = const Size(800, 1600);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        bool callbackCalled = false;

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository(Future.value([])),
            weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            onGunlukSekme: () => callbackCalled = true,
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('İş Planım'));
        await tester.pump();

        expect(callbackCalled, isTrue);
      });

      testWidgets(
        'callback verilmezse Tarlalarım TarlaListesiEkrani\'na gider',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.text('Tarlalarım'));
          await tester.pumpAndSettle();

          expect(find.byType(TarlaListesiEkrani), findsOneWidget);
        },
      );

      testWidgets(
        'Hızlı işlemlerde İş Ekle ve İş Planım görünür, eski buton metinleri görünmez',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
            ),
          );
          await tester.pumpAndSettle();

          expect(find.text('İş Ekle'), findsOneWidget);
          expect(find.text('İş Planım'), findsOneWidget);
          expect(find.text('İşlem Kaydı Ekle'), findsNothing);
          expect(find.text('Tüm Faaliyetler'), findsNothing);
        },
      );

      testWidgets(
        '2+ tarla varken tarla seçildiğinde doğru tarlaId ile açılır ve Planla moduna geçilebilir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaRepo = FakeTarlaRepository(
            Future.value([
              _tarla('t1', name: 'Kuzey Tarla'),
              _tarla('t2', name: 'Güney Tarla'),
            ]),
          );

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: tarlaRepo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.text('İş Ekle').first);
          await tester.pumpAndSettle();

          // Güney Tarla'yı seç
          await tester.tap(find.text('Güney Tarla'));
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
          final ekran = tester.widget<FaaliyetEklemeEkrani>(
            find.byType(FaaliyetEklemeEkrani),
          );
          expect(ekran.tarlaId, 't2');
          expect(ekran.initialIsCompleted, isTrue);

          // Kullanıcı Planla segmentine geçebilir
          await tester.tap(find.text('Planla'));
          await tester.pumpAndSettle();
          expect(find.text('İşi Planla'), findsOneWidget);
        },
      );

      testWidgets(
        'boş durumdaki İş Planla butonu FaaliyetEklemeEkrani\'nı doğrudan Planla modunda açar',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository(Future.value([_tarla('t1')])),
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.text('İş Planla'));
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
          final ekran = tester.widget<FaaliyetEklemeEkrani>(
            find.byType(FaaliyetEklemeEkrani),
          );
          expect(ekran.tarlaId, 't1');
          expect(ekran.initialIsCompleted, isFalse);
          expect(find.text('İşi Planla'), findsOneWidget);
        },
      );

      testWidgets(
        'İş Ekle ekranından true ile dönüldüğünde Ana Sayfa görev verilerini yeniler',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaRepo = FakeTarlaRepository(Future.value([_tarla('t1')]));
          final faaliyetRepo = FakeFaaliyetRepository();

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: tarlaRepo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: faaliyetRepo,
            ),
          );
          await tester.pumpAndSettle();

          expect(faaliyetRepo.getTumFaaliyetlerCallCount, 1);

          await tester.tap(find.text('İş Ekle').first);
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);

          // Pop with true
          final nav = tester.state<NavigatorState>(find.byType(Navigator).last);
          nav.pop(true);
          await tester.pumpAndSettle();

          expect(faaliyetRepo.getTumFaaliyetlerCallCount, 2);
        },
      );
    });

    // ── refreshNotifier ile yenileme ─────────────────────────────────────────
    group('refreshNotifier ile yenileme', () {
      testWidgets(
        'refreshNotifier değişince tarla sayısı ve alan güncellenir',
        (tester) async {
          final repo = _MutableFakeTarlaRepo([_tarla('t1', size: 10)]);
          final notifier = ValueNotifier<int>(0);
          addTearDown(notifier.dispose);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: repo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              refreshNotifier: notifier,
            ),
          );
          await tester.pumpAndSettle();

          // Başlangıç: 1 tarla, 10 dönüm
          expect(find.text('1'), findsOneWidget);
          expect(find.textContaining('10'), findsWidgets);

          // Repo güncellenir ve sinyal verilir
          repo.tarlalar = [_tarla('t1', size: 10), _tarla('t2', size: 25)];
          notifier.value++;
          await tester.pumpAndSettle();

          // Güncelleme: 2 tarla, 35 dönüm
          expect(find.text('2'), findsOneWidget);
          expect(find.textContaining('35'), findsOneWidget);
        },
      );

      testWidgets('refreshNotifier değişince hava durumu yeniden çağrılmaz', (
        tester,
      ) async {
        int weatherCallCount = 0;
        final weatherRepo = _CountingWeatherRepo(() {
          weatherCallCount++;
          return Future.value(_hava);
        });
        final repo = _MutableFakeTarlaRepo([_tarla('t1')]);
        final notifier = ValueNotifier<int>(0);
        addTearDown(notifier.dispose);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: repo,
            weatherRepo: weatherRepo,
            refreshNotifier: notifier,
          ),
        );
        await tester.pumpAndSettle();

        // Başlangıçta bir kez çağrılmış olmalı
        expect(weatherCallCount, 1);

        // Tarla verisi değişir ve sinyal verilir
        repo.tarlalar = [_tarla('t1'), _tarla('t2')];
        notifier.value++;
        await tester.pumpAndSettle();

        // Hava durumu yeniden çağrılmamış olmalı
        expect(weatherCallCount, 1);
      });
    });

    // ── Hava durumu tarla seçimi ve akışı (10 senaryo) ────────────────────
    group('hava durumu tarla seçimi ve akışı', () {
      testWidgets(
        'Senaryo 1: Tek uygun tarla - tarla adı görünür, dropdown oku ve seçim modalı görünmez',
        (tester) async {
          final repo = FakeTarlaRepository(
            Future.value([_tarla('1', name: 'Zeytinlik')]),
          );
          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            return const WeatherSummary(temperature: 22, description: 'Güneşli');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Tarla adı ve hava durumu görünür
          expect(find.text('Zeytinlik'), findsWidgets);
          expect(find.textContaining('22°C'), findsOneWidget);

          // Tek uygun tarla olduğu için dropdown oku görünmez
          expect(find.byIcon(Icons.arrow_drop_down), findsNothing);

          // Tıklanabilir seçim alanı olarak davranmaz
          await tester.tap(find.text('Zeytinlik').first);
          await tester.pumpAndSettle();
          expect(find.byType(BottomSheet), findsNothing);
          expect(weatherRepo.requestedFarmIds, ['1']);
        },
      );

      testWidgets(
        'Senaryo 1b: 1 koordinatlı + 1 koordinatsız tarla - tek uygun tarla olduğu için dropdown oku yine görünmez',
        (tester) async {
          final repo = FakeTarlaRepository(
            Future.value([
              _tarla('1', name: 'Zeytinlik'),
              Tarla(id: '2', name: 'Konumsuz', latitude: null, longitude: null, size: 5),
            ]),
          );
          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            return const WeatherSummary(temperature: 22, description: 'Güneşli');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          expect(find.text('Zeytinlik'), findsWidgets);
          expect(find.byIcon(Icons.arrow_drop_down), findsNothing);
        },
      );

      testWidgets(
        'Senaryo 2: İki uygun tarla - başlangıçta Tarla A seçilir, modal ile Tarla B seçilince güncellenir',
        (tester) async {
          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            if (farmId == 'a') {
              return const WeatherSummary(temperature: 18, description: 'Bulutlu');
            } else if (farmId == 'b') {
              return const WeatherSummary(temperature: 26, description: 'Açık');
            }
            return const WeatherSummary(temperature: 0, description: '');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Başlangıçta Tarla A seçili
          expect(find.text('Tarla A'), findsWidgets);
          expect(find.textContaining('18°C'), findsOneWidget);
          expect(find.byIcon(Icons.arrow_drop_down), findsOneWidget);
          expect(weatherRepo.requestedFarmIds, ['a']);

          // Dropdown'a tıkla ve bottom sheet'i aç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();

          expect(find.text('Hava durumu için tarla seçin'), findsOneWidget);
          expect(find.text('Tarla B'), findsWidgets);

          // Tarla B'yi seç
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pumpAndSettle();

          // Seçim güncellendi, Tarla B verisi ve adı görünüyor
          expect(weatherRepo.requestedFarmIds, ['a', 'b']);
          expect(find.text('Tarla B'), findsWidgets);
          expect(find.textContaining('26°C'), findsOneWidget);
        },
      );

      testWidgets(
        'Senaryo 3: Refresh seçimi korur - Tarla B seçildikten sonra refresh Tarla B olarak kalır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            if (farmId == 'b') {
              return const WeatherSummary(temperature: 26, description: 'Açık');
            }
            return const WeatherSummary(temperature: 18, description: 'Bulutlu');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Tarla B'yi seç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);

          // Refresh et (pull to refresh)
          await tester.fling(find.byType(SingleChildScrollView), const Offset(0, 300), 1000);
          await tester.pumpAndSettle();

          // Seçim Tarla A'ya zıplamaz, Tarla B olarak korunur
          expect(find.text('Tarla B'), findsWidgets);
          expect(weatherRepo.requestedFarmIds.last, 'b');
        },
      );

      testWidgets(
        'Senaryo 4: Seçilen tarla artık yoksa güvenli şekilde ilk uygun tarlaya fallback yapar',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = _MutableFakeTarlaRepo([tarlaA, tarlaB]);

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            if (farmId == 'b') {
              return const WeatherSummary(temperature: 26, description: 'Açık');
            }
            return const WeatherSummary(temperature: 18, description: 'Bulutlu');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Tarla B'yi seç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);

          // Tarla B listeden silindi
          repo.tarlalar = [tarlaA];

          // Refresh tetikle
          (tester.state(find.byType(AnaSayfaEkrani)) as dynamic).refresh();
          await tester.pumpAndSettle();

          // Güvenli şekilde Tarla A'ya fallback yaptı
          expect(find.text('Tarla A'), findsWidgets);
          expect(find.textContaining('18°C'), findsOneWidget);
        },
      );

      testWidgets(
        'Senaryo 5: Koordinatsız tarla - modal içinde disabled görünür ve seçilemez',
        (tester) async {
          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final tarlaC = Tarla(id: 'c', name: 'Tarla C (Konumsuz)', latitude: null, longitude: null, size: 20);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB, tarlaC]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            return const WeatherSummary(temperature: 20, description: 'Açık');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Modal aç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();

          // Tarla C 'Konum bilgisi yok' alt yazısıyla görünmeli
          expect(find.text('Tarla C (Konumsuz)'), findsOneWidget);
          expect(find.textContaining('Konum bilgisi yok'), findsOneWidget);

          // Tarla C'ye tıklandığında seçilemez olmalı (onTap null)
          await tester.tap(find.text('Tarla C (Konumsuz)'));
          await tester.pumpAndSettle();

          // Bottom sheet hala açık ve Tarla C için weather çağrılmadı
          expect(find.text('Hava durumu için tarla seçin'), findsOneWidget);
          expect(weatherRepo.requestedFarmIds.contains('c'), isFalse);

          // Tarla B seçilebilir
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);
          expect(weatherRepo.requestedFarmIds, ['a', 'b']);
        },
      );

      testWidgets(
        'Senaryo 6: Hiç koordinatlı tarla yok - weather API çağrılmaz, konum ekle gösterilir',
        (tester) async {
          final tarla1 = Tarla(id: '1', name: 'Tarla 1', latitude: null, longitude: null, size: 10);
          final tarla2 = Tarla(id: '2', name: 'Tarla 2', latitude: null, longitude: null, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarla1, tarla2]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            return const WeatherSummary(temperature: 20, description: 'Açık');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Weather API çağrılmamalıdır
          expect(weatherRepo.requestedFarmIds, isEmpty);

          // Konum ekleme yönlendirmesi görünmeli
          expect(find.text('Hava durumu için tarla konumu ekleyin'), findsOneWidget);
          expect(find.text('Konum ekle'), findsOneWidget);
        },
      );

      testWidgets(
        'Senaryo 7: Hiç tarla yok - weather API çağrılmaz, boş durum gösterilir',
        (tester) async {
          final repo = FakeTarlaRepository(Future.value([]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            return const WeatherSummary(temperature: 20, description: 'Açık');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Weather API çağrılmamalıdır
          expect(weatherRepo.requestedFarmIds, isEmpty);

          // Tarla ekleme yönlendirmesi görünmeli
          expect(find.text('Hava durumunu görmek için tarla ekleyin'), findsOneWidget);
          expect(find.text('Tarla Ekle'), findsWidgets);
        },
      );

      testWidgets(
        'Senaryo 8: Race condition - geç gelen Tarla A yanıtı hızlı Tarla B yanıtını ezmez',
        (tester) async {
          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB]));

          final completerA = Completer<WeatherSummary>();
          final completerB = Completer<WeatherSummary>();
          completerA.future.ignore();
          completerB.future.ignore();

          final weatherRepo = _RecordingWeatherRepo((farmId) {
            if (farmId == 'a') {
              return completerA.future;
            } else {
              return completerB.future;
            }
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pump(); // Tarla A isteği başladı, completerA bekliyor

          expect(find.text('Tarla A'), findsWidgets);
          expect(find.byIcon(Icons.arrow_drop_down), findsOneWidget);

          // Kullanıcı Tarla B'yi seçer
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pump();
          await tester.pump(const Duration(milliseconds: 300));
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pump(); // Tarla B isteği başladı

          // Hızlı gelen Tarla B yanıtı tamamlanır
          completerB.complete(const WeatherSummary(temperature: 28, description: 'B Sıcak'));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);
          expect(find.textContaining('28°C'), findsOneWidget);

          // Yavaş gelen eski Tarla A yanıtı tamamlanır
          completerA.complete(const WeatherSummary(temperature: 12, description: 'A Soğuk'));
          await tester.pumpAndSettle();

          // UI Tarla B'de kalmalıdır, Tarla A yanıtı ezmemelidir
          expect(find.text('Tarla B'), findsWidgets);
          expect(find.textContaining('28°C'), findsOneWidget);
          expect(find.textContaining('12°C'), findsNothing);
        },
      );

      testWidgets(
        'Senaryo 9: Tarla değişiminde stale veri karışmaması - Tarla A verisi Tarla B adıyla görünmez',
        (tester) async {
          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB]));

          final completerB = Completer<WeatherSummary>();
          completerB.future.ignore();

          final weatherRepo = _RecordingWeatherRepo((farmId) {
            if (farmId == 'a') {
              return Future.value(const WeatherSummary(temperature: 16, description: 'Yağmurlu'));
            } else {
              return completerB.future;
            }
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Tarla A verisi 16°C
          expect(find.text('Tarla A'), findsWidgets);
          expect(find.textContaining('16°C'), findsOneWidget);

          // Tarla B'yi seç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pump(); // transition/loading anı

          // Bu geçiş anında Tarla A'nın sıcaklığı görünmemeli, Tarla B adı ve loading görünmeli
          expect(find.textContaining('16°C'), findsNothing);
          expect(find.text('Tarla B'), findsWidgets);
          expect(find.text('Hava durumu yükleniyor…'), findsOneWidget);

          // Tarla B yanıtı gelince
          completerB.complete(const WeatherSummary(temperature: 24, description: 'Güneşli'));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);
          expect(find.textContaining('24°C'), findsOneWidget);
        },
      );

      testWidgets(
        'Senaryo 10: Risk regresyonu - Seçilen tarlada kritik risk WeatherCard içinde gösterilir',
        (tester) async {
          final tarlaA = Tarla(id: 'a', name: 'Tarla A', latitude: 38.4, longitude: 27.1, size: 10);
          final tarlaB = Tarla(id: 'b', name: 'Tarla B', latitude: 39.1, longitude: 28.2, size: 15);
          final repo = FakeTarlaRepository(Future.value([tarlaA, tarlaB]));

          final weatherRepo = _RecordingWeatherRepo((farmId) async {
            if (farmId == 'b') {
              return WeatherSummary(
                temperature: 2,
                description: 'Don Bekleniyor',
                risks: [
                  WeatherRisk(
                    riskType: 'FROST',
                    severity: 'CRITICAL',
                    startsAt: DateTime(2026, 9, 5, 3),
                    endsAt: DateTime(2026, 9, 5, 6),
                    message: 'Önümüzdeki saatlerde şiddetli don riski var.',
                    suggestedAction: 'Hassas fideleri örtün ve sulama yapın.',
                  ),
                ],
              );
            }
            return const WeatherSummary(temperature: 20, description: 'Açık');
          });

          await tester.pumpWidget(_wrap(tarlaRepo: repo, weatherRepo: weatherRepo));
          await tester.pumpAndSettle();

          // Tarla B'yi seç
          await tester.tap(find.byIcon(Icons.arrow_drop_down));
          await tester.pumpAndSettle();
          await tester.tap(find.descendant(of: find.byType(BottomSheet), matching: find.text('Tarla B')));
          await tester.pumpAndSettle();

          expect(find.text('Tarla B'), findsWidgets);
          expect(find.text('Önümüzdeki saatlerde şiddetli don riski var.'), findsOneWidget);
          expect(find.textContaining('Hassas fideleri örtün'), findsOneWidget);
          expect(find.textContaining('Kritik'), findsOneWidget);
        },
      );
    });
  });
}

// ---------------------------------------------------------------------------
// Helper fakes with callbacks
// ---------------------------------------------------------------------------

class _CountingTarlaRepo implements TarlaRepository {
  _CountingTarlaRepo(this._fn);
  final Future<List<Tarla>> Function() _fn;

  @override
  Future<List<Tarla>> getTarlalar() => _fn();

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class _CountingTarlaLocationRepo
    implements TarlaRepository, TarlaLocationRepository {
  _CountingTarlaLocationRepo(this._fn);
  final Future<List<Tarla>> Function() _fn;
  TarlaLocation? updatedLocation;

  @override
  Future<List<Tarla>> getTarlalar() async {
    final list = await _fn();
    if (updatedLocation != null) {
      return list
          .map(
            (t) => Tarla(
              id: t.id,
              name: t.name,
              latitude: updatedLocation!.latitude,
              longitude: updatedLocation!.longitude,
              size: t.size,
              cropType: t.cropType,
              plantingDate: t.plantingDate,
            ),
          )
          .toList();
    }
    return list;
  }

  @override
  Future<void> addTarla(Tarla tarla) async {}

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) async {
    updatedLocation = location;
  }
}

class _CountingWeatherRepo implements WeatherRepository {
  _CountingWeatherRepo(this._fn);
  final Future<WeatherSummary> Function() _fn;

  @override
  Future<WeatherSummary> getWeather({String? farmId}) => _fn();
}

class _RecordingWeatherRepo implements WeatherRepository {
  _RecordingWeatherRepo(this._fn);
  final Future<WeatherSummary> Function(String? farmId) _fn;
  final List<String?> requestedFarmIds = [];

  @override
  Future<WeatherSummary> getWeather({String? farmId}) {
    requestedFarmIds.add(farmId);
    return _fn(farmId);
  }
}

/// Her `getTarlalar` çağrısında güncel `tarlalar` listesini döndürür.
class _MutableFakeTarlaRepo implements TarlaRepository {
  List<Tarla> tarlalar;
  _MutableFakeTarlaRepo(this.tarlalar);

  @override
  Future<List<Tarla>> getTarlalar() async => List.unmodifiable(tarlalar);

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

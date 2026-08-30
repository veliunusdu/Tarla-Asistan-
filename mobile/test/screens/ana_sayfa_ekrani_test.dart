import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
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
  Future<WeatherSummary> getWeather() => _future;
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

class FakeFaaliyetRepository implements FaaliyetRepository {
  FakeFaaliyetRepository({Future<List<Faaliyet>>? faaliyetlerFuture})
    : _future = faaliyetlerFuture ?? Future.value([]);

  final Future<List<Faaliyet>> _future;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() => _future;
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
        final repo = FakeTarlaRepository(Future.value([]));
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
            if (weatherCalls == 1) {
              throw const WeatherLocationRequiredException();
            }
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

          expect(weatherCalls, 1);
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

          // Ekran kapandı, ana sayfa yenilendi
          expect(find.byType(TarlaKonumDuzenlemeEkrani), findsNothing);
          expect(weatherCalls, 2);
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
            tarlaRepo: FakeTarlaRepository(Future.value([])),
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
    group('yaklaşan görevler', () {
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
          find.textContaining('Henüz planlanmış faaliyet bulunmuyor'),
          findsOneWidget,
        );
        expect(find.text('Gübreleme'), findsNothing);
      });

      testWidgets(
        'faaliyet yokken boş durum ve Faaliyet Planla butonu görünür',
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
            find.textContaining('Henüz planlanmış faaliyet bulunmuyor'),
            findsOneWidget,
          );
          expect(find.text('Faaliyet Planla'), findsOneWidget);
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

      testWidgets('tarla yokken Faaliyet Ekle SnackBar gösterir', (
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

        await tester.tap(find.text('Faaliyet Ekle').first);
        await tester.pump();

        expect(find.textContaining('önce en az bir tarla'), findsOneWidget);
      });

      testWidgets(
        '1 tarla varken Faaliyet Ekle doğrudan FaaliyetEklemeEkrani açar',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1600);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final tarlaRepo = FakeTarlaRepository(Future.value([_tarla('t1')]));

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: tarlaRepo,
              weatherRepo: FakeWeatherRepository(Future.value(_hava)),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.text('Faaliyet Ekle').first);
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
        },
      );

      testWidgets('2+ tarla varken Faaliyet Ekle seçim ekranı açar', (
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

        await tester.tap(find.text('Faaliyet Ekle').first);
        await tester.pumpAndSettle();

        // Bottom sheet içinde tarla isimleri görünmeli
        expect(find.text('Kuzey Tarla'), findsOneWidget);
        expect(find.text('Güney Tarla'), findsOneWidget);
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

      testWidgets('Tüm Faaliyetler callback çağrılır', (tester) async {
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

        await tester.tap(find.text('Tüm Faaliyetler'));
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

  @override
  Future<List<Tarla>> getTarlalar() => _fn();

  @override
  Future<void> addTarla(Tarla tarla) async {}

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) async {}
}

class _CountingWeatherRepo implements WeatherRepository {
  _CountingWeatherRepo(this._fn);
  final Future<WeatherSummary> Function() _fn;

  @override
  Future<WeatherSummary> getWeather() => _fn();
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

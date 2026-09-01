import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeFaaliyetRepository
    implements FaaliyetRepository, FaaliyetDeleteRepository {
  FakeFaaliyetRepository(this._future);

  final Future<List<Faaliyet>> _future;
  final List<String> deletedIds = [];

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) => _future;

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async => deletedIds.add(id);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Tarla _tarla({double lat = 0.0, double lng = 0.0}) => Tarla(
  id: 't1',
  name: 'Büyük Tarla',
  latitude: lat,
  longitude: lng,
  size: 42.5,
  cropType: 'Buğday',
  plantingDate: DateTime(2024, 3, 15),
);

Faaliyet _yapilacak() => Faaliyet(
  id: 'f1',
  tarlaId: 't1',
  type: 'Sulama',
  note: 'Sabah sulanacak',
  timestamp: DateTime(2024, 4, 1),
  dueDate: DateTime(2024, 4, 10),
  isCompleted: false,
);

Faaliyet _tamamlanan() => Faaliyet(
  id: 'f2',
  tarlaId: 't1',
  type: 'Gübreleme',
  note: '',
  timestamp: DateTime(2024, 3, 20),
  isCompleted: true,
);

Widget _wrap(Widget child) => MaterialApp(theme: AppTheme.light, home: child);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('TarlaDetayEkrani', () {
    testWidgets('tarla bilgileri gösterilir', (tester) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );

      expect(find.text('Büyük Tarla'), findsWidgets);
      expect(find.textContaining('Buğday'), findsOneWidget);
      expect(find.textContaining('42.5'), findsOneWidget);
      expect(find.textContaining('2024'), findsAtLeastNWidgets(1));
    });

    testWidgets('arşivleme kullanıcı onayından sonra çalışır', (tester) async {
      var archived = false;
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: _tarla(),
            faaliyetRepository: FakeFaaliyetRepository(Future.value([])),
            onArchive: () async => archived = true,
          ),
        ),
      );

      await tester.tap(find.byTooltip('Tarlayı arşivle'));
      await tester.pumpAndSettle();
      expect(find.text('Tarlayı arşivle?'), findsOneWidget);
      expect(archived, isFalse);
      await tester.tap(find.text('Arşivle'));
      await tester.pumpAndSettle();
      expect(archived, isTrue);
    });

    testWidgets('arşivleme hatasında ekran açık kalır ve hata gösterir', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: _tarla(),
            faaliyetRepository: FakeFaaliyetRepository(Future.value([])),
            onArchive: () async => throw Exception('network'),
          ),
        ),
      );

      await tester.tap(find.byTooltip('Tarlayı arşivle'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Arşivle'));
      await tester.pumpAndSettle();

      expect(find.byType(TarlaDetayEkrani), findsOneWidget);
      expect(
        find.text('Tarla arşivlenemedi. Lütfen tekrar deneyin.'),
        findsOneWidget,
      );
    });

    testWidgets('faaliyet silme repository üzerinden çalışır', (tester) async {
      final repository = FakeFaaliyetRepository(Future.value([_tamamlanan()]));
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repository),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Geçmiş'));
      await tester.pumpAndSettle();

      await tester.tap(find.byTooltip('Faaliyeti sil'));
      await tester.pumpAndSettle();

      expect(repository.deletedIds, ['f2']);
    });

    testWidgets('düzenleme işlemini kullanıcıdan başlatır', (tester) async {
      var opened = false;
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: _tarla(),
            faaliyetRepository: FakeFaaliyetRepository(Future.value([])),
            onEdit: () async {
              opened = true;
              return false;
            },
          ),
        ),
      );

      await tester.tap(find.byTooltip('Tarlayı düzenle'));
      await tester.pumpAndSettle();

      expect(opened, isTrue);
    });

    testWidgets('konum 0.0 ise "Konum eklenmedi" gösterilir', (tester) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );

      expect(find.text('Konum eklenmedi'), findsOneWidget);
    });

    testWidgets('konum null ise "Konum eklenmedi" gösterilir', (tester) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      final tarla = Tarla(
        id: 't-null',
        name: 'Konum Yok Tarla',
        // latitude and longitude intentionally null
      );
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: tarla, faaliyetRepository: repo)),
      );

      expect(find.text('Konum eklenmedi'), findsOneWidget);
    });

    testWidgets('konum 0.0 değilse koordinatlar gösterilir', (tester) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: _tarla(lat: 39.12345, lng: 35.67890),
            faaliyetRepository: repo,
          ),
        ),
      );

      expect(find.textContaining('39.12345'), findsOneWidget);
    });

    testWidgets('yükleme sırasında AppLoadingView gösterilir', (tester) async {
      final completer = Completer<List<Faaliyet>>();
      final repo = FakeFaaliyetRepository(completer.future);
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      completer.complete([]);
    });

    testWidgets('hata durumunda AppErrorView gösterilir', (tester) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final completer = Completer<List<Faaliyet>>();
      final repo = FakeFaaliyetRepository(completer.future);
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );

      completer.completeError('db hatası');
      await tester.pump();

      expect(find.textContaining('Tekrar'), findsOneWidget);
    });

    testWidgets('"Tekrar Dene" tıklandığında liste yeniden yüklenir', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      int callCount = 0;
      late final Completer<List<Faaliyet>> c1;
      c1 = Completer();

      final repo = _CallCountingRepo(() {
        callCount++;
        if (callCount == 1) {
          return c1.future;
        }
        return Future.value([]);
      });

      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );

      c1.completeError('hata');
      await tester.pump();

      final retryBtn = find.textContaining('Tekrar');
      expect(retryBtn, findsOneWidget);
      await tester.tap(retryBtn);
      await tester.pumpAndSettle();

      expect(callCount, 2);
      expect(find.textContaining('Tekrar'), findsNothing);
    });

    testWidgets('planlı faaliyet Yapılacaklar sekmesinde gösterilir', (
      tester,
    ) async {
      final repo = FakeFaaliyetRepository(Future.value([_yapilacak()]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sulama'), findsOneWidget);
    });

    testWidgets('tamamlanan faaliyet Geçmiş sekmesinde gösterilir', (
      tester,
    ) async {
      final repo = FakeFaaliyetRepository(Future.value([_tamamlanan()]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );
      await tester.pumpAndSettle();

      // switch to Geçmiş tab
      await tester.tap(find.text('Geçmiş'));
      await tester.pumpAndSettle();

      expect(find.text('Gübreleme'), findsOneWidget);
    });

    testWidgets('boş Yapılacaklar sekmesinde AppEmptyView gösterilir', (
      tester,
    ) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );
      await tester.pumpAndSettle();

      expect(find.textContaining('Planlanmış faaliyet yok'), findsOneWidget);
    });

    testWidgets('boş Geçmiş sekmesinde AppEmptyView gösterilir', (
      tester,
    ) async {
      final repo = FakeFaaliyetRepository(Future.value([]));
      await tester.pumpWidget(
        _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Geçmiş'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Henüz geçmiş faaliyet yok'), findsOneWidget);
    });

    testWidgets(
      'FAB tıklandığında FaaliyetEklemeEkrani aynı faaliyetRepository ile açılır',
      (tester) async {
        final repo = FakeFaaliyetRepository(Future.value([]));
        await tester.pumpWidget(
          _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.byType(FloatingActionButton));
        await tester.pumpAndSettle();

        expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
        final eklenenEkran = tester.widget<FaaliyetEklemeEkrani>(
          find.byType(FaaliyetEklemeEkrani),
        );
        expect(identical(eklenenEkran.repositoryForTesting, repo), isTrue);
      },
    );
  });
}

// ---------------------------------------------------------------------------
// Helper fake that delegates to a callback so call-count can be tracked
// ---------------------------------------------------------------------------

class _CallCountingRepo implements FaaliyetRepository {
  _CallCountingRepo(this._fn);
  final Future<List<Faaliyet>> Function() _fn;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) => _fn();

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];
}

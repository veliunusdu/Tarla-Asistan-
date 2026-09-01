import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeFaaliyetRepository implements FaaliyetRepository {
  FakeFaaliyetRepository(this._future);

  final Future<List<Faaliyet>> _future;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) => _future;

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
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

    // ── Arşivleme ──────────────────────────────────────────────────────────
    group('arşivleme', () {
      testWidgets('onTarlaArsivle verilmezse arşivle butonu görünmez', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository(Future.value([]));
        await tester.pumpWidget(
          _wrap(TarlaDetayEkrani(tarla: _tarla(), faaliyetRepository: repo)),
        );
        await tester.pumpAndSettle();

        expect(find.byType(PopupMenuButton<String>), findsNothing);
      });

      testWidgets('onTarlaArsivle verilince PopupMenuButton görünür', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository(Future.value([]));
        await tester.pumpWidget(
          _wrap(
            TarlaDetayEkrani(
              tarla: _tarla(),
              faaliyetRepository: repo,
              onTarlaArsivle: () async {},
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byType(PopupMenuButton<String>), findsOneWidget);
      });

      testWidgets(
        'arşivleme onaylandığında callback çağrılır ve ekran kapanır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1200);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          bool callbackCalled = false;
          final repo = FakeFaaliyetRepository(Future.value([]));

          bool ekranAcik = true;
          await tester.pumpWidget(
            MaterialApp(
              home: Builder(
                builder: (ctx) => ElevatedButton(
                  onPressed: () async {
                    final result = await Navigator.push<bool>(
                      ctx,
                      MaterialPageRoute(
                        builder: (_) => TarlaDetayEkrani(
                          tarla: _tarla(),
                          faaliyetRepository: repo,
                          onTarlaArsivle: () async {
                            callbackCalled = true;
                          },
                        ),
                      ),
                    );
                    if (result == true) ekranAcik = false;
                  },
                  child: const Text('Aç'),
                ),
              ),
            ),
          );

          await tester.tap(find.text('Aç'));
          await tester.pumpAndSettle();

          // Menüyü aç
          await tester.tap(find.byType(PopupMenuButton<String>));
          await tester.pumpAndSettle();

          // Arşivle seçeneğine bas
          await tester.tap(find.text('Tarlayı Arşivle'));
          await tester.pumpAndSettle();

          // Onay diyaloğu — Arşivle butonuna bas
          await tester.tap(find.text('Arşivle'));
          await tester.pumpAndSettle();

          expect(callbackCalled, isTrue);
          expect(ekranAcik, isFalse);
        },
      );

      testWidgets(
        'arşivleme başarısız olunca hata snackbar gösterilir, ekran kapanmaz',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1200);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final repo = FakeFaaliyetRepository(Future.value([]));

          await tester.pumpWidget(
            MaterialApp(
              home: TarlaDetayEkrani(
                tarla: _tarla(),
                faaliyetRepository: repo,
                onTarlaArsivle: () async {
                  throw Exception('sunucu hatası');
                },
              ),
            ),
          );
          await tester.pumpAndSettle();

          // Menüyü aç
          await tester.tap(find.byType(PopupMenuButton<String>));
          await tester.pumpAndSettle();

          await tester.tap(find.text('Tarlayı Arşivle'));
          await tester.pumpAndSettle();

          // Onay diyaloğu
          await tester.tap(find.text('Arşivle'));
          await tester.pumpAndSettle();

          // Ekran hâlâ açık
          expect(find.byType(TarlaDetayEkrani), findsOneWidget);
          // Hata snackbar gösterildi
          expect(
            find.textContaining('arşivlenemedi'),
            findsOneWidget,
          );
        },
      );
    });
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

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

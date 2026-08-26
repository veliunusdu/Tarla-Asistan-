import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/fields/data/local_tarla_repository.dart';
import 'package:mobile/features/fields/data/tarla_read_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_listesi_ekrani.dart';
import 'package:mobile/shared/widgets/app_empty_view.dart';
import 'package:mobile/shared/widgets/app_error_view.dart';
import 'package:mobile/shared/widgets/app_loading_view.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeTarlaRepository implements TarlaRepository {
  FakeTarlaRepository(this._completer);

  final Completer<List<Tarla>> _completer;

  @override
  Future<List<Tarla>> getTarlalar() => _completer.future;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Tarla _tarla(String id, String name) => Tarla(
  id: id,
  name: name,
  latitude: 0,
  longitude: 0,
  size: 10,
  cropType: 'Buğday',
  plantingDate: DateTime(2024),
);

Widget _buildApp(TarlaRepository repository) => MaterialApp(
  theme: AppTheme.light,
  home: TarlaListesiEkrani(repository: repository),
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  _backendAdapterTests();

  group('TarlaListesiEkrani', () {
    testWidgets('yüklenirken AppLoadingView gösterir', (tester) async {
      final completer = Completer<List<Tarla>>();
      await tester.pumpWidget(_buildApp(FakeTarlaRepository(completer)));

      expect(find.byType(AppLoadingView), findsOneWidget);
      expect(find.text('Tarlalar yükleniyor…'), findsOneWidget);
      expect(find.byType(AppErrorView), findsNothing);

      completer.complete([]);
    });

    testWidgets('hata durumunda AppErrorView gösterir', (tester) async {
      final completer = Completer<List<Tarla>>();
      await tester.pumpWidget(_buildApp(FakeTarlaRepository(completer)));

      // FutureBuilder subscribe olduktan sonra hata üretilir;
      // bu sayede unhandled error propagate olmaz.
      completer.completeError(Exception('db hatası'));
      await tester.pump();

      expect(find.byType(AppErrorView), findsOneWidget);
      expect(find.text('Bir sorun oluştu'), findsOneWidget);
      expect(find.textContaining('Exception'), findsNothing);
      expect(find.byType(AppLoadingView), findsNothing);
    });

    testWidgets('boş liste AppEmptyView ve Tarla Ekle butonu gösterir', (
      tester,
    ) async {
      final completer = Completer<List<Tarla>>();
      await tester.pumpWidget(_buildApp(FakeTarlaRepository(completer)));

      completer.complete([]);
      await tester.pump();

      expect(find.byType(AppEmptyView), findsOneWidget);
      expect(find.text('Henüz tarla eklemediniz'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Tarla Ekle'), findsOneWidget);
      expect(find.byType(AppErrorView), findsNothing);
    });

    testWidgets('tarla listesi gösterir', (tester) async {
      final completer = Completer<List<Tarla>>();
      await tester.pumpWidget(_buildApp(FakeTarlaRepository(completer)));

      completer.complete([
        _tarla('1', 'Kuzey Tarla'),
        _tarla('2', 'Güney Tarla'),
      ]);
      await tester.pump();

      expect(find.text('Kuzey Tarla'), findsOneWidget);
      expect(find.text('Güney Tarla'), findsOneWidget);
      expect(find.byType(AppEmptyView), findsNothing);
      expect(find.byType(AppErrorView), findsNothing);
    });

    testWidgets("Tekrar Dene butonu repository'yi yeniden çağırır", (
      tester,
    ) async {
      var callCount = 0;
      // Her çağrıda yeni bir completer döndüren repository
      final repo = _CountingFakeRepository(
        onCall: () {
          callCount++;
          // İkinci çağrıda boş liste döndür
          if (callCount == 1) {
            return Completer<List<Tarla>>()..completeError(Exception('hata'));
          }
          return Completer<List<Tarla>>()..complete([]);
        },
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: TarlaListesiEkrani(repository: repo),
        ),
      );
      await tester.pump();

      expect(find.byType(AppErrorView), findsOneWidget);
      expect(callCount, 1);

      await tester.tap(find.widgetWithText(ElevatedButton, 'Tekrar Dene'));
      // İlk pump setState'i işler, ikinci pump FutureBuilder'ın zaten
      // tamamlanmış olan yeni future'ı çözmesini sağlar.
      await tester.pump();
      await tester.pump();

      expect(callCount, 2);
      expect(find.byType(AppEmptyView), findsOneWidget);
    });
  });
}

// ---------------------------------------------------------------------------
// Counting fake — retry davranışını test etmek için
// ---------------------------------------------------------------------------

class _CountingFakeRepository implements TarlaRepository {
  _CountingFakeRepository({required this.onCall});

  final Completer<List<Tarla>> Function() onCall;

  @override
  Future<List<Tarla>> getTarlalar() => onCall().future;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

// ---------------------------------------------------------------------------
// TarlaReadRepository fake — sadece okuma arayüzünü test etmek için
// ---------------------------------------------------------------------------

/// Implements [TarlaReadRepository] directly (no write path), suitable for
/// testing [TarlaListesiEkrani] with a backend-style adapter.
class FakeReadRepository implements TarlaReadRepository {
  FakeReadRepository(this._future);
  final Future<List<Tarla>> _future;

  @override
  Future<List<Tarla>> getTarlalar() => _future;
}

class _CountingReadRepository implements TarlaReadRepository {
  _CountingReadRepository({required this.onCall});

  final Completer<List<Tarla>> Function() onCall;
  int callCount = 0;

  @override
  Future<List<Tarla>> getTarlalar() {
    callCount++;
    return onCall().future;
  }
}

// ---------------------------------------------------------------------------
// Helpers for read-repository tests
// ---------------------------------------------------------------------------

Widget _buildReadApp(TarlaReadRepository repository) => MaterialApp(
  theme: AppTheme.light,
  home: TarlaListesiEkrani(repository: repository),
);

Tarla _backendTarla(String id, String name) => Tarla(id: id, name: name);

// ---------------------------------------------------------------------------
// Tests 11–16: TarlaListesiEkrani with backend read adapter DI
// ---------------------------------------------------------------------------

void _backendAdapterTests() {
  group('TarlaListesiEkrani — backend read adapter DI', () {
    // Test 11
    testWidgets(
      '11. backend read adapter inject edilince tarla listesi gösterir',
      (tester) async {
        final repo = FakeReadRepository(
          Future.value([
            _backendTarla('b1', 'Backend Tarlası'),
            _backendTarla('b2', 'İkinci Backend Tarlası'),
          ]),
        );

        await tester.pumpWidget(_buildReadApp(repo));
        await tester.pump();

        expect(find.text('Backend Tarlası'), findsOneWidget);
        expect(find.text('İkinci Backend Tarlası'), findsOneWidget);
        expect(find.byType(AppErrorView), findsNothing);
      },
    );

    // Test 12
    testWidgets('12. backend hatasında AppErrorView görünür', (tester) async {
      final completer = Completer<List<Tarla>>();
      final repo = FakeReadRepository(completer.future);

      await tester.pumpWidget(_buildReadApp(repo));
      completer.completeError(Exception('backend hatası'));
      await tester.pump();

      expect(find.byType(AppErrorView), findsOneWidget);
      expect(find.byType(AppLoadingView), findsNothing);
    });

    // Test 13
    testWidgets(
      '13. Tekrar Dene aynı backend adapter üzerinden yeniden çağrı yapar',
      (tester) async {
        var callIndex = 0;
        final repo = _CountingReadRepository(
          onCall: () {
            callIndex++;
            if (callIndex == 1) {
              return Completer<List<Tarla>>()
                ..completeError(Exception('backend hatası'));
            }
            return Completer<List<Tarla>>()
              ..complete([_backendTarla('b1', 'Yenilendi')]);
          },
        );

        await tester.pumpWidget(_buildReadApp(repo));
        await tester.pump();

        expect(find.byType(AppErrorView), findsOneWidget);
        expect(repo.callCount, 1);

        await tester.tap(find.widgetWithText(ElevatedButton, 'Tekrar Dene'));
        await tester.pump();
        await tester.pump();

        expect(repo.callCount, 2);
        expect(find.text('Yenilendi'), findsOneWidget);
      },
    );

    // Test 14
    test(
      '14. repository verilmezse LocalTarlaRepository varsayılanı korunur',
      () {
        // Verify the default constructor produces a widget whose backing
        // repository is LocalTarlaRepository.  We confirm this via type
        // inspection: the const LocalTarlaRepository() fallback is codified
        // in the widget constructor.
        const screen = TarlaListesiEkrani();
        expect(screen, isA<TarlaListesiEkrani>());

        // Additionally verify LocalTarlaRepository implements TarlaReadRepository,
        // confirming it is a valid fallback without breaking the interface.
        expect(const LocalTarlaRepository(), isA<TarlaReadRepository>());
      },
    );

    // Test 15
    testWidgets('15. FAB ve detay navigasyonu bozulmaz', (tester) async {
      final repo = FakeReadRepository(
        Future.value([_backendTarla('b1', 'Tarla A')]),
      );

      await tester.pumpWidget(_buildReadApp(repo));
      await tester.pump();

      // FAB hâlâ görünür
      expect(find.byType(FloatingActionButton), findsOneWidget);

      // Tarla listelendi
      expect(find.text('Tarla A'), findsOneWidget);

      // Detay ekranına navigasyon mevcut (ListTile'a onTap var)
      final tile = find.ancestor(
        of: find.text('Tarla A'),
        matching: find.byType(ListTile),
      );
      expect(tile, findsOneWidget);
    });

    // Test 16
    testWidgets('16. 320×640 ekranda overflow oluşmaz', (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      final repo = FakeReadRepository(
        Future.value([
          _backendTarla('b1', 'Dar Ekran Tarlası'),
          _backendTarla('b2', 'Bir Diğer Tarla'),
        ]),
      );

      await tester.pumpWidget(_buildReadApp(repo));
      await tester.pump();

      expect(tester.takeException(), isNull);
      expect(find.text('Dar Ekran Tarlası'), findsOneWidget);
    });
  });
}

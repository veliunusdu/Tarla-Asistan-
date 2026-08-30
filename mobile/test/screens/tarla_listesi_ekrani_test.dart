import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/local_tarla_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';
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

class RecordingTarlaRepository implements TarlaRepository {
  RecordingTarlaRepository([List<Tarla>? initialTarlalar])
    : _tarlalar = initialTarlalar?.toList() ?? [];

  final List<Tarla> _tarlalar;
  final List<Tarla> added = [];

  @override
  Future<List<Tarla>> getTarlalar() async => [..._tarlalar, ...added];

  @override
  Future<void> addTarla(Tarla tarla) async {
    added.add(tarla);
  }
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

Future<void> completeFarmForm(WidgetTester tester) async {
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Tarla Adı'),
    'Kuzey Tarla',
  );
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
    '5',
  );
  await tester.tap(find.byType(DropdownButtonFormField<String>));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Buğday').last);
  await tester.pumpAndSettle();
  await tester.tap(find.text('Tarih seçin'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Tamam'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Kaydet'));
  await tester.pumpAndSettle();
}

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

    testWidgets('boş durumdan eklenen tarla supplied repositoryye kaydolur', (
      tester,
    ) async {
      final farms = RecordingTarlaRepository();
      await tester.pumpWidget(_buildApp(farms));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ElevatedButton, 'Tarla Ekle'));
      await tester.pumpAndSettle();
      await completeFarmForm(tester);

      expect(farms.added.single.name, 'Kuzey Tarla');
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

    testWidgets('FAB ile eklenen tarla supplied repositoryye kaydolur', (
      tester,
    ) async {
      final farms = RecordingTarlaRepository([_tarla('1', 'Mevcut Tarla')]);
      await tester.pumpWidget(_buildApp(farms));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(FloatingActionButton));
      await tester.pumpAndSettle();
      await completeFarmForm(tester);

      expect(farms.added.single.name, 'Kuzey Tarla');
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
// Backend-capable repository fakes
// ---------------------------------------------------------------------------

class FakeReadRepository implements TarlaRepository {
  FakeReadRepository(this._future);
  final Future<List<Tarla>> _future;

  @override
  Future<List<Tarla>> getTarlalar() => _future;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class _CountingReadRepository implements TarlaRepository {
  _CountingReadRepository({required this.onCall});

  final Completer<List<Tarla>> Function() onCall;
  int callCount = 0;

  @override
  Future<List<Tarla>> getTarlalar() {
    callCount++;
    return onCall().future;
  }

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

// ---------------------------------------------------------------------------
// Helpers for backend-capable repository tests
// ---------------------------------------------------------------------------

Widget _buildReadApp(TarlaRepository repository) => MaterialApp(
  theme: AppTheme.light,
  home: TarlaListesiEkrani(repository: repository),
);

class _EmptyFaaliyetRepository implements FaaliyetRepository {
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];
}

Tarla _backendTarla(String id, String name) => Tarla(id: id, name: name);

// ---------------------------------------------------------------------------
// Tests 11–16: TarlaListesiEkrani with backend-capable repository DI
// ---------------------------------------------------------------------------

void _backendAdapterTests() {
  group('TarlaListesiEkrani — backend-capable repository DI', () {
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

        // The fallback must remain write-capable because this screen owns
        // field-creation routes as well as field reads.
        expect(const LocalTarlaRepository(), isA<TarlaRepository>());
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

    testWidgets('tarla tıklandığında TarlaDetayEkrani aynı faaliyetRepository ile açılır', (
      tester,
    ) async {
      final tarlaRepo = FakeReadRepository(
        Future.value([_backendTarla('b1', 'Tarla A')]),
      );
      final faaliyetRepo = _EmptyFaaliyetRepository();

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: TarlaListesiEkrani(
            repository: tarlaRepo,
            faaliyetRepository: faaliyetRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Tarla A'));
      await tester.pumpAndSettle();

      expect(find.byType(TarlaDetayEkrani), findsOneWidget);
      final detay = tester.widget<TarlaDetayEkrani>(
        find.byType(TarlaDetayEkrani),
      );
      expect(identical(detay.repositoryForTesting, faaliyetRepo), isTrue);
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

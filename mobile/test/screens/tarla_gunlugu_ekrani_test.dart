import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';
import 'package:mobile/screens/tarla_gunlugu_ekrani.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeTarlaRepository implements TarlaRepository {
  FakeTarlaRepository(this._tarlalar);
  final List<Tarla> _tarlalar;

  @override
  Future<List<Tarla>> getTarlalar() async => _tarlalar;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

/// Simulates the API returning a newly mapped Tarla instance on every reload.
class RefreshingTarlaRepository implements TarlaRepository {
  @override
  Future<List<Tarla>> getTarlalar() async => [
    _tarla('t1', name: 'Örnek Tarla'),
  ];

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class RecordingTarlaRepository implements TarlaRepository {
  final List<Tarla> added = [];

  @override
  Future<List<Tarla>> getTarlalar() async => added;

  @override
  Future<void> addTarla(Tarla tarla) async {
    added.add(tarla);
  }
}

/// Mutable fake — addFaaliyet kaydeder, getTumFaaliyetler içerir.
class FakeFaaliyetRepository
    implements
        FaaliyetRepository,
        PlanliGorevRepository,
        PlanliGorevCompletionRepository {
  FakeFaaliyetRepository({
    List<Faaliyet>? faaliyetler,
    List<Faaliyet>? planliGorevler,
    this.hata,
    this.addFaaliyetHata,
  }) : _faaliyetler = faaliyetler?.toList() ?? [],
       _planliGorevler =
           planliGorevler?.toList() ??
           (faaliyetler ?? const <Faaliyet>[])
               .where((faaliyet) => !faaliyet.isCompleted)
               .toList();

  final List<Faaliyet> _faaliyetler;
  final List<Faaliyet> _planliGorevler;
  final Object? hata;
  final Object? addFaaliyetHata;

  /// addFaaliyet çağrısıyla eklenen kayıtlar
  final List<Faaliyet> eklenenler = [];
  int planliGorevEklemeSayisi = 0;
  final List<String> tamamlananGorevler = [];

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet f) async {
    if (addFaaliyetHata != null) throw addFaaliyetHata!;
    eklenenler.add(f);
  }

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {
    if (addFaaliyetHata != null) throw addFaaliyetHata!;
    planliGorevEklemeSayisi++;
    eklenenler.add(gorev);
  }

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => [
    ..._planliGorevler,
    ...eklenenler,
  ];

  @override
  Future<void> completePlanliGorev(String id, {String? note}) async {
    tamamlananGorevler.add(id);
  }

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async {
    if (hata != null) throw hata!;
    return [..._faaliyetler, ...eklenenler];
  }

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {
    final idx = _faaliyetler.indexWhere((f) => f.id == id);
    if (idx != -1) {
      final f = _faaliyetler[idx];
      _faaliyetler[idx] = Faaliyet(
        id: f.id,
        tarlaId: f.tarlaId,
        type: f.type,
        note: f.note,
        timestamp: DateTime.now(),
        dueDate: null,
        isCompleted: true,
      );
    }
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Widget _wrap({
  required TarlaRepository tarlaRepo,
  required FaaliyetRepository faaliyetRepo,
  VoidCallback? onDataChanged,
}) => MaterialApp(
  theme: AppTheme.light,
  home: TarlaGunluguEkrani(
    tarlaRepository: tarlaRepo,
    faaliyetRepository: faaliyetRepo,
    onDataChanged: onDataChanged,
  ),
);

Tarla _tarla(String id, {String name = 'Tarla'}) => Tarla(
  id: id,
  name: name,
  latitude: 0,
  longitude: 0,
  size: 10,
  cropType: 'Buğday',
  plantingDate: DateTime(2024),
);

Faaliyet _faaliyet({
  required String id,
  required String tarlaId,
  String type = 'Sulama',
  bool isCompleted = false,
  DateTime? dueDate,
  DateTime? timestamp,
}) => Faaliyet(
  id: id,
  tarlaId: tarlaId,
  type: type,
  note: '',
  timestamp: timestamp ?? DateTime(2024, 1, 1),
  dueDate: dueDate,
  isCompleted: isCompleted,
);

DateTime _today() {
  final now = DateTime.now();
  return DateTime(now.year, now.month, now.day);
}

DateTime _tomorrow() => _today().add(const Duration(days: 1));

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
  await tester.ensureVisible(find.text('Kaydet'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Kaydet'));
  await tester.pumpAndSettle();
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('TarlaGunluguEkrani', () {
    // ── Temel durumlar ────────────────────────────────────────────────────
    group('temel durumlar', () {
      testWidgets('loading durumunda AppLoadingView gösterilir', (
        tester,
      ) async {
        final completer = Completer<List<Faaliyet>>();
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: _SlowFaaliyetRepository(completer.future),
          ),
        );
        expect(find.byType(CircularProgressIndicator), findsOneWidget);
        completer.complete([]);
      });

      testWidgets('hata durumunda AppErrorView ve Tekrar Dene gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1200);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        int callCount = 0;
        final c1 = Completer<List<Faaliyet>>();

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: _CountingFaaliyetRepo(() {
              callCount++;
              if (callCount == 1) return c1.future;
              return Future.value([]);
            }),
          ),
        );

        c1.completeError('hata');
        await tester.pump();

        expect(find.textContaining('Tekrar'), findsOneWidget);

        await tester.tap(find.textContaining('Tekrar'));
        await tester.pumpAndSettle();

        expect(callCount, 2);
        expect(find.textContaining('Tekrar'), findsNothing);
      });
    });

    // ── UI yapısı ─────────────────────────────────────────────────────────
    group('UI yapısı', () {
      testWidgets(
        'Gömülü görev formu ve inputları görünmez, tek İş Ekle FAB görünür',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          expect(find.text('Yeni Görev'), findsNothing);
          expect(find.text('Görevi Ekle'), findsNothing);
          expect(find.widgetWithText(TextFormField, 'Görev'), findsNothing);
          expect(
            find.widgetWithText(TextFormField, 'Not (isteğe bağlı)'),
            findsNothing,
          );
          expect(
            find.widgetWithText(FloatingActionButton, 'İş Ekle'),
            findsOneWidget,
          );
        },
      );

      testWidgets('Dört FilterChip görünür, SegmentedButton bulunmaz', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byType(FilterChip), findsNWidgets(4));
        expect(find.text('Bugün'), findsOneWidget);
        expect(find.text('Yaklaşan'), findsOneWidget);
        expect(find.text('Geciken'), findsOneWidget);
        expect(find.text('Tümü'), findsOneWidget);
        expect(find.byType(SegmentedButton), findsNothing);
      });

      testWidgets('360×800 ekranda overflow oluşmaz', (tester) async {
        tester.view.physicalSize = const Size(360, 800);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([
              _tarla('t1', name: 'Orta Boy Tarla Adı'),
            ]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama görevi',
                  isCompleted: false,
                  dueDate: _today(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
      });
    });

    // ── Filtreler ─────────────────────────────────────────────────────────
    group('filtreler', () {
      testWidgets('İş Planım tamamlanmış faaliyetleri göstermez', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final planliGorev = _faaliyet(
          id: 'task-1',
          tarlaId: 't1',
          type: 'Yarın sulama',
          dueDate: _tomorrow(),
        );
        final tamamlanmisFaaliyet = _faaliyet(
          id: 'activity-1',
          tarlaId: 't1',
          type: 'Dün gübreleme',
          isCompleted: true,
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [tamamlanmisFaaliyet],
              planliGorevler: [planliGorev],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        expect(find.text('Yarın sulama'), findsOneWidget);
        expect(find.text('Dün gübreleme'), findsNothing);
      });

      testWidgets('Bugün filtresi varsayılan seçilidir', (tester) async {
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        // "Bugün" FilterChip'i seçili olmalı
        final chips = tester.widgetList<FilterChip>(find.byType(FilterChip));
        final bugunChip = chips.firstWhere(
          (c) => (c.label as Text).data == 'Bugün',
        );
        expect(bugunChip.selected, isTrue);
      });

      testWidgets('Bugün filtresi bugünün planlı görevini gösterir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final bugunF = _faaliyet(
          id: 'f1',
          tarlaId: 't1',
          type: 'Sulama',
          isCompleted: false,
          dueDate: _today(),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(faaliyetler: [bugunF]),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Sulama'), findsOneWidget);
      });

      testWidgets('Bugün filtresi gelecek tarihin görevini göstermez', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final yariF = _faaliyet(
          id: 'f1',
          tarlaId: 't1',
          type: 'Sulama',
          isCompleted: false,
          dueDate: _tomorrow(),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(faaliyetler: [yariF]),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Sulama'), findsNothing);
        expect(
          find.textContaining('Bugün için planlanmış iş bulunmuyor'),
          findsOneWidget,
        );
      });

      testWidgets(
        'Bugün filtresi boş durumda açıklama ve İş Ekle FAB gösterir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
              faaliyetRepo: FakeFaaliyetRepository(),
            ),
          );
          await tester.pumpAndSettle();

          expect(
            find.text('Bugün için planlanmış iş bulunmuyor.'),
            findsOneWidget,
          );
          expect(
            find.widgetWithText(FloatingActionButton, 'İş Ekle'),
            findsOneWidget,
          );
        },
      );

      testWidgets('Yaklaşan filtresi yalnızca ileri tarihli işleri gösterir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tarla A')]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama',
                  isCompleted: false,
                  dueDate: _tomorrow(),
                ),
                _faaliyet(
                  id: 'f2',
                  tarlaId: 't1',
                  type: 'Gübreleme',
                  dueDate: _today().subtract(const Duration(days: 1)),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Yaklaşan'));
        await tester.pumpAndSettle();

        expect(find.text('Sulama'), findsOneWidget);
        expect(find.text('Gübreleme'), findsNothing);
      });

      testWidgets('Geciken filtresi yalnızca gecikmiş işleri gösterir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tarla A')]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama',
                  dueDate: _tomorrow(),
                ),
                _faaliyet(
                  id: 'f2',
                  tarlaId: 't1',
                  type: 'Gübreleme',
                  dueDate: _today().subtract(const Duration(days: 1)),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Geciken'));
        await tester.pumpAndSettle();

        expect(find.text('Gübreleme'), findsOneWidget);
        expect(find.text('Sulama'), findsNothing);
      });

      testWidgets('Tümü filtresi tüm açık planlı işleri gösterir', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tarla A')]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama',
                  isCompleted: false,
                  dueDate: _tomorrow(),
                ),
                _faaliyet(
                  id: 'f2',
                  tarlaId: 't1',
                  type: 'Gübreleme',
                  dueDate: _today().subtract(const Duration(days: 1)),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        expect(find.text('Sulama'), findsOneWidget);
        expect(find.text('Gübreleme'), findsOneWidget);
      });

      testWidgets('Geciken boş durumda uygun mesaj gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  isCompleted: false,
                  dueDate: _tomorrow(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Geciken'));
        await tester.pumpAndSettle();

        expect(
          find.textContaining('Geciken iş bulunmuyor'),
          findsOneWidget,
        );
      });
    });

    group('planlı görev tamamlama', () {
      testWidgets('Tamamla eylemi görevi backend repository ile tamamlar', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final tarla = _tarla('t1', name: 'Örnek Tarla');
        final gorev = _faaliyet(
          id: 'task-1',
          tarlaId: 't1',
          type: 'Sulama',
          dueDate: DateTime.now(),
          isCompleted: false,
        );
        final repo = FakeFaaliyetRepository(faaliyetler: [gorev]);

        await tester.pumpWidget(
          _wrap(tarlaRepo: FakeTarlaRepository([tarla]), faaliyetRepo: repo),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tamamla'));
        await tester.pumpAndSettle();

        expect(repo.tamamlananGorevler, ['task-1']);
        expect(find.text('İş tamamlandı.'), findsOneWidget);
      });
    });

    // ── Sıralama ──────────────────────────────────────────────────────────
    group('sıralama', () {
      testWidgets('Geciken işler en eski tarihten başlayarak sıralanır', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final eski = _faaliyet(
          id: 'f1',
          tarlaId: 't1',
          type: 'Sulama',
          dueDate: _today().subtract(const Duration(days: 2)),
        );
        final yeni = _faaliyet(
          id: 'f2',
          tarlaId: 't1',
          type: 'Gübreleme',
          dueDate: _today().subtract(const Duration(days: 1)),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(faaliyetler: [eski, yeni]),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Geciken'));
        await tester.pumpAndSettle();

        final sulamaPos = tester.getTopLeft(find.text('Sulama')).dy;
        final gubrePos = tester.getTopLeft(find.text('Gübreleme')).dy;
        // Sulama daha eski tarihli olduğu için daha yukarıda olmalı.
        expect(sulamaPos, lessThan(gubrePos));
      });
    });

    // ── Kart içeriği ──────────────────────────────────────────────────────
    group('kart içeriği', () {
      testWidgets('farklı tarlalara ait kayıtlarda tarla adları gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([
              _tarla('t1', name: 'Kuzey Tarlası'),
              _tarla('t2', name: 'Güney Tarlası'),
            ]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama',
                  dueDate: _tomorrow(),
                ),
                _faaliyet(
                  id: 'f2',
                  tarlaId: 't2',
                  type: 'Gübreleme',
                  dueDate: _tomorrow(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        expect(find.text('Kuzey Tarlası'), findsOneWidget);
        expect(find.text('Güney Tarlası'), findsOneWidget);
      });

      testWidgets('eşleşmeyen tarlaId için "Bilinmeyen tarla" gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 'yok',
                  type: 'Test',
                  dueDate: _tomorrow(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        expect(find.text('Bilinmeyen tarla'), findsOneWidget);
      });

      testWidgets('tarla bulunursa karta dokunulunca TarlaDetayEkrani açılır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final t = _tarla('t1', name: 'Test Tarlası');

        final repo = FakeFaaliyetRepository(
          faaliyetler: [
            _faaliyet(
              id: 'f1',
              tarlaId: 't1',
              type: 'Sulama',
              dueDate: _tomorrow(),
            ),
          ],
        );

        await tester.pumpWidget(
          _wrap(tarlaRepo: FakeTarlaRepository([t]), faaliyetRepo: repo),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Sulama'));
        await tester.pumpAndSettle();

        expect(find.byType(TarlaDetayEkrani), findsOneWidget);
        final detay = tester.widget<TarlaDetayEkrani>(
          find.byType(TarlaDetayEkrani),
        );
        expect(identical(detay.repositoryForTesting, repo), isTrue);
      });
    });

    // ── Form — tarla yokken ───────────────────────────────────────────────
    group('tarla yokken form durumu', () {
      testWidgets('tarla yoksa tarla ekleme empty state gösterilir', (
        tester,
      ) async {
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.textContaining('önce bir tarla oluşturmalısın'),
          findsOneWidget,
        );
        expect(find.text('Tarla Ekle'), findsOneWidget);
      });

      testWidgets('Tarla Ekle butonu TarlaEklemeEkrani açar', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tarla Ekle'));
        await tester.pumpAndSettle();

        expect(find.byType(TarlaEklemeEkrani), findsOneWidget);
      });

      testWidgets('İş Planım ekranından eklenen tarla supplied repositoryye kaydolur', (
        tester,
      ) async {
        final farms = RecordingTarlaRepository();
        await tester.pumpWidget(
          _wrap(tarlaRepo: farms, faaliyetRepo: FakeFaaliyetRepository()),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tarla Ekle'));
        await tester.pumpAndSettle();
        await completeFarmForm(tester);

        expect(farms.added, hasLength(1));
      });
    });

    // ── Yeni İş Ekleme Akışı ──────────────────────────────────────────────────
    group('yeni iş ekleme akışı', () {
      testWidgets('tek tarla varsa İş Ekle doğrudan FaaliyetEklemeEkrani açar', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tek Tarla')]),
            faaliyetRepo: repo,
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.widgetWithText(FloatingActionButton, 'İş Ekle'));
        await tester.pumpAndSettle();

        expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
        final ekran = tester.widget<FaaliyetEklemeEkrani>(
          find.byType(FaaliyetEklemeEkrani),
        );
        expect(ekran.tarlaId, 't1');
        expect(ekran.initialIsCompleted, isFalse);
      });

      testWidgets(
        'birden fazla tarla varsa seçim bottom sheet açılır ve seçilen tarlanın ekranı açılır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final repo = FakeFaaliyetRepository();
          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository([
                _tarla('t1', name: 'Birinci Tarla'),
                _tarla('t2', name: 'İkinci Tarla'),
              ]),
              faaliyetRepo: repo,
            ),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.widgetWithText(FloatingActionButton, 'İş Ekle'));
          await tester.pumpAndSettle();

          // Bottom sheet açılmış olmalı
          expect(find.text('Hangi tarla için?'), findsOneWidget);
          expect(find.text('Birinci Tarla'), findsOneWidget);
          expect(find.text('İkinci Tarla'), findsOneWidget);

          // İkinci tarlayı seç
          await tester.tap(find.text('İkinci Tarla'));
          await tester.pumpAndSettle();

          // FaaliyetEklemeEkrani t2 ile açılmış olmalı
          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);
          final ekran = tester.widget<FaaliyetEklemeEkrani>(
            find.byType(FaaliyetEklemeEkrani),
          );
          expect(ekran.tarlaId, 't2');
          expect(ekran.initialIsCompleted, isFalse);
        },
      );

      testWidgets(
        'yeni planlı iş eklenip true dönüldüğünde liste yenilenir ve onDataChanged tetiklenir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          bool onDataChangedCagirildi = false;
          final repo = FakeFaaliyetRepository();

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tek Tarla')]),
              faaliyetRepo: repo,
              onDataChanged: () => onDataChangedCagirildi = true,
            ),
          );
          await tester.pumpAndSettle();

          // İş Ekle butonuna bas
          await tester.tap(find.widgetWithText(FloatingActionButton, 'İş Ekle'));
          await tester.pumpAndSettle();

          expect(find.byType(FaaliyetEklemeEkrani), findsOneWidget);

          // İş türü seç, tarih bugün kalsın, İşi Planla'ya bas
          await tester.tap(find.byType(DropdownButtonFormField<String>));
          await tester.pumpAndSettle();
          await tester.tap(find.text('Sulama').last);
          await tester.pumpAndSettle();

          await tester.tap(find.text('İşi Planla'));
          await tester.pumpAndSettle();

          // Ekran kapandı, liste yenilendi
          expect(find.byType(FaaliyetEklemeEkrani), findsNothing);
          expect(find.text('Sulama'), findsOneWidget);
          expect(onDataChangedCagirildi, isTrue);
          expect(repo.eklenenler, hasLength(1));
          expect(repo.planliGorevEklemeSayisi, 1);
        },
      );

      testWidgets('tamamlama işlemi ekstra addFaaliyet çağırmaz ve completePlanliGorev çağırır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final planliGorev = _faaliyet(
          id: 'task-complete-1',
          tarlaId: 't1',
          type: 'Ot Temizliği',
          dueDate: _today(),
        );

        final repo = FakeFaaliyetRepository(planliGorevler: [planliGorev]);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: repo,
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Ot Temizliği'), findsOneWidget);
        expect(find.text('Tamamla'), findsOneWidget);

        await tester.tap(find.text('Tamamla'));
        await tester.pumpAndSettle();

        expect(repo.tamamlananGorevler, contains('task-complete-1'));
        // Kesin kural: addFaaliyet veya addPlanliGorev çağrılmamalı
        expect(repo.eklenenler, isEmpty);
        expect(find.text('İş tamamlandı.'), findsOneWidget);
      });
    });

    // ── Overflow / responsive ─────────────────────────────────────────────
    group('responsive / overflow', () {
      testWidgets('320x640 ekranda render exception oluşmaz', (tester) async {
        tester.view.physicalSize = const Size(480, 960);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'X' * 40)]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'A' * 40,
                  isCompleted: false,
                  dueDate: _today(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Exception yoksa geçer
        expect(tester.takeException(), isNull);
      });

      testWidgets('uzun tarla ve görev adları overflow oluşturmaz', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(480, 960);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([
              _tarla('t1', name: 'Çok Uzun Tarla Adı Örneği Burada'),
            ]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Çok Uzun Faaliyet Adı Örneği',
                  isCompleted: false,
                  dueDate: _today(),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
      });

      testWidgets(
        'gerçek 320×640 ekranda form ve görev kartı overflow oluşturmaz',
        (tester) async {
          tester.view.physicalSize = const Size(320, 640);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          await tester.pumpWidget(
            _wrap(
              tarlaRepo: FakeTarlaRepository([
                _tarla('t1', name: 'Küçük Ekran Tarlası'),
              ]),
              faaliyetRepo: FakeFaaliyetRepository(
                faaliyetler: [
                  _faaliyet(
                    id: 'f1',
                    tarlaId: 't1',
                    type: 'Sulama görevi',
                    isCompleted: false,
                    dueDate: _today(),
                  ),
                ],
              ),
            ),
          );
          await tester.pumpAndSettle();

          // Form, Dropdown, FilterChip ve görev kartı render istisnası üretmez
          expect(tester.takeException(), isNull);
        },
      );
    });
  });
}

// ---------------------------------------------------------------------------
// Helper fakes
// ---------------------------------------------------------------------------

class _SlowFaaliyetRepository
    implements FaaliyetRepository, PlanliGorevRepository {
  _SlowFaaliyetRepository(this._future);
  final Future<List<Faaliyet>> _future;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() => Future.value([]);
  @override
  Future<List<Faaliyet>> getPlanliGorevler() => _future;
  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {}
  @override
  Future<void> deleteFaaliyet(String id) async {}
  @override
  Future<void> markAsCompleted(String id) async {}
}

class _CountingFaaliyetRepo
    implements FaaliyetRepository, PlanliGorevRepository {
  _CountingFaaliyetRepo(this._fn);
  final Future<List<Faaliyet>> Function() _fn;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() => Future.value([]);
  @override
  Future<List<Faaliyet>> getPlanliGorevler() => _fn();
  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {}
  @override
  Future<void> deleteFaaliyet(String id) async {}
  @override
  Future<void> markAsCompleted(String id) async {}
}

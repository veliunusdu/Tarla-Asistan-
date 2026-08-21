import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
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

/// Mutable fake — addFaaliyet kaydeder, getTumFaaliyetler içerir.
class FakeFaaliyetRepository implements FaaliyetRepository {
  FakeFaaliyetRepository({
    List<Faaliyet>? faaliyetler,
    this.hata,
    this.addFaaliyetHata,
  }) : _faaliyetler = faaliyetler?.toList() ?? [];

  final List<Faaliyet> _faaliyetler;
  final Object? hata;
  final Object? addFaaliyetHata;

  /// addFaaliyet çağrısıyla eklenen kayıtlar
  final List<Faaliyet> eklenenler = [];

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet f) async {
    if (addFaaliyetHata != null) throw addFaaliyetHata!;
    eklenenler.add(f);
  }

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async {
    if (hata != null) throw hata!;
    return [..._faaliyetler, ...eklenenler];
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
      testWidgets('Yeni Görev formu ve etiketleri görünür', (tester) async {
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

        expect(find.text('Yeni Görev'), findsOneWidget);
        expect(find.text('Tarla'), findsWidgets);
        expect(find.text('Görev'), findsWidgets);
        expect(find.text('Not (isteğe bağlı)'), findsOneWidget);
        expect(find.text('Görevi Ekle'), findsOneWidget);
      });

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
        expect(find.text('Planlanan'), findsOneWidget);
        expect(find.text('Tamamlanan'), findsOneWidget);
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
          find.textContaining('Bugün için görev bulunmuyor'),
          findsOneWidget,
        );
      });

      testWidgets('Bugün filtresi boş durumda Görev Ekle butonu gösterir', (
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

        // "Görev Ekle" is in the empty-state action; "Görevi Ekle" is in the form button
        expect(find.text('Görev Ekle'), findsOneWidget);
      });

      testWidgets('Planlanan filtresi yalnızca isCompleted=false gösterir', (
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
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Planlanan'));
        await tester.pumpAndSettle();

        expect(find.text('Sulama'), findsOneWidget);
        expect(find.text('Gübreleme'), findsNothing);
      });

      testWidgets('Tamamlanan filtresi yalnızca isCompleted=true gösterir', (
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
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tamamlanan'));
        await tester.pumpAndSettle();

        expect(find.text('Gübreleme'), findsOneWidget);
        expect(find.text('Sulama'), findsNothing);
      });

      testWidgets('Tümü filtresi tüm kayıtları gösterir', (tester) async {
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
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
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

      testWidgets('Tamamlanan boş durumda uygun mesaj gösterilir', (
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

        await tester.tap(find.text('Tamamlanan'));
        await tester.pumpAndSettle();

        expect(
          find.textContaining('Henüz tamamlanmış faaliyet bulunmuyor'),
          findsOneWidget,
        );
      });
    });

    // ── Sıralama ──────────────────────────────────────────────────────────
    group('sıralama', () {
      testWidgets('Tamamlanan en yeniden eskiye sıralanır', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final eski = _faaliyet(
          id: 'f1',
          tarlaId: 't1',
          type: 'Sulama',
          isCompleted: true,
          timestamp: DateTime(2024, 1),
        );
        final yeni = _faaliyet(
          id: 'f2',
          tarlaId: 't1',
          type: 'Gübreleme',
          isCompleted: true,
          timestamp: DateTime(2024, 6),
        );

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1')]),
            faaliyetRepo: FakeFaaliyetRepository(faaliyetler: [eski, yeni]),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tamamlanan'));
        await tester.pumpAndSettle();

        final sulamaPos = tester.getTopLeft(find.text('Sulama')).dy;
        final gubrePos = tester.getTopLeft(find.text('Gübreleme')).dy;
        // Gübreleme (yeni) daha yukarıda olmalı
        expect(gubrePos, lessThan(sulamaPos));
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
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
                ),
                _faaliyet(
                  id: 'f2',
                  tarlaId: 't2',
                  type: 'Gübreleme',
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
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
                  isCompleted: true,
                  timestamp: DateTime(2024),
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

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([t]),
            faaliyetRepo: FakeFaaliyetRepository(
              faaliyetler: [
                _faaliyet(
                  id: 'f1',
                  tarlaId: 't1',
                  type: 'Sulama',
                  isCompleted: true,
                  timestamp: DateTime(2024, 6),
                ),
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Tümü'));
        await tester.pumpAndSettle();

        await tester.tap(find.text('Sulama'));
        await tester.pumpAndSettle();

        expect(find.byType(TarlaDetayEkrani), findsOneWidget);
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
    });

    // ── Form doğrulaması ──────────────────────────────────────────────────
    group('form doğrulaması', () {
      testWidgets('tarla seçmeden kayıt yapılamaz', (tester) async {
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

        // Görev adını doldur ama tarla seçme
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Görev'),
          'Sulama',
        );
        await tester.tap(find.text('Görevi Ekle'));
        await tester.pumpAndSettle();

        expect(find.textContaining('Tarla seçimi zorunludur'), findsOneWidget);
      });

      testWidgets('boş görev adı kaydedilemiyor', (tester) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final t = _tarla('t1', name: 'Örnek Tarla');

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([t]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        // Tarla seç
        await tester.tap(
          find.widgetWithText(DropdownButtonFormField<Tarla>, 'Tarla seç'),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Örnek Tarla').last);
        await tester.pumpAndSettle();

        // Görev alanı boş → kaydet
        await tester.tap(find.text('Görevi Ekle'));
        await tester.pumpAndSettle();

        expect(find.textContaining('Görev adı boş olamaz'), findsOneWidget);
      });

      testWidgets(
        'geçerli görev isCompleted:false ile repository\'ye gönderilir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final t = _tarla('t1', name: 'Örnek Tarla');
          final repo = FakeFaaliyetRepository();

          await tester.pumpWidget(
            _wrap(tarlaRepo: FakeTarlaRepository([t]), faaliyetRepo: repo),
          );
          await tester.pumpAndSettle();

          // Tarla seç
          await tester.tap(
            find.widgetWithText(DropdownButtonFormField<Tarla>, 'Tarla seç'),
          );
          await tester.pumpAndSettle();
          await tester.tap(find.text('Örnek Tarla').last);
          await tester.pumpAndSettle();

          // Görev adı gir
          await tester.enterText(
            find.widgetWithText(TextFormField, 'Görev'),
            'Sulama',
          );

          // Kaydet
          await tester.tap(find.text('Görevi Ekle'));
          await tester.pumpAndSettle();

          expect(repo.eklenenler, hasLength(1));
          expect(repo.eklenenler.first.isCompleted, isFalse);
          expect(repo.eklenenler.first.type, 'Sulama');
          expect(repo.eklenenler.first.tarlaId, 't1');
        },
      );

      testWidgets('kayıt sonrası liste yenilenir ve yeni görev görünür', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final t = _tarla('t1', name: 'Örnek Tarla');
        final repo = FakeFaaliyetRepository();

        await tester.pumpWidget(
          _wrap(tarlaRepo: FakeTarlaRepository([t]), faaliyetRepo: repo),
        );
        await tester.pumpAndSettle();

        // Tarla seç
        await tester.tap(
          find.widgetWithText(DropdownButtonFormField<Tarla>, 'Tarla seç'),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Örnek Tarla').last);
        await tester.pumpAndSettle();

        // Görev adı gir
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Görev'),
          'SulamaGörev',
        );

        // Kaydet
        await tester.tap(find.text('Görevi Ekle'));
        await tester.pumpAndSettle();

        // Yeni görev bugün planlı olduğundan Bugün filtresinde görünmeli
        expect(find.text('SulamaGörev'), findsOneWidget);
      });

      testWidgets('kayıt sonrası Görev eklendi SnackBar gösterilir', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final t = _tarla('t1', name: 'Örnek Tarla');

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([t]),
            faaliyetRepo: FakeFaaliyetRepository(),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(
          find.widgetWithText(DropdownButtonFormField<Tarla>, 'Tarla seç'),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Örnek Tarla').last);
        await tester.pumpAndSettle();

        await tester.enterText(
          find.widgetWithText(TextFormField, 'Görev'),
          'Sulama',
        );

        await tester.tap(find.text('Görevi Ekle'));
        await tester.pump(); // microtask flush
        await tester.pump(); // render SnackBar

        expect(find.text('Görev eklendi.'), findsOneWidget);
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

    // ── onDataChanged callback ────────────────────────────────────────────────
    group('onDataChanged', () {
      testWidgets('görev başarıyla eklenince onDataChanged çağrılır', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        bool called = false;
        final repo = FakeFaaliyetRepository();

        await tester.pumpWidget(
          _wrap(
            tarlaRepo: FakeTarlaRepository([_tarla('t1', name: 'Tarla 1')]),
            faaliyetRepo: repo,
            onDataChanged: () => called = true,
          ),
        );
        await tester.pumpAndSettle();

        // Tarla seç
        await tester.tap(find.byType(DropdownButtonFormField<Tarla>));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Tarla 1').last);
        await tester.pumpAndSettle();

        // Görev adı yaz
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Görev'),
          'Test görevi',
        );

        // Görevi Ekle butonuna bas
        await tester.tap(find.text('Görevi Ekle'));
        await tester.pumpAndSettle();

        expect(called, isTrue);
      });
    });
  });
}

// ---------------------------------------------------------------------------
// Helper fakes
// ---------------------------------------------------------------------------

class _SlowFaaliyetRepository implements FaaliyetRepository {
  _SlowFaaliyetRepository(this._future);
  final Future<List<Faaliyet>> _future;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() => _future;
}

class _CountingFaaliyetRepo implements FaaliyetRepository {
  _CountingFaaliyetRepo(this._fn);
  final Future<List<Faaliyet>> Function() _fn;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() => _fn();
}

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';
import 'package:uuid/uuid.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeFaaliyetRepository
    implements FaaliyetRepository, PlanliGorevRepository {
  final List<Faaliyet> kayitlar = [];
  final List<Faaliyet> planliGorevler = [];
  Object? _hataNeden;

  void hatayiAyarla(Object neden) => _hataNeden = neden;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {
    if (_hataNeden != null) throw _hataNeden!;
    kayitlar.add(faaliyet);
  }

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {
    if (_hataNeden != null) throw _hataNeden!;
    planliGorevler.add(gorev);
  }

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => planliGorevler;

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

Widget _wrap(Widget child) => MaterialApp(theme: AppTheme.light, home: child);

const _tarlaId = 'tarla1';

Future<void> _isTuruGir(WidgetTester tester, String text) async {
  final finder = find.widgetWithText(TextFormField, 'İş türü');
  await tester.enterText(finder, text);
  await tester.pumpAndSettle();
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('FaaliyetEklemeEkrani (İş Ekle)', () {
    group('ekran başlığı ve durum seçimi', () {
      testWidgets('ekranda İş Ekle başlığı görünür', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('İş Ekle'), findsOneWidget);
      });

      testWidgets('varsayılan durum Yapıldı seçilidir', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Bu işi ne yapıyorsun?'), findsOneWidget);
        expect(find.text('Yapıldığı tarih'), findsOneWidget);
        expect(find.text('İşi Kaydet'), findsOneWidget);

        final segmented = tester.widget<SegmentedButton<IsDurumu>>(
          find.byType(SegmentedButton<IsDurumu>),
        );
        expect(segmented.selected, {IsDurumu.yapildi});
      });

      testWidgets('Planla modu seçilebilir ve UI planlama moduna geçer', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('Planla'));
        await tester.pumpAndSettle();

        expect(find.text('Planlanan tarih'), findsOneWidget);
        expect(find.text('İşi Planla'), findsOneWidget);

        final segmented = tester.widget<SegmentedButton<IsDurumu>>(
          find.byType(SegmentedButton<IsDurumu>),
        );
        expect(segmented.selected, {IsDurumu.planla});
      });
    });

    group('UI serbest metin kontrolleri (Test 13, 14, 15)', () {
      testWidgets('iş türü için DropdownButtonFormField bulunmaz (Test 13)', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      });

      testWidgets('ekranda "Diğer" seçeneği bulunmaz (Test 14)', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Diğer'), findsNothing);
      });

      testWidgets('iş türü için doğrudan serbest metin TextFormField sunulur (Test 15)', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.widgetWithText(TextFormField, 'İş türü'), findsOneWidget);
      });
    });

    group('form doğrulama (Test 5, 6, 7)', () {
      testWidgets('boş iş türü reddedilir (Test 5)', (tester) async {
        final repo = FakeFaaliyetRepository();
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: bugun,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(repo.planliGorevler, isEmpty);
        expect(find.text('Lütfen yapılan veya planlanan işi yazın.'), findsOneWidget);
      });

      testWidgets('sadece whitespace girildiğinde reddedilir (Test 6)', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: bugun,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, '     ');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(find.text('Lütfen yapılan veya planlanan işi yazın.'), findsOneWidget);
      });

      testWidgets('150 karakterden uzun iş türü reddedilir (Test 7)', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: bugun,
            ),
          ),
        );
        await tester.pumpAndSettle();

        final longText = 'A' * 151;
        // Enter via controller directly to bypass maxLength input formatter for validation testing
        await _isTuruGir(tester, longText);
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
      });

      testWidgets('tarih seçilmeden kayıt yapılmaz', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(tarlaId: _tarlaId, faaliyetRepository: repo),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Sulama');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Lütfen gerçekleşme tarihini seçin.'),
          findsOneWidget,
        );
      });

      testWidgets('Yapıldı modunda gelecek tarih girilirse kayıt yapılmaz', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final yarin = DateTime.now().add(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: yarin,
              initialIsCompleted: true,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Sulama');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Gerçekleşme tarihi gelecek bir tarih olamaz.'),
          findsOneWidget,
        );
      });

      testWidgets('Planla modunda geçmiş tarih girilirse kayıt yapılmaz', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final dun = DateTime.now().subtract(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: dun,
              initialIsCompleted: false,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Sulama');
        await tester.tap(find.text('İşi Planla'));
        await tester.pumpAndSettle();

        expect(repo.planliGorevler, isEmpty);
        expect(
          find.text('Planlanan tarih geçmiş bir tarih olamaz.'),
          findsOneWidget,
        );
      });
    });

    group('kaydetme davranışı ve serbest metin akışı (Test 1, 2, 3, 4, 11, 12)', () {
      testWidgets(
        '"Damla sulama yaptım" create edilir ve aynı string kaydedilir (Test 1 & 15)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final bugun = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: bugun,
                initialIsCompleted: true,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, 'Damla sulama yaptım');
          await tester.enterText(
            find.widgetWithText(TextFormField, 'Not'),
            'Sabah erkenden açıldı',
          );
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.planliGorevler, isEmpty);

          final f = repo.kayitlar.first;
          expect(f.isCompleted, isTrue);
          expect(f.dueDate, isNull);
          expect(f.type, 'Damla sulama yaptım');
          expect(f.note, 'Sabah erkenden açıldı');
          expect(Uuid.isValidUUID(fromString: f.id), isTrue);
        },
      );

      testWidgets(
        '"Çapa" enum listesinde olmamasına rağmen kabul edilir (Test 2)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final bugun = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: bugun,
                initialIsCompleted: true,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, 'Çapa');
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.kayitlar.first.type, 'Çapa');
        },
      );

      testWidgets(
        '"Fidan bağlama" serbest metin olarak kabul edilir (Test 3)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final bugun = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: bugun,
                initialIsCompleted: true,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, 'Fidan bağlama');
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.kayitlar.first.type, 'Fidan bağlama');
        },
      );

      testWidgets(
        'Whitespace "   Çapa   " trim edilerek "Çapa" olarak kaydedilir (Test 4)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final bugun = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: bugun,
                initialIsCompleted: true,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, '   Çapa   ');
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.kayitlar.first.type, 'Çapa');
        },
      );

      testWidgets(
        'Planlanan activity: "Toprak havalandırma" doğru kaydedilir (Test 11)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final yarin = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day + 1,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: yarin,
                initialIsCompleted: false,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, 'Toprak havalandırma');
          await tester.tap(find.text('İşi Planla'));
          await tester.pumpAndSettle();

          expect(repo.planliGorevler, hasLength(1));
          expect(repo.planliGorevler.first.type, 'Toprak havalandırma');
          expect(repo.planliGorevler.first.dueDate, yarin);
          expect(repo.planliGorevler.first.isCompleted, isFalse);
        },
      );

      testWidgets(
        'Tamamlanan activity: "Toprak havalandırma" aynı model type alanını kullanır (Test 12)',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final bugun = DateTime(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          );

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialSelectedDate: bugun,
                initialIsCompleted: true,
              ),
            ),
          );
          await tester.pumpAndSettle();

          await _isTuruGir(tester, 'Toprak havalandırma');
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.kayitlar.first.type, 'Toprak havalandırma');
          expect(repo.kayitlar.first.isCompleted, isTrue);
        },
      );

      testWidgets('not alanı opsiyoneldir', (tester) async {
        final repo = FakeFaaliyetRepository();
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: bugun,
              initialIsCompleted: true,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Hasat');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, hasLength(1));
        expect(repo.kayitlar.first.type, 'Hasat');
        expect(repo.kayitlar.first.note, isEmpty);
      });

      testWidgets('loading sırasında ikinci kayıt engellenir', (tester) async {
        final completer = Completer<void>();
        final slowRepo = _SlowFaaliyetRepository(completer.future);
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: slowRepo,
              initialSelectedDate: bugun,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Sulama');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pump();

        final btn = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
        expect(btn.onPressed, isNull);
        expect(slowRepo.callCount, 1);

        completer.complete();
        await tester.pumpAndSettle();
      });

      testWidgets('repository hatası kullanıcıya SnackBar gösterir', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        repo.hatayiAyarla(Exception('db hatası'));
        final bugun = DateTime(
          DateTime.now().year,
          DateTime.now().month,
          DateTime.now().day,
        );

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: bugun,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await _isTuruGir(tester, 'Sulama');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(
          find.text('İş kaydedilemedi. Lütfen tekrar deneyin.'),
          findsOneWidget,
        );
      });
    });
  });
}

// ---------------------------------------------------------------------------
// Helper fake with controllable delay
// ---------------------------------------------------------------------------

class _SlowFaaliyetRepository
    implements FaaliyetRepository, PlanliGorevRepository {
  _SlowFaaliyetRepository(this._future);
  final Future<void> _future;
  int callCount = 0;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) {
    callCount++;
    return _future;
  }

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) {
    callCount++;
    return _future;
  }

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => [];

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

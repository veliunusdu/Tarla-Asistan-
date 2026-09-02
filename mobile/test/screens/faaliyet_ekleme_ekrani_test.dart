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

Future<void> _secilenTur(WidgetTester tester, String tur) async {
  await tester.tap(find.byType(DropdownButtonFormField<String>));
  await tester.pumpAndSettle();
  await tester.tap(find.text(tur).last);
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

        // Planla segmentine bas
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

    group('form doğrulama', () {
      testWidgets('iş türü seçilmeden kayıt yapılmaz', (tester) async {
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
        expect(find.text('Lütfen bir iş türü seçin.'), findsOneWidget);
      });

      testWidgets('Diğer seçildiğinde İş adı zorunludur', (tester) async {
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

        await _secilenTur(tester, 'Diğer');
        expect(find.text('İş adı'), findsOneWidget);

        await tester.tap(find.text('İşi Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(find.text('Lütfen iş adını girin.'), findsOneWidget);
      });

      testWidgets('tarih seçilmeden kayıt yapılmaz', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(tarlaId: _tarlaId, faaliyetRepository: repo),
          ),
        );
        await tester.pumpAndSettle();

        await _secilenTur(tester, 'Sulama');
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

        await _secilenTur(tester, 'Sulama');
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

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('İşi Planla'));
        await tester.pumpAndSettle();

        expect(repo.planliGorevler, isEmpty);
        expect(
          find.text('Planlanan tarih geçmiş bir tarih olamaz.'),
          findsOneWidget,
        );
      });
    });

    group('kaydetme davranışı ve repository akışı', () {
      testWidgets(
        'Yapıldı modunda kayıt Faaliyet repository addFaaliyet akışını kullanır ve addPlanliGorev çağırmaz',
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

          await _secilenTur(tester, 'Gübreleme');
          await tester.enterText(
            find.widgetWithText(TextFormField, 'Not'),
            'Azot gübresi atıldı',
          );
          await tester.tap(find.text('İşi Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          expect(repo.planliGorevler, isEmpty);

          final f = repo.kayitlar.first;
          expect(f.isCompleted, isTrue);
          expect(f.dueDate, isNull);
          expect(f.type, 'Gübreleme');
          expect(f.note, 'Azot gübresi atıldı');
          expect(Uuid.isValidUUID(fromString: f.id), isTrue);
        },
      );

      testWidgets(
        'Planla modunda kayıt PlanliGorevRepository addPlanliGorev akışını kullanır ve addFaaliyet çağırmaz',
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

          await _secilenTur(tester, 'İlaçlama');
          await tester.enterText(
            find.widgetWithText(TextFormField, 'Not'),
            'Mantar ilacı atılacak',
          );
          await tester.tap(find.text('İşi Planla'));
          await tester.pumpAndSettle();

          expect(repo.planliGorevler, hasLength(1));
          expect(repo.kayitlar, isEmpty);

          final f = repo.planliGorevler.first;
          expect(f.isCompleted, isFalse);
          expect(f.dueDate, yarin);
          expect(f.type, 'İlaçlama');
          expect(f.note, 'Mantar ilacı atılacak');
          expect(Uuid.isValidUUID(fromString: f.id), isTrue);
        },
      );

      testWidgets(
        'Planla modunda Diğer seçildiğinde task title girilen İş adı olur',
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

          await _secilenTur(tester, 'Diğer');
          await tester.enterText(
            find.widgetWithText(TextFormField, 'İş adı'),
            'Budama temizliği',
          );
          await tester.tap(find.text('İşi Planla'));
          await tester.pumpAndSettle();

          expect(repo.planliGorevler, hasLength(1));
          expect(repo.planliGorevler.first.type, 'Budama temizliği');
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

        await _secilenTur(tester, 'Hasat');
        // Not girmeden kaydet
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

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('İşi Kaydet'));
        await tester.pump();

        // Kaydediliyor — buton pasif olmalı
        final btn = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
        expect(btn.onPressed, isNull);

        // İkinci kez tap yapılmaya çalışılsa dahi çağrı sayısı 1 kalmalı
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

        await _secilenTur(tester, 'Sulama');
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

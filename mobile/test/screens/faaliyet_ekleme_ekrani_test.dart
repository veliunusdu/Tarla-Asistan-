import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeFaaliyetRepository implements FaaliyetRepository {
  final List<Faaliyet> kayitlar = [];
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
  group('FaaliyetEklemeEkrani', () {
    group('form doğrulama', () {
      testWidgets('faaliyet türü seçilmeden kayıt yapılmaz', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialSelectedDate: DateTime.now().add(const Duration(days: 1)),
            ),
          ),
        );

        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(find.text('Lütfen bir faaliyet türü seçin.'), findsOneWidget);
      });

      testWidgets('tarih seçilmeden kayıt yapılmaz', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(tarlaId: _tarlaId, faaliyetRepository: repo),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(find.textContaining('tarihi seçin'), findsOneWidget);
      });

      testWidgets('Planlandı modunda geçmiş tarih girilirse kayıt yapılmaz', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final dun = DateTime.now().subtract(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialIsCompleted: false,
              initialSelectedDate: dun,
            ),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Planlanan tarih geçmiş bir tarih olamaz.'),
          findsOneWidget,
        );
      });

      testWidgets('Tamamlandı modunda gelecek tarih girilirse kayıt yapılmaz', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        final yarin = DateTime.now().add(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialIsCompleted: true,
              initialSelectedDate: yarin,
            ),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Gerçekleşme tarihi gelecek bir tarih olamaz.'),
          findsOneWidget,
        );
      });
    });

    group('kaydetme davranışı', () {
      testWidgets(
        'Planlandı faaliyeti isCompleted:false ve doğru dueDate ile kaydedilir',
        (tester) async {
          final repo = FakeFaaliyetRepository();
          final yarin = DateTime.now().add(const Duration(days: 1));
          final beklenenGun = DateTime(yarin.year, yarin.month, yarin.day);

          await tester.pumpWidget(
            _wrap(
              FaaliyetEklemeEkrani(
                tarlaId: _tarlaId,
                faaliyetRepository: repo,
                initialIsCompleted: false,
                initialSelectedDate: beklenenGun,
              ),
            ),
          );

          await _secilenTur(tester, 'Sulama');
          await tester.tap(find.text('Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          final f = repo.kayitlar.first;
          expect(f.isCompleted, isFalse);
          expect(f.dueDate, beklenenGun);
          expect(f.type, 'Sulama');
        },
      );

      testWidgets(
        'Tamamlandı faaliyeti isCompleted:true ve dueDate:null kaydedilir',
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
                initialIsCompleted: true,
                initialSelectedDate: bugun,
              ),
            ),
          );

          await _secilenTur(tester, 'Gübreleme');
          await tester.tap(find.text('Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          final f = repo.kayitlar.first;
          expect(f.isCompleted, isTrue);
          expect(f.dueDate, isNull);
          expect(f.type, 'Gübreleme');
        },
      );

      testWidgets('loading sırasında ikinci kayıt engellenir', (tester) async {
        final completer = Completer<void>();
        final slowRepo = _SlowFaaliyetRepository(completer.future);
        final yarin = DateTime.now().add(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: slowRepo,
              initialIsCompleted: false,
              initialSelectedDate: yarin,
            ),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('Kaydet'));
        await tester.pump();

        // Kaydediliyor — buton pasif olmalı
        final btn = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
        expect(btn.onPressed, isNull);

        completer.complete();
        await tester.pumpAndSettle();
      });

      testWidgets('repository hatası kullanıcıya SnackBar gösterir', (
        tester,
      ) async {
        final repo = FakeFaaliyetRepository();
        repo.hatayiAyarla(Exception('db hatası'));
        final yarin = DateTime.now().add(const Duration(days: 1));

        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(
              tarlaId: _tarlaId,
              faaliyetRepository: repo,
              initialIsCompleted: false,
              initialSelectedDate: yarin,
            ),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(
          find.text('Faaliyet kaydedilemedi. Lütfen tekrar deneyin.'),
          findsOneWidget,
        );
      });
    });
  });
}

// ---------------------------------------------------------------------------
// Helper fake with controllable delay
// ---------------------------------------------------------------------------

class _SlowFaaliyetRepository implements FaaliyetRepository {
  _SlowFaaliyetRepository(this._future);
  final Future<void> _future;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) => _future;

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

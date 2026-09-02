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
      testWidgets('yalnızca gerçekleşen işlem kaydeder ve planlı durum seçicisini göstermez', (
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

        expect(find.text('İşlem Kaydı Ekle'), findsOneWidget);
        expect(find.byType(SegmentedButton<bool>), findsNothing);
        expect(find.text('Gerçekleşme Tarihi'), findsOneWidget);
      });

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

      testWidgets('açıklama boş veya 2 karakterden kısaysa kayıt yapılmaz', (
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

        await _secilenTur(tester, 'Sulama');
        // Açıklama girmeden kaydetmeye çalış
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Faaliyet açıklaması en az 2 karakter olmalıdır.'),
          findsOneWidget,
        );

        // 1 karakter girildiğinde de hata devam etmeli
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
          'A',
        );
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Faaliyet açıklaması en az 2 karakter olmalıdır.'),
          findsOneWidget,
        );
      });

      testWidgets('tarih seçilmeden kayıt yapılmaz', (tester) async {
        final repo = FakeFaaliyetRepository();
        await tester.pumpWidget(
          _wrap(
            FaaliyetEklemeEkrani(tarlaId: _tarlaId, faaliyetRepository: repo),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
          'Sulama yapıldı',
        );
        await tester.tap(find.text('Kaydet'));
        await tester.pumpAndSettle();

        expect(repo.kayitlar, isEmpty);
        expect(
          find.text('Lütfen gerçekleşme tarihini seçin.'),
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
              initialSelectedDate: yarin,
            ),
          ),
        );

        await _secilenTur(tester, 'Sulama');
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
          'Sulama yapıldı',
        );
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
        'Tamamlandı faaliyeti isCompleted:true, dueDate:null ve UUID id ile kaydedilir',
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
              ),
            ),
          );

          await _secilenTur(tester, 'Gübreleme');
          await tester.enterText(
            find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
            'Azot gübresi atıldı',
          );
          await tester.tap(find.text('Kaydet'));
          await tester.pumpAndSettle();

          expect(repo.kayitlar, hasLength(1));
          final f = repo.kayitlar.first;
          expect(f.isCompleted, isTrue);
          expect(f.dueDate, isNull);
          expect(f.type, 'Gübreleme');
          expect(f.note, 'Azot gübresi atıldı');
          expect(Uuid.isValidUUID(fromString: f.id), isTrue);
        },
      );

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

        await _secilenTur(tester, 'Sulama');
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
          'Sulama yapıldı',
        );
        await tester.tap(find.text('Kaydet'));
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

        await _secilenTur(tester, 'Sulama');
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Faaliyet Açıklaması'),
          'Sulama yapıldı',
        );
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
  int callCount = 0;

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) {
    callCount++;
    return _future;
  }

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

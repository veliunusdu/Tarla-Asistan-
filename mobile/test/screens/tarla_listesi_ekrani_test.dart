import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
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

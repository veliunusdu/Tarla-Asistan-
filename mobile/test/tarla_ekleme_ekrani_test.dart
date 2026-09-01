import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';

class _RecordingTarlaRepository
    implements TarlaRepository, TarlaUpdateRepository {
  int writes = 0;
  Tarla? updated;

  @override
  Future<List<Tarla>> getTarlalar() async => [];

  @override
  Future<void> addTarla(Tarla tarla) async {
    writes += 1;
  }

  @override
  Future<void> updateTarla(Tarla tarla) async {
    updated = tarla;
  }
}

void main() {
  testWidgets('rejects a farm name longer than 120 characters', (tester) async {
    final store = _RecordingTarlaRepository();
    await tester.pumpWidget(
      MaterialApp(home: TarlaEklemeEkrani(repository: store)),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Tarla Adı'),
      List.filled(121, 'a').join(),
    );
    await tester.tap(find.text('Kaydet'));
    await tester.pump();

    expect(
      find.text('Tarla adı en fazla 120 karakter olabilir.'),
      findsOneWidget,
    );
    expect(store.writes, 0);
  });

  testWidgets('updates an existing farm instead of creating another one', (
    tester,
  ) async {
    final store = _RecordingTarlaRepository();
    final tarla = Tarla(
      id: 'farm-1',
      name: 'Eski Tarla',
      latitude: 38.42,
      longitude: 27.14,
      size: 12,
      cropType: 'Buğday',
      plantingDate: DateTime(2026, 3, 15),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: TarlaEklemeEkrani(repository: store, editingTarla: tarla),
      ),
    );

    expect(find.text('Tarla Düzenle'), findsOneWidget);
    expect(
      find.text('Ürün bilgisi bu ekranda değiştirilemez.'),
      findsOneWidget,
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Tarla Adı'),
      'Yeni Tarla',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
      '18,5',
    );
    await tester.tap(find.text('Kaydet'));
    await tester.pump();

    expect(store.writes, 0);
    expect(store.updated?.id, 'farm-1');
    expect(store.updated?.name, 'Yeni Tarla');
    expect(store.updated?.size, 18.5);
  });
}

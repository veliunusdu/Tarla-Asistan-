import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';

class _RecordingTarlaRepository implements TarlaRepository {
  int writes = 0;

  @override
  Future<List<Tarla>> getTarlalar() async => [];

  @override
  Future<void> addTarla(Tarla tarla) async {
    writes += 1;
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
}

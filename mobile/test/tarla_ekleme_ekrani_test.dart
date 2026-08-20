import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';
import 'package:mobile/services/firestore_farm_repository.dart';

void main() {
  testWidgets('rejects a farm name longer than 120 characters', (tester) async {
    final store = _RecordingFarmStore();
    await tester.pumpWidget(
      MaterialApp(
        home: TarlaEklemeEkrani(
          repository: FirestoreFarmRepository(uid: 'uid-1', store: store),
        ),
      ),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Tarla adı'),
      List.filled(121, 'a').join(),
    );
    await tester.tap(find.text('Tarlayı kaydet'));
    await tester.pump();

    expect(find.text('En fazla 120 karakter girin.'), findsOneWidget);
    expect(store.writes, 0);
  });
}

class _RecordingFarmStore implements FirestoreFarmStore {
  int writes = 0;

  @override
  Future<void> setDocument(String path, Map<String, Object?> data) async {
    writes += 1;
  }

  @override
  Stream<List<FirestoreActivityDocument>> watchActivities(String farmId) =>
      const Stream.empty();

  @override
  Stream<List<FirestoreFarmDocument>> watchFarms(String ownerId) =>
      const Stream.empty();

  @override
  Stream<FirestoreWriteFailure> get writeFailures => const Stream.empty();
}

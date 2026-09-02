import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/main.dart';

void main() {
  testWidgets('uygulama Türkçe Material yerelleştirmesini destekler', (
    tester,
  ) async {
    await tester.pumpWidget(
      const TarimAsistaniApp(isFirstRun: true, firebaseReady: false),
    );

    final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(app.supportedLocales, contains(const Locale('tr')));
  });
}

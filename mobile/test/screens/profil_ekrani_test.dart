import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/screens/profil_ekrani.dart';

void main() {
  testWidgets('confirms before signing out', (tester) async {
    var calls = 0;
    await tester.pumpWidget(
      MaterialApp(home: ProfilEkrani(onLogout: () async => calls++)),
    );

    await tester.tap(find.text('Çıkış yap'));
    await tester.pumpAndSettle();

    expect(find.text('Çıkış yapmak istiyor musunuz?'), findsOneWidget);
    expect(calls, 0);
  });
}

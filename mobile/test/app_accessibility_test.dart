import 'package:flutter/material.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('login remains usable on a narrow screen with large text', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    await tester.binding.setSurfaceSize(const Size(320, 640));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(textScaler: TextScaler.linear(2)),
        child: TarimAsistaniApp(
          isFirstRun: false,
          firebaseReady: false,
          authStateChanges: Stream<User?>.empty(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Telefon numarası'), findsOneWidget);
    expect(find.text('Kod gönder'), findsOneWidget);
    expect(
      tester.getSize(find.byType(FilledButton)).height,
      greaterThanOrEqualTo(48),
    );
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
    expect(tester.takeException(), isNull);
  });

  testWidgets('onboarding supports large text without overflow', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({});
    await tester.binding.setSurfaceSize(const Size(320, 640));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(textScaler: TextScaler.linear(2)),
        child: TarimAsistaniApp(
          isFirstRun: true,
          firebaseReady: false,
          authStateChanges: Stream<User?>.empty(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text("Tarla Asistanı'na hoş geldiniz"), findsOneWidget);
    expect(find.text('İlerle'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/widgets/app_loading_view.dart';

import '../../helpers/test_app.dart';

void main() {
  group('AppLoadingView', () {
    testWidgets('CircularProgressIndicator gösterir', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppLoadingView()));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('mesaj verilince metin gösterir', (tester) async {
      await tester.pumpWidget(
        const TestApp(child: AppLoadingView(message: 'Yükleniyor…')),
      );

      expect(find.text('Yükleniyor…'), findsOneWidget);
    });

    testWidgets('mesaj verilmezse metin göstermez', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppLoadingView()));

      expect(find.byType(Text), findsNothing);
    });
  });
}

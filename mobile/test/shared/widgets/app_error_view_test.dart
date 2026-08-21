import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/widgets/app_error_view.dart';

import '../../helpers/test_app.dart';

void main() {
  group('AppErrorView', () {
    testWidgets('varsayılan başlık ve açıklamayı gösterir', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppErrorView()));

      expect(find.text('Bir sorun oluştu'), findsOneWidget);
      expect(
        find.text('İşlem tamamlanamadı. Lütfen tekrar deneyin.'),
        findsOneWidget,
      );
    });

    testWidgets('teknik exception metni göstermez', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppErrorView()));

      // Stack trace veya "Exception:" içeren metin olmamalı
      final texts = tester
          .widgetList<Text>(find.byType(Text))
          .map((t) => t.data ?? '')
          .toList();

      expect(
        texts.any((t) => t.contains('Exception') || t.contains('Stack')),
        isFalse,
      );
    });

    testWidgets('retry callback verilince buton gösterir', (tester) async {
      var retried = false;

      await tester.pumpWidget(
        TestApp(child: AppErrorView(onRetry: () => retried = true)),
      );

      final button = find.widgetWithText(ElevatedButton, 'Tekrar Dene');
      expect(button, findsOneWidget);

      await tester.tap(button);
      expect(retried, isTrue);
    });

    testWidgets('retry callback olmadan buton göstermez', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppErrorView()));

      expect(find.byType(ElevatedButton), findsNothing);
    });

    testWidgets('hata ikonu gösterir', (tester) async {
      await tester.pumpWidget(const TestApp(child: AppErrorView()));

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
    });
  });
}

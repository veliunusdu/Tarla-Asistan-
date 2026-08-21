import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/widgets/app_empty_view.dart';

import '../../helpers/test_app.dart';

void main() {
  group('AppEmptyView', () {
    testWidgets('ikon ve başlık gösterir', (tester) async {
      await tester.pumpWidget(
        const TestApp(
          child: AppEmptyView(icon: Icons.grass, title: 'Henüz tarla yok'),
        ),
      );

      expect(find.byIcon(Icons.grass), findsOneWidget);
      expect(find.text('Henüz tarla yok'), findsOneWidget);
    });

    testWidgets('açıklama verilince gösterir', (tester) async {
      await tester.pumpWidget(
        const TestApp(
          child: AppEmptyView(
            icon: Icons.grass,
            title: 'Başlık',
            description: 'Açıklama metni',
          ),
        ),
      );

      expect(find.text('Açıklama metni'), findsOneWidget);
    });

    testWidgets('işlem butonu verilince gösterir ve tıklanabilir', (
      tester,
    ) async {
      var tapped = false;

      await tester.pumpWidget(
        TestApp(
          child: AppEmptyView(
            icon: Icons.grass,
            title: 'Başlık',
            actionLabel: 'Tarla Ekle',
            onAction: () => tapped = true,
          ),
        ),
      );

      final button = find.widgetWithText(ElevatedButton, 'Tarla Ekle');
      expect(button, findsOneWidget);

      // Dokunma alanı en az 48dp (DESIGN.md §4.1)
      final size = tester.getSize(button);
      expect(size.height, greaterThanOrEqualTo(48));

      await tester.tap(button);
      expect(tapped, isTrue);
    });

    testWidgets('işlem callback olmadan buton göstermez', (tester) async {
      await tester.pumpWidget(
        const TestApp(
          child: AppEmptyView(
            icon: Icons.grass,
            title: 'Başlık',
            actionLabel: 'Tarla Ekle',
          ),
        ),
      );

      expect(find.byType(ElevatedButton), findsNothing);
    });
  });
}

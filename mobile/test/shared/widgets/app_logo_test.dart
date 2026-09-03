import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_colors.dart';
import 'package:mobile/shared/widgets/app_logo.dart';

void main() {
  group('AppLogo Widget Tests', () {
    testWidgets('renders Icons.grass matching Tarlalarım identity', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(size: 80),
          ),
        ),
      );

      expect(find.byIcon(Icons.grass), findsOneWidget);
    });

    testWidgets('renders with transparent background when backgroundColor is null',
        (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(
              size: 64,
              backgroundColor: null,
            ),
          ),
        ),
      );

      expect(find.byIcon(Icons.grass), findsOneWidget);
      expect(find.byType(Container), findsNothing);
    });

    testWidgets('supports custom colors and sizing', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(
              size: 100,
              iconSize: 50,
              backgroundColor: AppColors.primary,
              iconColor: Colors.amber,
              isCircle: false,
            ),
          ),
        ),
      );

      final icon = tester.widget<Icon>(find.byIcon(Icons.grass));
      expect(icon.size, equals(50));
      expect(icon.color, equals(Colors.amber));
    });
  });
}

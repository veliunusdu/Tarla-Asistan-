import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_colors.dart';
import 'package:mobile/app/theme/app_theme.dart';

void main() {
  group('AppTheme', () {
    late ThemeData theme;

    setUp(() {
      theme = AppTheme.light;
    });

    test('Material 3 etkin', () {
      expect(theme.useMaterial3, isTrue);
    });

    test('primary renk DESIGN.md ile uyumlu (#2E7D32)', () {
      expect(theme.colorScheme.primary, AppColors.primary);
    });

    test('ElevatedButton minimum yüksekliği 48dp (DESIGN.md §4.1)', () {
      final size = theme.elevatedButtonTheme.style?.minimumSize?.resolve({});
      expect(size?.height, greaterThanOrEqualTo(48));
    });

    test('bodyLarge en az 16sp (DESIGN.md §3)', () {
      expect(theme.textTheme.bodyLarge?.fontSize, greaterThanOrEqualTo(16));
    });

    testWidgets('MaterialApp tema bağlantısı doğru çalışır', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: const Scaffold(body: Text('test')),
        ),
      );

      final context = tester.element(find.text('test'));
      expect(Theme.of(context).colorScheme.primary, AppColors.primary);
      expect(Theme.of(context).useMaterial3, isTrue);
    });
  });
}

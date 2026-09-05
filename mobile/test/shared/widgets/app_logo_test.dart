import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/widgets/app_logo.dart';

void main() {
  group('AppLogo Widget Tests', () {
    testWidgets('renders the selected white-background brand asset', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(size: 80),
          ),
        ),
      );

      final image = tester.widget<Image>(find.byType(Image));
      expect(
        image.image,
        isA<AssetImage>().having(
          (provider) => provider.assetName,
          'assetName',
          AppLogo.assetPath,
        ),
      );
    });

    testWidgets('uses the requested square size', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(size: 72),
          ),
        ),
      );

      final image = tester.widget<Image>(find.byType(Image));
      expect(image.width, 72);
      expect(image.height, 72);
    });

    testWidgets('exposes an accessible brand label', (tester) async {
      final semantics = tester.ensureSemantics();
      addTearDown(semantics.dispose);

      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: AppLogo(
              semanticLabel: 'Tarla Asistanı marka işareti',
            ),
          ),
        ),
      );

      expect(
        find.bySemanticsLabel('Tarla Asistanı marka işareti'),
        findsOneWidget,
      );
    });
  });
}

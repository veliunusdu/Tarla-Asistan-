import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/shared/widgets/tarla_secim_bottom_sheet.dart';

void main() {
  final tarlalar = [
    Tarla(
      id: 'tarla-1',
      name: 'Kuzey Tarlası',
      cropType: 'Buğday',
      size: 25.0,
    ),
    Tarla(
      id: 'tarla-2',
      name: 'Güney Tarlası',
      cropType: 'Mısır',
      size: 40.0,
    ),
  ];

  testWidgets('TarlaSecimBottomSheet shows title and farm list correctly', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: TarlaSecimBottomSheet(tarlalar: tarlalar),
        ),
      ),
    );

    // 1. Title verification
    expect(find.text('Hangi tarla için?'), findsOneWidget);

    // 2. Farm names and subtitles verification
    expect(find.text('Kuzey Tarlası'), findsOneWidget);
    expect(find.text('Buğday · 25 dönüm'), findsOneWidget);
    expect(find.text('Güney Tarlası'), findsOneWidget);
    expect(find.text('Mısır · 40 dönüm'), findsOneWidget);
  });

  testWidgets('TarlaSecimBottomSheet returns selected farm on tap via show()', (
    tester,
  ) async {
    Tarla? secilenTarla;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => ElevatedButton(
              onPressed: () async {
                secilenTarla = await TarlaSecimBottomSheet.show(
                  context,
                  tarlalar: tarlalar,
                );
              },
              child: const Text('Seç'),
            ),
          ),
        ),
      ),
    );

    // Tap button to open bottom sheet
    await tester.tap(find.text('Seç'));
    await tester.pumpAndSettle();

    expect(find.text('Hangi tarla için?'), findsOneWidget);

    // 3. Tap second farm
    await tester.tap(find.text('Güney Tarlası'));
    await tester.pumpAndSettle();

    // Verify correct Tarla was returned
    expect(secilenTarla, isNotNull);
    expect(secilenTarla!.id, 'tarla-2');
    expect(secilenTarla!.name, 'Güney Tarlası');
  });

  testWidgets('TarlaSecimBottomSheet safe on dismiss / barrier tap', (
    tester,
  ) async {
    Tarla? secilenTarla;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => ElevatedButton(
              onPressed: () async {
                secilenTarla = await TarlaSecimBottomSheet.show(
                  context,
                  tarlalar: tarlalar,
                );
              },
              child: const Text('Seç'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Seç'));
    await tester.pumpAndSettle();

    expect(find.text('Hangi tarla için?'), findsOneWidget);

    // 4. Dismiss by tapping barrier above bottom sheet
    await tester.tapAt(const Offset(20, 20));
    await tester.pumpAndSettle();

    // Sheet closed, returns null safely
    expect(find.text('Hangi tarla için?'), findsNothing);
    expect(secilenTarla, isNull);
  });
}

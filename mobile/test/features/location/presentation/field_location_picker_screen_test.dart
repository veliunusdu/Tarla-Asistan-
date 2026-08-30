import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
import 'package:mobile/features/location/presentation/field_location_picker_screen.dart';

void main() {
  testWidgets('returns the location selected from the map', (tester) async {
    TarlaLocation? result;

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => ElevatedButton(
            onPressed: () async {
              result = await Navigator.of(context).push<TarlaLocation>(
                MaterialPageRoute(
                  builder: (_) => FieldLocationPickerScreen(
                    mapBuilder:
                        (context, center, onLocationSelected, onLoadError) {
                          return ElevatedButton(
                            onPressed: () => onLocationSelected(
                              const TarlaLocation(
                                latitude: 38.4237,
                                longitude: 27.1428,
                              ),
                            ),
                            child: const Text('Haritada konum sec'),
                          );
                        },
                  ),
                ),
              );
            },
            child: const Text('Konum secici ac'),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Konum secici ac'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Haritada konum sec'));
    await tester.pump();

    expect(find.text('Bu konumu kullan'), findsOneWidget);
    await tester.tap(find.text('Bu konumu kullan'));
    await tester.pumpAndSettle();

    expect(result, isNotNull);
    expect(result!.latitude, 38.4237);
    expect(result!.longitude, 27.1428);
  });
}

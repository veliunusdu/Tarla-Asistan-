import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
import 'package:mobile/features/location/presentation/field_location_picker_screen.dart';

void main() {
  testWidgets('disables confirmation until a map location is selected', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: FieldLocationPickerScreen(
          mapBuilder: (context, center, onLocationSelected, onLoadError) {
            return const SizedBox();
          },
        ),
      ),
    );

    final confirmButton = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'Bu konumu kullan'),
    );

    expect(confirmButton.onPressed, isNull);
  });

  testWidgets('renders a marker for an initial location', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: FieldLocationPickerScreen(
          initialLocation: TarlaLocation(latitude: 38.4237, longitude: 27.1428),
        ),
      ),
    );

    expect(find.byType(MarkerLayer), findsOneWidget);
    expect(find.byIcon(Icons.location_pin), findsOneWidget);
  });

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

  testWidgets('returns null when the picker is cancelled', (tester) async {
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
                        (context, center, onLocationSelected, onLoadError) =>
                            const SizedBox(),
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
    await tester.tap(find.byTooltip('Back'));
    await tester.pumpAndSettle();

    expect(result, isNull);
  });

  testWidgets('returns null when leaving the map load error screen', (
    tester,
  ) async {
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
                            onPressed: onLoadError,
                            child: const Text('Harita hatasi olustur'),
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
    await tester.tap(find.text('Harita hatasi olustur'));
    await tester.pump();

    expect(
      find.text('Harita yüklenemedi. Lütfen bağlantınızı kontrol edin.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Geri dön'));
    await tester.pumpAndSettle();

    expect(result, isNull);
  });
}

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/fields/data/tarla_location_repository.dart';
import 'package:mobile/features/location/data/location_service.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_konum_duzenleme_ekrani.dart';

class FakeTarlaLocationRepository implements TarlaLocationRepository {
  String? updatedId;
  TarlaLocation? updatedLocation;
  bool shouldThrow = false;
  int updateCallCount = 0;

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) async {
    updateCallCount++;
    if (shouldThrow) throw Exception('Update failed');
    updatedId = id;
    updatedLocation = location;
  }
}

class FakeLocationService implements LocationService {
  FakeLocationService({this.location, this.error});

  final TarlaLocation? location;
  final Object? error;

  @override
  Future<TarlaLocation> getCurrentLocation() async {
    if (error != null) throw error!;
    return location ??
        const TarlaLocation(latitude: 38.4237, longitude: 27.1428);
  }
}

void _setupScreen(WidgetTester tester) {
  tester.view.physicalSize = const Size(800, 1200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _buildApp({
  required Tarla tarla,
  required TarlaLocationRepository repo,
  LocationService? locationService,
  Future<TarlaLocation?> Function(BuildContext, TarlaLocation?)?
      locationPicker,
}) =>
    MaterialApp(
      theme: AppTheme.light,
      home: TarlaKonumDuzenlemeEkrani(
        tarla: tarla,
        repository: repo,
        locationService: locationService,
        locationPicker: locationPicker,
      ),
    );

void main() {
  final testTarla = Tarla(
    id: 'tarla-123',
    name: 'Deneme Tarlası',
    latitude: null,
    longitude: null,
    size: 12,
    cropType: 'Buğday',
    plantingDate: DateTime(2026, 1, 1),
  );

  testWidgets('tarla adını gösterir', (tester) async {
    _setupScreen(tester);
    final repo = FakeTarlaLocationRepository();
    await tester.pumpWidget(_buildApp(tarla: testTarla, repo: repo));

    expect(find.text('Deneme Tarlası'), findsOneWidget);
  });

  testWidgets('konum seçilmeden kaydetmeye izin vermez', (tester) async {
    _setupScreen(tester);
    final repo = FakeTarlaLocationRepository();
    await tester.pumpWidget(_buildApp(tarla: testTarla, repo: repo));

    final saveButton = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'Kaydet'),
    );
    expect(saveButton.onPressed, isNull);
  });

  testWidgets(
      'Konumumu kullan ile GPS konumu alınıp kaydedildiğinde updateTarlaLocation çağrılır ve ekran true döner',
      (tester) async {
    _setupScreen(tester);
    final repo = FakeTarlaLocationRepository();
    final locationService = FakeLocationService(
      location: const TarlaLocation(latitude: 39.9208, longitude: 32.8541),
    );

    bool? popResult;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.light,
        home: Builder(
          builder: (context) => ElevatedButton(
            onPressed: () async {
              popResult = await Navigator.push<bool>(
                context,
                MaterialPageRoute(
                  builder: (_) => TarlaKonumDuzenlemeEkrani(
                    tarla: testTarla,
                    repository: repo,
                    locationService: locationService,
                  ),
                ),
              );
            },
            child: const Text('Open'),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();

    expect(find.text('Deneme Tarlası'), findsOneWidget);

    await tester.tap(find.text('Konumumu kullan'));
    await tester.pumpAndSettle();

    expect(find.textContaining('39.9208'), findsOneWidget);

    final saveFinder = find.widgetWithText(ElevatedButton, 'Kaydet');
    final saveButton = tester.widget<ElevatedButton>(saveFinder);
    expect(saveButton.onPressed, isNotNull);

    await tester.tap(saveFinder);
    await tester.pumpAndSettle();

    expect(repo.updateCallCount, 1);
    expect(repo.updatedId, 'tarla-123');
    expect(repo.updatedLocation?.latitude, 39.9208);
    expect(repo.updatedLocation?.longitude, 32.8541);
    expect(popResult, isTrue);
  });

  testWidgets('Haritada seç ile konum seçilip kaydedilebilir', (tester) async {
    _setupScreen(tester);
    final repo = FakeTarlaLocationRepository();

    await tester.pumpWidget(
      _buildApp(
        tarla: testTarla,
        repo: repo,
        locationPicker: (ctx, initial) async =>
            const TarlaLocation(latitude: 41.0082, longitude: 28.9784),
      ),
    );

    await tester.tap(find.text('Haritada seç'));
    await tester.pumpAndSettle();

    expect(find.textContaining('41.0082'), findsOneWidget);

    await tester.tap(find.widgetWithText(ElevatedButton, 'Kaydet'));
    await tester.pumpAndSettle();

    expect(repo.updatedId, 'tarla-123');
    expect(repo.updatedLocation?.latitude, 41.0082);
    expect(repo.updatedLocation?.longitude, 28.9784);
  });
}

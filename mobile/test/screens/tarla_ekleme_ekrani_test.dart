import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/features/location/data/location_service.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';

// ---------------------------------------------------------------------------
// Fake repository
// ---------------------------------------------------------------------------

class FakeTarlaRepository implements TarlaRepository {
  FakeTarlaRepository({this.addCompleter, this.shouldThrow = false});

  final Completer<void>? addCompleter;
  final bool shouldThrow;

  Tarla? lastAdded;
  int addCallCount = 0;

  @override
  Future<List<Tarla>> getTarlalar() async => [];

  @override
  Future<void> addTarla(Tarla tarla) async {
    addCallCount++;
    lastAdded = tarla;
    if (shouldThrow) throw Exception('db error');
    if (addCompleter != null) return addCompleter!.future;
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
        const TarlaLocation(latitude: 39.92077, longitude: 32.85411);
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

void _setupScreen(WidgetTester tester) {
  tester.view.physicalSize = const Size(800, 1200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _buildApp(
  TarlaRepository repo, {
  LocationService? locationService,
  FieldLocationPicker? locationPicker,
}) => MaterialApp(
  theme: AppTheme.light,
  home: TarlaEklemeEkrani(
    repository: repo,
    locationService: locationService,
    locationPicker: locationPicker,
  ),
);

Future<void> _doldurForm(
  WidgetTester tester, {
  String ad = 'Kuzey Tarla',
  String boyut = '5.5',
  String urun = 'Buğday',
  bool tarihSec = true,
}) async {
  await tester.enterText(find.widgetWithText(TextFormField, 'Tarla Adı'), ad);
  await tester.enterText(
    find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
    boyut,
  );

  if (urun.isNotEmpty) {
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text(urun).last);
    await tester.pumpAndSettle();
  }

  if (tarihSec) {
    await tester.tap(find.text('Tarih seçin'));
    await tester.pumpAndSettle();
    // DatePicker'da bugünkü tarihi onayla
    await tester.tap(find.text('Tamam'));
    await tester.pumpAndSettle();
  }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('TarlaEklemeEkrani form doğrulama', () {
    testWidgets('boş tarla adı formu geçmez', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text('Tarla adı boş bırakılamaz.'), findsOneWidget);
      expect(repo.addCallCount, 0);
    });

    testWidgets('boş büyüklük formu geçmez', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Tarla Adı'),
        'Test',
      );
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text('Büyüklük boş bırakılamaz.'), findsOneWidget);
      expect(repo.addCallCount, 0);
    });

    testWidgets('parse edilemeyen büyüklük formu geçmez', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Tarla Adı'),
        'Test',
      );
      // Formatter rakam/virgül/nokta dışını bloklar; yalnızca nokta
      // sayısal dönüşümü başarısız kılar.
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
        '.',
      );
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text('Geçerli bir sayı girin.'), findsOneWidget);
      expect(repo.addCallCount, 0);
    });

    testWidgets('sıfır büyüklük formu geçmez', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Tarla Adı'),
        'Test',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
        '0',
      );
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text("Büyüklük 0'dan büyük olmalıdır."), findsOneWidget);
      expect(repo.addCallCount, 0);
    });

    testWidgets('virgüllü büyüklük güvenli parse edilir', (tester) async {
      _setupScreen(tester);
      final completer = Completer<void>()..complete();
      final repo = FakeTarlaRepository(addCompleter: completer);
      await tester.pumpWidget(_buildApp(repo));

      await _doldurForm(tester, boyut: '12,5');
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(repo.lastAdded?.size, 12.5);
    });

    testWidgets('ürün seçilmeden kayıt yapılmaz', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Tarla Adı'),
        'Test',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
        '5',
      );
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text('Lütfen bir ürün seçin.'), findsWidgets);
      expect(repo.addCallCount, 0);
    });

    testWidgets('tarih seçilmeden kayıt yapılmaz', (tester) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Tarla Adı'),
        'Test',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
        '5',
      );

      // Ürün seçiyoruz ama tarih seçmiyoruz
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Buğday').last);
      await tester.pumpAndSettle();

      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(repo.addCallCount, 0);
    });
  });

  group('TarlaEklemeEkrani kaydetme davranışı', () {
    testWidgets(
      'başarılı kayıtta doğru tarla gönderilir ve ekran true ile kapanır',
      (tester) async {
        _setupScreen(tester);
        final completer = Completer<void>()..complete();
        final repo = FakeTarlaRepository(addCompleter: completer);

        bool? popResult;
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.light,
            home: Navigator(
              onGenerateRoute: (settings) => MaterialPageRoute(
                builder: (_) => Scaffold(
                  body: Builder(
                    builder: (ctx) => ElevatedButton(
                      onPressed: () async {
                        popResult = await Navigator.push<bool>(
                          ctx,
                          MaterialPageRoute(
                            builder: (_) => TarlaEklemeEkrani(repository: repo),
                          ),
                        );
                      },
                      child: const Text('Aç'),
                    ),
                  ),
                ),
              ),
            ),
          ),
        );

        await tester.tap(find.text('Aç'));
        await tester.pumpAndSettle();

        await _doldurForm(tester);
        await tester.tap(find.text('Kaydet'));
        await tester.pump();
        await tester.pumpAndSettle();

        expect(repo.addCallCount, 1);
        expect(repo.lastAdded?.name, 'Kuzey Tarla');
        expect(repo.lastAdded?.cropType, 'Buğday');
        expect(popResult, isTrue);
      },
    );

    testWidgets('loading sırasında ikinci kayıt engellenir', (tester) async {
      _setupScreen(tester);
      final completer = Completer<void>();
      final repo = FakeTarlaRepository(addCompleter: completer);
      await tester.pumpWidget(_buildApp(repo));

      await _doldurForm(tester);

      // İlk kayıt — askıda kalır
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      // Buton pasif olmalı
      final button = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
      expect(button.onPressed, isNull);

      // İkinci tap denemesi
      await tester.tap(find.byType(ElevatedButton));
      await tester.pump();

      expect(repo.addCallCount, 1);

      completer.complete();
    });

    testWidgets('repository hatası kullanıcıya SnackBar gösterir', (
      tester,
    ) async {
      _setupScreen(tester);
      final repo = FakeTarlaRepository(shouldThrow: true);
      await tester.pumpWidget(_buildApp(repo));

      await _doldurForm(tester);
      await tester.tap(find.text('Kaydet'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));

      expect(
        find.text('Tarla kaydedilemedi. Lütfen tekrar deneyin.'),
        findsOneWidget,
      );
      // Ham exception mesajı gösterilmemeli
      expect(find.textContaining('Exception'), findsNothing);
    });
  });

  group('Tarla konumu seçimi ve görünürlüğü', () {
    testWidgets('Tarla konumu alanı, butonları ve Konumsuz devam et seçeneği görünür', (
      tester,
    ) async {
      final repo = FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(repo));

      expect(find.text('Tarla konumu'), findsOneWidget);
      expect(find.text('Konum seçilmedi'), findsOneWidget);
      expect(find.text('Konumumu kullan'), findsOneWidget);
      expect(find.text('Haritada seç'), findsOneWidget);
      expect(find.text('Konumsuz devam et'), findsOneWidget);

      // Tarla adı alanından sonra ve Büyüklük alanından önce olduğunu doğrula
      final nameFinder = find.widgetWithText(TextFormField, 'Tarla Adı');
      final locationFinder = find.text('Tarla konumu');
      final sizeFinder = find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)');

      final nameBottom = tester.getBottomLeft(nameFinder).dy;
      final locationTop = tester.getTopLeft(locationFinder).dy;
      final locationBottom = tester.getBottomLeft(locationFinder).dy;
      final sizeTop = tester.getTopLeft(sizeFinder).dy;

      expect(locationTop, greaterThanOrEqualTo(nameBottom));
      expect(sizeTop, greaterThanOrEqualTo(locationBottom));
    });

    testWidgets(
      'Konumsuz devam et form doğrulamalarını çalıştırır ve eksik alanda kaydetmez',
      (tester) async {
        final repo = FakeTarlaRepository();
        await tester.pumpWidget(_buildApp(repo));

        await tester.tap(find.text('Konumsuz devam et'));
        await tester.pump();

        expect(find.text('Tarla adı boş bırakılamaz.'), findsOneWidget);
        expect(repo.addCallCount, 0);
      },
    );

    testWidgets(
      'Konumsuz devam et ile kaydedildiğinde koordinatlar null gönderilir',
      (tester) async {
        final completer = Completer<void>()..complete();
        final repo = FakeTarlaRepository(addCompleter: completer);
        await tester.pumpWidget(_buildApp(repo));

        await _doldurForm(tester);
        await tester.tap(find.text('Konumsuz devam et'));
        await tester.pump();
        await tester.pumpAndSettle();

        expect(repo.addCallCount, 1);
        expect(repo.lastAdded?.latitude, isNull);
        expect(repo.lastAdded?.longitude, isNull);
      },
    );

    testWidgets(
      'Konum seçildiğinde koordinat özeti, Haritada değiştir ve Konumu kaldır gösterilir',
      (tester) async {
        final repo = FakeTarlaRepository();
        final locationService = FakeLocationService(
          location: const TarlaLocation(latitude: 38.4237, longitude: 27.1428),
        );
        await tester.pumpWidget(
          _buildApp(repo, locationService: locationService),
        );

        await tester.tap(find.text('Konumumu kullan'));
        await tester.pumpAndSettle();

        expect(find.text('38.42370, 27.14280'), findsOneWidget);
        expect(find.text('Haritada değiştir'), findsOneWidget);
        expect(find.text('Konumu kaldır'), findsOneWidget);

        // Konumu kaldırınca tekrar 'Konum seçilmedi' ve 'Haritada seç' döner
        await tester.tap(find.text('Konumu kaldır'));
        await tester.pumpAndSettle();

        expect(find.text('Konum seçilmedi'), findsOneWidget);
        expect(find.text('Haritada seç'), findsOneWidget);
      },
    );
  });
}


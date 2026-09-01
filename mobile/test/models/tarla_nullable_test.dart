import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/mappers/farm_mapper.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

class _EmptyFaaliyetRepo implements FaaliyetRepository {
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];
  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

Widget _wrap(Widget child) => MaterialApp(theme: AppTheme.light, home: child);

FarmResponseDto _dto({
  String id = 'farm-test',
  String name = 'Nullable Test Tarla',
  double? latitude = 39.5,
  double? longitude = 32.1,
  double? sizeInHectares = 8.0,
}) => FarmResponseDto(
  id: id,
  ownerId: 'owner',
  name: name,
  latitude: latitude,
  longitude: longitude,
  sizeInHectares: sizeInHectares,
  irrigationMethod: null,
  soilType: null,
  note: null,
  archivedAt: null,
  createdAt: DateTime.parse('2026-01-01T00:00:00Z'),
  updatedAt: DateTime.parse('2026-01-01T00:00:00Z'),
  currentCrop: null,
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  // -------------------------------------------------------------------------
  // 1. Tarla.fromJson — tüm alanlar dolu
  // -------------------------------------------------------------------------

  group('Senaryo 1: Tarla.fromJson tüm alanlar doluyken doğru parse eder', () {
    test('all fields are parsed correctly', () {
      final json = {
        'id': 'id-1',
        'name': 'Test',
        'latitude': 39.92,
        'longitude': 32.85,
        'size': 12.5,
        'cropType': 'Buğday',
        'plantingDate': '2026-03-15T00:00:00.000',
      };
      final t = Tarla.fromJson(json);

      expect(t.id, 'id-1');
      expect(t.name, 'Test');
      expect(t.latitude, 39.92);
      expect(t.longitude, 32.85);
      expect(t.size, 12.5);
      expect(t.cropType, 'Buğday');
      expect(t.plantingDate, DateTime.parse('2026-03-15T00:00:00.000'));
    });
  });

  // -------------------------------------------------------------------------
  // 2. Nullable alanlar null geldiğinde sahte değer üretilmez
  // -------------------------------------------------------------------------

  group('Senaryo 2: Nullable alanlar null olduğunda sahte değer üretmez', () {
    test('absent optional fields produce null — not 0.0 or empty string', () {
      final json = {'id': 'id-2', 'name': 'Sadece İsim'};
      final t = Tarla.fromJson(json);

      expect(t.latitude, isNull);
      expect(t.longitude, isNull);
      expect(t.size, isNull);
      expect(t.cropType, isNull);
      expect(t.plantingDate, isNull);
    });

    test('explicit null JSON values produce null fields', () {
      final json = {
        'id': 'id-3',
        'name': 'Null Değerler',
        'latitude': null,
        'longitude': null,
        'size': null,
        'cropType': null,
        'plantingDate': null,
      };
      final t = Tarla.fromJson(json);

      expect(t.latitude, isNull);
      expect(t.size, isNull);
      expect(t.cropType, isNull);
      expect(t.plantingDate, isNull);
    });
  });

  // -------------------------------------------------------------------------
  // 3. SQLite int/double sayıları güvenli parse edilir
  // -------------------------------------------------------------------------

  group('Senaryo 3: int/double SQLite sayıları güvenli parse edilir', () {
    test('integer literals are promoted to double', () {
      final json = {
        'id': 'id-4',
        'name': 'Int',
        'latitude': 39,
        'longitude': 32,
        'size': 10,
      };
      final t = Tarla.fromJson(json);

      expect(t.latitude, 39.0);
      expect(t.longitude, 32.0);
      expect(t.size, 10.0);
    });

    test('double values are parsed without loss', () {
      final json = {
        'id': 'id-5',
        'name': 'Double',
        'latitude': 39.12345,
        'longitude': 32.98765,
        'size': 7.5,
      };
      final t = Tarla.fromJson(json);

      expect(t.latitude, closeTo(39.12345, 0.00001));
      expect(t.size, 7.5);
    });
  });

  // -------------------------------------------------------------------------
  // 4. Null plantingDate parse hatası oluşturmaz
  // -------------------------------------------------------------------------

  group('Senaryo 4: Null plantingDate parse hatası oluşturmaz', () {
    test('null plantingDate value does not throw', () {
      final json = {'id': 'id-6', 'name': 'Tarihi Yok', 'plantingDate': null};
      expect(() => Tarla.fromJson(json), returnsNormally);
      expect(Tarla.fromJson(json).plantingDate, isNull);
    });

    test('absent plantingDate key does not throw', () {
      final json = {'id': 'id-7', 'name': 'Key Yok'};
      expect(() => Tarla.fromJson(json), returnsNormally);
    });
  });

  // -------------------------------------------------------------------------
  // 5. toJson null değerleri güvenli taşır
  // -------------------------------------------------------------------------

  group('Senaryo 5: toJson null değerleri güvenli taşır', () {
    test('null fields are written as null — not 0 or empty string', () {
      const t = Tarla(id: 'id-8', name: 'Sadece Zorunlu');
      final json = t.toJson();

      expect(json['latitude'], isNull);
      expect(json['longitude'], isNull);
      expect(json['size'], isNull);
      expect(json['cropType'], isNull);
      expect(json['plantingDate'], isNull);
    });

    test('non-null fields are serialised correctly', () {
      final t = Tarla(
        id: 'id-9',
        name: 'Tam',
        latitude: 39.0,
        longitude: 32.0,
        size: 5.0,
        cropType: 'Buğday',
        plantingDate: DateTime(2026, 3, 15),
      );
      final json = t.toJson();

      expect(json['latitude'], 39.0);
      expect(json['size'], 5.0);
      expect(json['cropType'], 'Buğday');
      expect(json['plantingDate'] as String, contains('2026-03-15'));
    });
  });

  // -------------------------------------------------------------------------
  // 6. FarmMapper nullable FarmResponse'u geçerli Tarla'ya dönüştürür
  // -------------------------------------------------------------------------

  group('Senaryo 6: FarmMapper nullable FarmResponse geçerli Tarla üretir', () {
    test('all optional fields null → valid Tarla with null fields', () {
      final tarla = FarmMapper.fromDto(
        _dto(latitude: null, longitude: null, sizeInHectares: null),
      );

      expect(tarla.id, 'farm-test');
      expect(tarla.latitude, isNull);
      expect(tarla.size, isNull);
    });

    test('full optional fields → Tarla with correct values', () {
      final tarla = FarmMapper.fromDto(_dto());

      expect(tarla.latitude, 39.5);
      expect(tarla.size, 8.0);
    });
  });

  // -------------------------------------------------------------------------
  // 7. currentCrop null olduğunda ürün ve tarih null kalır
  // -------------------------------------------------------------------------

  group(
    'Senaryo 7: currentCrop null olduğunda cropType ve plantingDate null',
    () {
      test('null currentCrop produces null cropType and plantingDate', () {
        final tarla = FarmMapper.fromDto(_dto()); // currentCrop: null in _dto

        expect(tarla.cropType, isNull);
        expect(tarla.plantingDate, isNull);
      });
    },
  );

  // -------------------------------------------------------------------------
  // 8. Backend listesindeki nullable kayıt mapper yüzünden kaybolmaz
  // -------------------------------------------------------------------------

  group(
    'Senaryo 8: Backend listesindeki nullable kayıt mapper yüzünden kaybolmaz',
    () {
      test(
        'fromDtoList retains ALL DTOs including those with all-null optionals',
        () {
          final dtos = [
            _dto(
              id: 'f1',
              latitude: 38.0,
              longitude: 27.0,
              sizeInHectares: 5.0,
            ),
            _dto(
              id: 'f2',
              latitude: null,
              longitude: null,
              sizeInHectares: null,
            ),
            _dto(
              id: 'f3',
              latitude: 39.0,
              longitude: 32.0,
              sizeInHectares: null,
            ),
          ];

          final result = FarmMapper.fromDtoList(dtos);

          expect(result, hasLength(3));
          expect(result.map((t) => t.id), containsAll(['f1', 'f2', 'f3']));
          expect(result[1].latitude, isNull);
        },
      );
    },
  );

  // -------------------------------------------------------------------------
  // 9. Dashboard null size değerlerini toplama katmaz
  // -------------------------------------------------------------------------

  group('Senaryo 9: Dashboard null size değerlerini toplam alana eklemez', () {
    test('null size is skipped in fold — no NullPointerEquivalent', () {
      final tarlalar = [
        Tarla(id: '1', name: 'A', size: 10.0),
        Tarla(id: '2', name: 'B'), // size null
        Tarla(id: '3', name: 'C', size: 5.0),
      ];

      final toplam = tarlalar.fold<double>(0, (s, t) => s + (t.size ?? 0.0));

      expect(toplam, 15.0);
    });

    test('all null sizes sum to 0.0', () {
      final tarlalar = [Tarla(id: '1', name: 'A'), Tarla(id: '2', name: 'B')];
      final toplam = tarlalar.fold<double>(0, (s, t) => s + (t.size ?? 0.0));

      expect(toplam, 0.0);
    });
  });

  // -------------------------------------------------------------------------
  // 10. Tarla detay nullable alanlarda açıklayıcı metin gösterir
  // -------------------------------------------------------------------------

  group(
    'Senaryo 10: Tarla detay nullable alanlarda açıklayıcı metin gösterir',
    () {
      testWidgets('null cropType/size/plantingDate show fallback labels', (
        tester,
      ) async {
        final tarla = Tarla(
          id: 'detay-1',
          name: 'Eksik Bilgili Tarla',
          latitude: 38.0,
          longitude: 27.0,
        );
        await tester.pumpWidget(
          _wrap(
            TarlaDetayEkrani(
              tarla: tarla,
              faaliyetRepository: _EmptyFaaliyetRepo(),
            ),
          ),
        );

        expect(find.text('Ürün bilgisi yok'), findsOneWidget);
        expect(find.text('Alan bilgisi yok'), findsOneWidget);
        expect(find.text('Ekim tarihi yok'), findsOneWidget);
      });
    },
  );

  // -------------------------------------------------------------------------
  // 11. Legacy 0.0,0.0 ve null koordinat → "Konum eklenmedi"
  // -------------------------------------------------------------------------

  group('Senaryo 11: "Konum eklenmedi" doğru tetiklenir', () {
    testWidgets('legacy 0.0, 0.0 shows "Konum eklenmedi"', (tester) async {
      final tarla = Tarla(
        id: 'legacy-1',
        name: 'Eski Kayıt',
        latitude: 0.0,
        longitude: 0.0,
        size: 5.0,
        cropType: 'Buğday',
        plantingDate: DateTime(2024),
      );
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: tarla,
            faaliyetRepository: _EmptyFaaliyetRepo(),
          ),
        ),
      );

      expect(find.text('Konum eklenmedi'), findsOneWidget);
    });

    testWidgets('null latitude shows "Konum eklenmedi"', (tester) async {
      final tarla = Tarla(id: 'null-lat', name: 'Konumsuz');
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: tarla,
            faaliyetRepository: _EmptyFaaliyetRepo(),
          ),
        ),
      );

      expect(find.text('Konum eklenmedi'), findsOneWidget);
    });

    testWidgets('valid coordinates are shown correctly', (tester) async {
      final tarla = Tarla(
        id: 'coords',
        name: 'Koordinatlı',
        latitude: 39.12345,
        longitude: 32.98765,
        size: 3.0,
        cropType: 'Mısır',
        plantingDate: DateTime(2025),
      );
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: tarla,
            faaliyetRepository: _EmptyFaaliyetRepo(),
          ),
        ),
      );

      expect(find.textContaining('39.12345'), findsOneWidget);
      expect(find.text('Konum eklenmedi'), findsNothing);
    });
  });

  // -------------------------------------------------------------------------
  // 12. SQLite null round-trip via toJson / fromJson
  // -------------------------------------------------------------------------

  group('Senaryo 12: SQLite null tarla alanları yazılıp okunabilir', () {
    test('toJson then fromJson preserves null fields', () {
      const original = Tarla(id: 'rt-1', name: 'Round Trip');
      final restored = Tarla.fromJson(original.toJson());

      expect(restored.id, original.id);
      expect(restored.latitude, isNull);
      expect(restored.size, isNull);
      expect(restored.cropType, isNull);
      expect(restored.plantingDate, isNull);
    });

    test('non-null toJson then fromJson preserves values', () {
      final original = Tarla(
        id: 'rt-2',
        name: 'Dolu',
        latitude: 38.7,
        longitude: 35.4,
        size: 12.5,
        cropType: 'Buğday',
        plantingDate: DateTime(2026, 3, 15),
      );
      final restored = Tarla.fromJson(original.toJson());

      expect(restored.latitude, 38.7);
      expect(restored.size, 12.5);
      expect(restored.cropType, 'Buğday');
      expect(restored.plantingDate?.day, 15);
    });
  });

  // -------------------------------------------------------------------------
  // 13. Mevcut dolu kayıtların davranışı bozulmaz
  // -------------------------------------------------------------------------

  group('Senaryo 13: Mevcut dolu Tarla kayıtlarının davranışı bozulmaz', () {
    test('Tarla with all fields constructed and accessed correctly', () {
      final t = Tarla(
        id: 'full-1',
        name: 'Tam Kayıt',
        latitude: 38.7,
        longitude: 35.4,
        size: 12.5,
        cropType: 'WHEAT',
        plantingDate: DateTime.utc(2026, 8, 1),
      );

      expect(t.latitude, 38.7);
      expect(t.size, 12.5);
      expect(t.cropType, 'WHEAT');
      expect(t.plantingDate, DateTime.utc(2026, 8, 1));
    });

    test('full Tarla serialises and deserialises without data loss', () {
      final original = Tarla(
        id: 'full-2',
        name: 'Tam JSON',
        latitude: 39.5,
        longitude: 32.1,
        size: 7.0,
        cropType: 'Buğday',
        plantingDate: DateTime(2026, 4, 10),
      );
      final restored = Tarla.fromJson(original.toJson());

      expect(restored.latitude, original.latitude);
      expect(restored.size, original.size);
      expect(restored.cropType, original.cropType);
      expect(restored.plantingDate?.year, 2026);
      expect(restored.plantingDate?.month, 4);
    });
  });

  // -------------------------------------------------------------------------
  // 14. Ekranlar 320×640 boyutunda overflow oluşturmaz
  // -------------------------------------------------------------------------

  group('Senaryo 14: TarlaDetayEkrani 320×640 overflow oluşturmaz', () {
    testWidgets('null fields — no overflow at 320×640', (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final tarla = Tarla(id: 'ov-1', name: 'Küçük Ekran Tarla');
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: tarla,
            faaliyetRepository: _EmptyFaaliyetRepo(),
          ),
        ),
      );

      expect(tester.takeException(), isNull);
    });

    testWidgets('full fields — no overflow at 320×640', (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final tarla = Tarla(
        id: 'ov-2',
        name: 'Uzun İsimli Tarla Ki Overflow Test Edilsin',
        latitude: 39.12345,
        longitude: 32.98765,
        size: 999.0,
        cropType: 'Buğday',
        plantingDate: DateTime(2026, 3, 15),
      );
      await tester.pumpWidget(
        _wrap(
          TarlaDetayEkrani(
            tarla: tarla,
            faaliyetRepository: _EmptyFaaliyetRepo(),
          ),
        ),
      );

      expect(tester.takeException(), isNull);
    });
  });
}

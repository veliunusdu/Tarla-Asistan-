import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/mappers/farm_mapper.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';
import 'package:mobile/screens/tarla_ekleme_ekrani.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class _FakeTarlaRepository implements TarlaRepository {
  _FakeTarlaRepository({this.addCompleter});

  final Completer<void>? addCompleter;
  Tarla? lastAdded;
  int addCallCount = 0;

  @override
  Future<List<Tarla>> getTarlalar() async => [];

  @override
  Future<void> addTarla(Tarla tarla) async {
    addCallCount++;
    lastAdded = tarla;
    if (addCompleter != null) return addCompleter!.future;
  }
}

class _FakeFaaliyetRepository
    implements
        FaaliyetRepository,
        FaaliyetDeleteRepository,
        PlanliGorevRepository,
        PlanliGorevCompletionRepository {
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async {}

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => [];

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => [];

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async {}

  @override
  Future<void> completePlanliGorev(String id, {String? note}) async {}

  @override
  Future<void> deleteFaaliyet(String id) async {}

  @override
  Future<void> markAsCompleted(String id) async {}
}

void _setupScreen(WidgetTester tester) {
  tester.view.physicalSize = const Size(800, 1200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _buildApp(Widget screen) => MaterialApp(
      theme: AppTheme.light,
      home: screen,
    );

void main() {
  group('Farmer Crop Free-Text UI & Flow Tests (Prompt #24)', () {
    // 13. Tarla formunda crop dropdown yok
    testWidgets('13. Tarla formunda crop DropdownButtonFormField bulunmaz',
        (tester) async {
      _setupScreen(tester);
      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    });

    // 14. Hardcoded ürün seçenekleri yok
    testWidgets('14. Çiftçiye hardcoded/predefined ürün seçenekleri sunulmaz',
        (tester) async {
      _setupScreen(tester);
      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      // Formda doğrudan sabit seçenek butonları/çipler/dropdown elemanları sunulmaz
      expect(find.text('Buğday'), findsNothing);
      expect(find.text('Arpa'), findsNothing);
      expect(find.text('Mısır'), findsNothing);
      expect(find.text('Ayçiçeği'), findsNothing);
      expect(find.text('Domates'), findsNothing);
    });

    // 15. "Diğer" yok
    testWidgets('15. Seçenek listesinde "Diğer" öğesi bulunmaz',
        (tester) async {
      _setupScreen(tester);
      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      expect(find.text('Diğer'), findsNothing);
    });

    // 16. Crop TextFormField var
    testWidgets('16. Ürün için serbest metin TextFormField bulunur',
        (tester) async {
      _setupScreen(tester);
      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      final cropField = find.widgetWithText(TextFormField, 'Ürün');
      expect(cropField, findsOneWidget);
      expect(find.text('Bu tarlada yetiştirdiğiniz ürünü yazın'), findsOneWidget);
    });

    // 17. "Nohut" girilip gönderilebilir
    testWidgets('17. "Nohut" yazılıp başarıyla kaydedilebilir', (tester) async {
      _setupScreen(tester);
      final completer = Completer<void>()..complete();
      final repo = _FakeTarlaRepository(addCompleter: completer);
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Tarla Adı'), 'Kuzey Tarlası');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'), '15');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Ürün'), 'Nohut');

      await tester.tap(find.text('Tarih seçin'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Tamam'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.text('Kaydet'));
      await tester.tap(find.text('Kaydet'));
      await tester.pumpAndSettle();

      expect(repo.addCallCount, 1);
      expect(repo.lastAdded?.cropType, 'Nohut');
    });

    // 18. "Zeytin" girilip gönderilebilir
    testWidgets('18. "Zeytin" yazılıp başarıyla kaydedilebilir', (tester) async {
      _setupScreen(tester);
      final completer = Completer<void>()..complete();
      final repo = _FakeTarlaRepository(addCompleter: completer);
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Tarla Adı'), 'Zeytinlik');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'), '30');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Ürün'), 'Zeytin');

      await tester.tap(find.text('Tarih seçin'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Tamam'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.text('Kaydet'));
      await tester.tap(find.text('Kaydet'));
      await tester.pumpAndSettle();

      expect(repo.addCallCount, 1);
      expect(repo.lastAdded?.cropType, 'Zeytin');
    });

    // 19. Whitespace trim edilir
    testWidgets('19. Başında ve sonunda boşluk olan "   Nohut   " trim edilir',
        (tester) async {
      _setupScreen(tester);
      final completer = Completer<void>()..complete();
      final repo = _FakeTarlaRepository(addCompleter: completer);
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Tarla Adı'), 'Batı Tarlası');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'), '10');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Ürün'), '   Nohut   ');

      await tester.tap(find.text('Tarih seçin'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Tamam'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.text('Kaydet'));
      await tester.tap(find.text('Kaydet'));
      await tester.pumpAndSettle();

      expect(repo.addCallCount, 1);
      expect(repo.lastAdded?.cropType, 'Nohut');
    });

    // 20. Empty validation
    testWidgets('20. Boş veya sadece boşluk içeren ürün adı hata verir',
        (tester) async {
      _setupScreen(tester);
      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(repository: repo)));

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Tarla Adı'), 'Tarla');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'), '5');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Ürün'), '    ');

      await tester.ensureVisible(find.text('Kaydet'));
      await tester.tap(find.text('Kaydet'));
      await tester.pump();

      expect(find.text('Ürün adı boş bırakılamaz.'), findsOneWidget);
      expect(repo.addCallCount, 0);
    });

    // 21. Tarla detayında custom crop görünür
    testWidgets('21. Tarla detay ekranında özel ürün adı (Nohut) görüntülenir',
        (tester) async {
      _setupScreen(tester);
      final tarla = Tarla(
        id: 't-custom',
        name: 'Güneş Tarlası',
        size: 20,
        cropType: 'Nohut',
        plantingDate: DateTime(2026, 3, 1),
      );

      await tester.pumpWidget(_buildApp(TarlaDetayEkrani(
        tarla: tarla,
        faaliyetRepository: _FakeFaaliyetRepository(),
      )));
      await tester.pumpAndSettle();

      expect(find.text('Nohut'), findsOneWidget);
      expect(find.text('Other'), findsNothing);
      expect(find.text('Diğer'), findsNothing);
    });

    // 22. Eski farm crop_type fallback'i doğru görünür
    testWidgets('22. Eski legacy WHEAT kaydı mobilde "Buğday" olarak çözümlenir',
        (tester) async {
      _setupScreen(tester);
      final legacyDto = FarmResponseDto.fromJson({
        'id': 'f-legacy',
        'owner_id': 'user-1',
        'name': 'Eski Buğday Tarlası',
        'created_at': '2026-03-15T00:00:00Z',
        'current_crop': {
          'id': 'cp-1',
          'farm_id': 'f-legacy',
          'crop_type': 'WHEAT',
          'variety': null,
          'planted_at': '2026-03-15',
          'harvested_at': null,
          'status': 'ACTIVE',
          'created_at': '2026-03-15T00:00:00Z',
          'updated_at': '2026-03-15T00:00:00Z',
        },
      });

      final tarla = FarmMapper.fromDto(legacyDto);
      expect(tarla.cropType, 'Buğday');

      await tester.pumpWidget(_buildApp(TarlaDetayEkrani(
        tarla: tarla,
        faaliyetRepository: _FakeFaaliyetRepository(),
      )));
      await tester.pumpAndSettle();

      expect(find.text('Buğday'), findsOneWidget);
      expect(find.text('WHEAT'), findsNothing);
    });

    // 23. Uzun crop name UI overflow yapmaz
    testWidgets('23. Çok uzun ürün adı tarla detayında overflow oluşturmaz',
        (tester) async {
      _setupScreen(tester);
      final tarla = Tarla(
        id: 't-long',
        name: 'Ova Tarlası',
        size: 50,
        cropType:
            'Sertifikalı Kırmızı Ekmeklik Ata Tohumu Buğdayı - Yerel Varyete',
        plantingDate: DateTime(2026, 2, 20),
      );

      await tester.pumpWidget(_buildApp(TarlaDetayEkrani(
        tarla: tarla,
        faaliyetRepository: _FakeFaaliyetRepository(),
      )));
      await tester.pumpAndSettle();

      expect(
          find.text(
              'Sertifikalı Kırmızı Ekmeklik Ata Tohumu Buğdayı - Yerel Varyete'),
          findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    // 24. Edit/season flow doğru çalışır
    testWidgets(
        '24. Düzenleme ekranında mevcut ürün salt-okunur gösterilir, overwrite edilmez',
        (tester) async {
      _setupScreen(tester);
      final tarla = Tarla(
        id: 't-edit',
        name: 'Eski Tarla',
        size: 20,
        cropType: 'Nohut',
        plantingDate: DateTime(2026, 3, 1),
      );

      final repo = _FakeTarlaRepository();
      await tester.pumpWidget(_buildApp(TarlaEklemeEkrani(
        editingTarla: tarla,
        repository: repo,
      )));

      // Düzenleme modunda ürün TextFormField olarak değil, kilitli container olarak gösterilir
      expect(find.text('Nohut'), findsOneWidget);
      // Ürün başlığı altında TextFormField bulunmaz (sadece Tarla Adı ve Büyüklük düzenlenir)
      expect(find.widgetWithText(TextFormField, 'Tarla Adı'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Büyüklük (Dönüm)'),
          findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Ürün'), findsNothing);
    });
  });
}

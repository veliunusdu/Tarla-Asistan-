import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/features/weather/data/weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/notification_target.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/ana_sayfa_ekrani.dart';
import 'package:mobile/screens/faaliyet_ekleme_ekrani.dart';
import 'package:mobile/screens/notification_target_screen.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';
import 'package:mobile/screens/tarla_gunlugu_ekrani.dart';
import 'package:mobile/services/api_client.dart';

class _FakeTarlaRepo implements TarlaRepository {
  _FakeTarlaRepo(this.tarlalar);
  final List<Tarla> tarlalar;
  @override
  Future<List<Tarla>> getTarlalar() async => tarlalar;
  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class _FakeWeatherRepo implements WeatherRepository {
  const _FakeWeatherRepo();
  @override
  Future<WeatherSummary> getWeather({String? farmId}) async =>
      const WeatherSummary(temperature: 24, description: 'Güneşli');
}

class _FakeFaaliyetRepo
    implements
        FaaliyetRepository,
        PlanliGorevRepository,
        FaaliyetDeleteRepository,
        PlanliGorevCompletionRepository {
  final List<Faaliyet> faaliyetler = [];
  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async =>
      faaliyetler.where((f) => f.tarlaId == tarlaId).toList();
  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) async =>
      faaliyetler.add(faaliyet);
  @override
  Future<List<Faaliyet>> getTumFaaliyetler() async => faaliyetler;
  @override
  Future<void> addPlanliGorev(Faaliyet gorev) async => faaliyetler.add(gorev);
  @override
  Future<List<Faaliyet>> getPlanliGorevler() async =>
      faaliyetler.where((f) => !f.isCompleted).toList();
  @override
  Future<void> completePlanliGorev(String id, {String? note}) async {}
  @override
  Future<void> markAsCompleted(String id) async {}
  @override
  Future<void> deleteFaaliyet(String id) async =>
      faaliyetler.removeWhere((f) => f.id == id);
}

void main() {
  final testTarla = Tarla(
    id: 'tarla-1',
    name: 'Deneme Tarlası',
    cropType: 'Buğday',
    size: 20.0,
  );

  group('User Terminology Audit Tests (No legacy Görev/Faaliyet/İşlem UI)', () {
    testWidgets('AnaSayfaEkrani does not show legacy terminology', (
      tester,
    ) async {
      final tarlaRepo = _FakeTarlaRepo([testTarla]);
      final faaliyetRepo = _FakeFaaliyetRepo();

      await tester.pumpWidget(
        MaterialApp(
          home: AnaSayfaEkrani(
            tarlaRepository: tarlaRepo,
            faaliyetRepository: faaliyetRepo,
            weatherRepository: const _FakeWeatherRepo(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Legacy terms must NOT exist in UI
      expect(find.text('İşlem Kaydı Ekle'), findsNothing);
      expect(find.text('Tüm Faaliyetler'), findsNothing);
      expect(find.text('Yaklaşan Görevler'), findsNothing);
      expect(find.text('Faaliyet Planla'), findsNothing);
      expect(find.text('Görev Ekle'), findsNothing);
      expect(find.text('Faaliyet Ekle'), findsNothing);

      // New unified terms must exist
      expect(find.text('İş Ekle'), findsOneWidget);
      expect(find.text('Yaklaşan İşler'), findsOneWidget);
      expect(find.text('İş Planım'), findsOneWidget);
    });

    testWidgets('TarlaGunluguEkrani does not show legacy terminology', (
      tester,
    ) async {
      final tarlaRepo = _FakeTarlaRepo([testTarla]);
      final faaliyetRepo = _FakeFaaliyetRepo();

      await tester.pumpWidget(
        MaterialApp(
          home: TarlaGunluguEkrani(
            tarlaRepository: tarlaRepo,
            faaliyetRepository: faaliyetRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Görev Ekle'), findsNothing);
      expect(find.text('Faaliyet Ekle'), findsNothing);
      expect(find.text('İşlem Kaydı Ekle'), findsNothing);

      // Floating Action Button must be 'İş Ekle'
      expect(find.text('İş Ekle'), findsOneWidget);
      expect(find.text('İş Planım'), findsOneWidget);
    });

    testWidgets('TarlaDetayEkrani does not show legacy terminology', (
      tester,
    ) async {
      final faaliyetRepo = _FakeFaaliyetRepo();

      await tester.pumpWidget(
        MaterialApp(
          home: TarlaDetayEkrani(
            tarla: testTarla,
            faaliyetRepository: faaliyetRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Görev Ekle'), findsNothing);
      expect(find.text('Faaliyet Ekle'), findsNothing);
      expect(find.text('İşlem Kaydı Ekle'), findsNothing);

      // FAB and Tabs must use unified terminology
      expect(find.text('İş Ekle'), findsOneWidget);
      expect(find.text('Planlanan'), findsOneWidget);
      expect(find.text('Geçmiş'), findsOneWidget);
    });

    testWidgets('FaaliyetEklemeEkrani displays İş Ekle, Planla, Yapıldı', (
      tester,
    ) async {
      final faaliyetRepo = _FakeFaaliyetRepo();

      await tester.pumpWidget(
        MaterialApp(
          home: FaaliyetEklemeEkrani(
            tarlaId: testTarla.id,
            faaliyetRepository: faaliyetRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Screen title must be 'İş Ekle'
      expect(find.text('İş Ekle'), findsOneWidget);
      expect(find.text('Planla'), findsOneWidget);
      expect(find.text('Yapıldı'), findsOneWidget);

      expect(find.text('Görev Ekle'), findsNothing);
      expect(find.text('Faaliyet Ekle'), findsNothing);
    });

    testWidgets('NotificationTargetScreen displays İş Detayı instead of Görev Detayı', (
      tester,
    ) async {
      final client = ApiClient(
        httpClient: MockClient((request) async {
          return http.Response(
            '{"title": "Sulama Vakti", "status": "PLANNED", "priority": "HIGH", "due_date": "2026-09-05", "description": "Tarlayı sula", "reason": "Kuraklık"}',
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
        idTokenProvider: () async => 'dummy-token',
      );

      final target = NotificationTarget(
        type: NotificationTargetType.task,
        resourceId: 'task-123',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: NotificationTargetScreen(
            target: target,
            apiClient: client,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('İş Detayı'), findsOneWidget);
      expect(find.text('Görev detayı'), findsNothing);
      expect(find.text('Görev Detayı'), findsNothing);
    });
  });
}

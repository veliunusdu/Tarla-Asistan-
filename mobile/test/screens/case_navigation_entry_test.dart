import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/activities/data/local_faaliyet_repository.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/vaka_detay_ekrani.dart';
import 'package:mobile/features/cases/presentation/vaka_listesi_ekrani.dart';
import 'package:mobile/features/fields/data/local_tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/notification_target.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/notification_target_screen.dart';
import 'package:mobile/screens/profil_ekrani.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';
import 'package:mobile/services/api_client.dart';

class MockLocalFaaliyetRepository extends LocalFaaliyetRepository {
  const MockLocalFaaliyetRepository();

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async => [];

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => [];
}

class MockCaseNavRepository implements CaseRepository {
  @override
  Future<String> createCase(CreateCaseInput input) async => 'id';

  @override
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status}) async => [];

  @override
  Future<CaseDetail> getCaseById(String caseId) async {
    return CaseDetail(
      id: caseId,
      farmId: 'f-1',
      farmName: 'Zeytinlik',
      category: CaseCategory.pest,
      status: CaseStatus.open,
      title: 'Test Vaka',
      description: 'Test',
      createdAt: DateTime.now(),
      messages: [],
    );
  }

  @override
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  }) async {
    throw UnimplementedError();
  }

  @override
  Future<Map<String, String>> getAuthHeaders() async => {};
}

void main() {
  testWidgets('TarlaDetayEkrani provides Bildirimleri Gör button that opens VakaListesiEkrani', (tester) async {
    final tarla = Tarla(
      id: 'tarla-42',
      name: 'Zeytinlik',
      size: 5.0,
      cropType: 'Zeytin',
      plantingDate: DateTime(2025, 1, 1),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: TarlaDetayEkrani(
          tarla: tarla,
          faaliyetRepository: const MockLocalFaaliyetRepository(),
          tarlaRepository: const LocalTarlaRepository(),
          caseRepository: MockCaseNavRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final button = find.text('Bildirimleri Gör');
    expect(button, findsOneWidget);

    await tester.tap(button);
    await tester.pumpAndSettle();

    expect(find.byType(VakaListesiEkrani), findsOneWidget);
  });

  testWidgets('ProfilEkrani provides Sorun Bildirimlerim menu item that opens VakaListesiEkrani', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ProfilEkrani(
          caseRepository: MockCaseNavRepository(),
          tarlaRepository: const LocalTarlaRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final menuItem = find.text('Sorun Bildirimlerim');
    expect(menuItem, findsOneWidget);

    await tester.tap(menuItem);
    await tester.pumpAndSettle();

    expect(find.byType(VakaListesiEkrani), findsOneWidget);
  });

  testWidgets('NotificationTargetScreen renders VakaDetayEkrani when target is supportCase', (tester) async {
    final client = ApiClient(
      httpClient: MockClient((request) async => http.Response('{}', 200)),
      idTokenProvider: () async => 'dummy-token',
    );

    await tester.pumpWidget(
      MaterialApp(
        home: NotificationTargetScreen(
          target: NotificationTarget(
            type: NotificationTargetType.supportCase,
            resourceId: 'case-99',
          ),
          apiClient: client,
          caseRepository: MockCaseNavRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(VakaDetayEkrani), findsOneWidget);
  });
}

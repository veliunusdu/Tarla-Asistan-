import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/vaka_detay_ekrani.dart';
import 'package:mobile/models/notification_target.dart';
import 'package:mobile/screens/notification_target_screen.dart';
import 'package:mobile/services/api_client.dart';

class _MockCaseRepo implements CaseRepository {
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
      title: 'Zeytin Güvesi',
      description: 'Yapraklarda hasar var',
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
  testWidgets('NotificationTargetScreen renders task content for task target', (
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
      idTokenProvider: () async => 'token',
    );

    final target = NotificationTarget(
      type: NotificationTargetType.task,
      resourceId: 'task-1',
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
    expect(find.text('Sulama Vakti'), findsOneWidget);
    expect(find.text('Tarlayı sula'), findsOneWidget);
  });

  testWidgets('NotificationTargetScreen renders VakaDetayEkrani for supportCase target', (
    tester,
  ) async {
    final client = ApiClient(
      httpClient: MockClient((request) async => http.Response('{}', 200)),
      idTokenProvider: () async => 'token',
    );

    final target = NotificationTarget(
      type: NotificationTargetType.supportCase,
      resourceId: 'case-123',
    );

    await tester.pumpWidget(
      MaterialApp(
        home: NotificationTargetScreen(
          target: target,
          apiClient: client,
          caseRepository: _MockCaseRepo(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(VakaDetayEkrani), findsOneWidget);
    expect(find.text('Zeytin Güvesi'), findsOneWidget);
  });

  testWidgets('NotificationTargetScreen renders proactive advisory content', (
    tester,
  ) async {
    final client = ApiClient(
      httpClient: MockClient((request) async {
        expect(request.url.path, '/api/v1/ai/advisories');
        expect(request.url.queryParameters['farm_id'], 'farm-1');
        return http.Response(
          '[{"id":"advisory-1","title":"Sulamayı erteleyin","summary":"Yarın yağış bekleniyor","severity":"Warning","action_recommendation":"Sulamayı yağış sonrasına bırakın"}]',
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        );
      }),
      idTokenProvider: () async => 'token',
    );

    await tester.pumpWidget(
      MaterialApp(
        home: NotificationTargetScreen(
          target: const NotificationTarget(
            type: NotificationTargetType.advisory,
            resourceId: 'advisory-1',
            farmId: 'farm-1',
          ),
          apiClient: client,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('AI Tarla Uyarısı'), findsOneWidget);
    expect(find.text('Sulamayı erteleyin'), findsOneWidget);
    expect(find.text('Yarın yağış bekleniyor'), findsOneWidget);
  });
}

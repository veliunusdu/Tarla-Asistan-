import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/activities/data/faaliyet_repository.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/sorun_bildir_ekrani.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/screens/tarla_detay_ekrani.dart';

class DummyCaseRepo implements CaseRepository {
  @override
  Future<String> createCase(CreateCaseInput input) async => 'case-id';

  @override
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status}) async => [];

  @override
  Future<CaseDetail> getCaseById(String caseId) => throw UnimplementedError();

  @override
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  }) =>
      throw UnimplementedError();
}

class DummyFaaliyetRepo implements FaaliyetRepository {
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

class DummyTarlaRepo implements TarlaRepository {
  @override
  Future<List<Tarla>> getTarlalar() async => [];
  @override
  Future<void> addTarla(Tarla tarla) async {}
}

void main() {
  testWidgets(
    'TarlaDetayEkrani shows Sorun Bildir action button and navigates to SorunBildirEkrani',
    (tester) async {
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
            faaliyetRepository: DummyFaaliyetRepo(),
            tarlaRepository: DummyTarlaRepo(),
            caseRepository: DummyCaseRepo(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final button = find.text('Sorun Bildir');
      expect(button, findsOneWidget);

      await tester.tap(button);
      await tester.pumpAndSettle();

      expect(find.byType(SorunBildirEkrani), findsOneWidget);
    },
  );
}

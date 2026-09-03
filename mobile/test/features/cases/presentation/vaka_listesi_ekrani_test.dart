import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/sorun_bildir_ekrani.dart';
import 'package:mobile/features/cases/presentation/vaka_detay_ekrani.dart';
import 'package:mobile/features/cases/presentation/vaka_listesi_ekrani.dart';
import 'package:mobile/features/fields/data/local_tarla_repository.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';

class MockCaseRepository implements CaseRepository {
  MockCaseRepository({this.cases = const [], this.shouldThrow = false});
  final List<CaseSummary> cases;
  final bool shouldThrow;

  @override
  Future<String> createCase(CreateCaseInput input) async => 'created-id';

  @override
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status}) async {
    if (shouldThrow) {
      throw Exception('Failed to load cases');
    }
    return cases.where((c) {
      if (farmId != null && c.farmId != farmId) return false;
      if (status != null && c.status != status) return false;
      return true;
    }).toList();
  }

  @override
  Future<CaseDetail> getCaseById(String caseId) async {
    return CaseDetail(
      id: caseId,
      farmId: 'f-1',
      farmName: 'Büyük Tarla',
      category: CaseCategory.pest,
      status: CaseStatus.waitingFarmer,
      title: 'Kurt Zararlısı',
      description: 'Meyvelerde kurt var.',
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
}

class FakeTarlaRepository implements TarlaRepository {
  @override
  Future<List<Tarla>> getTarlalar() async => [];

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

void main() {
  final sampleCase = CaseSummary(
    id: 'case-1',
    farmId: 'farm-1',
    farmName: 'Zeytinlik',
    category: CaseCategory.pest,
    status: CaseStatus.waitingFarmer,
    title: 'Zeytin Sineği Teşhisi',
    createdAt: DateTime.now(),
    updatedAt: DateTime.now(),
    messageCount: 3,
    mediaCount: 1,
  );

  final closedCase = CaseSummary(
    id: 'case-2',
    farmId: 'farm-1',
    farmName: 'Zeytinlik',
    category: CaseCategory.irrigation,
    status: CaseStatus.closed,
    title: 'Damlama Arızası',
    createdAt: DateTime.now().subtract(const Duration(days: 2)),
    updatedAt: DateTime.now().subtract(const Duration(days: 1)),
    messageCount: 5,
    mediaCount: 0,
  );

  testWidgets('renders case list cards with status badges', (tester) async {
    final repo = MockCaseRepository(cases: [sampleCase]);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sorun Bildirimlerim'), findsOneWidget);
    expect(find.text('Zeytin Sineği Teşhisi'), findsOneWidget);
    expect(find.text('Zeytinlik'), findsOneWidget);
    expect(find.text('Bilgi Bekliyor'), findsOneWidget);
    expect(find.text('3 mesaj'), findsOneWidget);
  });

  testWidgets('renders empty view when no cases exist', (tester) async {
    final repo = MockCaseRepository(cases: []);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Kayıtlı bir sorun bildiriminiz bulunmuyor.'), findsOneWidget);
    expect(find.byType(FloatingActionButton), findsOneWidget);
  });

  testWidgets('renders farm title when farmName is provided', (tester) async {
    final repo = MockCaseRepository(cases: [sampleCase]);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
          farmId: 'farm-1',
          farmName: 'Zeytinlik',
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Zeytinlik Bildirimleri'), findsOneWidget);
  });

  testWidgets('filters cases by active and closed tabs', (tester) async {
    final repo = MockCaseRepository(cases: [sampleCase, closedCase]);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Default 'Tümü': both show
    expect(find.text('Zeytin Sineği Teşhisi'), findsOneWidget);
    expect(find.text('Damlama Arızası'), findsOneWidget);

    // Switch to 'Aktifler'
    await tester.tap(find.text('Aktifler'));
    await tester.pumpAndSettle();

    expect(find.text('Zeytin Sineği Teşhisi'), findsOneWidget);
    expect(find.text('Damlama Arızası'), findsNothing);

    // Switch to 'Çözülenler'
    await tester.tap(find.text('Çözülenler'));
    await tester.pumpAndSettle();

    expect(find.text('Zeytin Sineği Teşhisi'), findsNothing);
    expect(find.text('Damlama Arızası'), findsOneWidget);
  });

  testWidgets('navigates to detail screen when tapping card', (tester) async {
    final repo = MockCaseRepository(cases: [sampleCase]);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Zeytin Sineği Teşhisi'));
    await tester.pumpAndSettle();

    // Destination rendered (either placeholder or detail screen)
    expect(find.byType(VakaDetayEkrani), findsOneWidget);
  });

  testWidgets('navigates to SorunBildirEkrani when FAB is tapped', (tester) async {
    final repo = MockCaseRepository(cases: []);
    final tarlaRepo = FakeTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle();

    expect(find.byType(SorunBildirEkrani), findsOneWidget);
  });

  testWidgets('renders error state when load fails', (tester) async {
    final repo = MockCaseRepository(shouldThrow: true);
    final tarlaRepo = const LocalTarlaRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: VakaListesiEkrani(
          caseRepository: repo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Bildirimler yüklenemedi'), findsOneWidget);
    expect(find.text('Tekrar Dene'), findsOneWidget);
  });
}

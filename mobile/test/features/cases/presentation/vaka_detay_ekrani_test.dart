import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/features/ai_assistant/data/image_picker_service.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/vaka_detay_ekrani.dart';

final kTestImage = Uint8List.fromList(<int>[
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
  0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
  0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
  0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
  0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
  0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
  0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
  0x42, 0x60, 0x82,
]);

class MockChatCaseRepository implements CaseRepository {
  MockChatCaseRepository({
    required this.detail,
    this.shouldFailSend = false,
    this.shouldFailLoad = false,
  });

  CaseDetail detail;
  CaseMessage? sentMessage;
  List<int>? sentImageBytes;
  String? sentImageFileName;
  bool shouldFailSend;
  bool shouldFailLoad;

  @override
  Future<String> createCase(CreateCaseInput input) async => 'id';

  @override
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status}) async => [];

  @override
  Future<CaseDetail> getCaseById(String caseId) async {
    if (shouldFailLoad) {
      throw Exception('Detay yüklenemedi');
    }
    return detail;
  }

  @override
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  }) async {
    if (shouldFailSend) {
      throw Exception('Ağ bağlantısı başarısız.');
    }
    final msg = CaseMessage(
      id: 'new-msg-1',
      caseId: caseId,
      senderId: 'farmer-1',
      senderName: 'Siz',
      messageType: CaseMessageType.comment,
      body: body,
      createdAt: DateTime.now(),
      isCurrentUser: true,
    );
    sentMessage = msg;
    sentImageBytes = imageBytes;
    sentImageFileName = imageFileName;
    detail = CaseDetail(
      id: detail.id,
      farmId: detail.farmId,
      farmName: detail.farmName,
      category: detail.category,
      status: CaseStatus.inReview, // Auto-transitions
      title: detail.title,
      description: detail.description,
      initialMediaUrls: detail.initialMediaUrls,
      messages: [...detail.messages, msg],
      createdAt: detail.createdAt,
    );
    return msg;
  }
}

class MockChatImagePickerService implements ImagePickerService {
  MockChatImagePickerService({this.imageToReturn});
  final PickedImageData? imageToReturn;

  @override
  Future<PickedImageData?> pickImage({required ImageSource source}) async {
    return imageToReturn;
  }
}

void main() {
  final sampleDetail = CaseDetail(
    id: 'case-100',
    farmId: 'farm-1',
    farmName: 'Büyük Tarla',
    category: CaseCategory.disease,
    status: CaseStatus.waitingFarmer,
    title: 'Yaprak Yanıklığı',
    description: 'Yapraklar hızla kuruyor.',
    createdAt: DateTime.now(),
    messages: [
      CaseMessage(
        id: 'm-1',
        caseId: 'case-100',
        senderId: 'expert-1',
        senderName: 'Ziraat Müh. Ayşe',
        messageType: CaseMessageType.additionalInfoRequest,
        body: 'Hangi ilacı ve dozajı kullandınız?',
        createdAt: DateTime.now().subtract(const Duration(hours: 1)),
      ),
    ],
  );

  testWidgets('renders case details, expert message bubble, and allows reply', (tester) async {
    final repo = MockChatCaseRepository(detail: sampleDetail);

    await tester.pumpWidget(
      MaterialApp(
        home: VakaDetayEkrani(
          caseId: 'case-100',
          caseRepository: repo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Yaprak Yanıklığı'), findsOneWidget);
    expect(find.text('Büyük Tarla'), findsOneWidget);
    expect(find.text('Hangi ilacı ve dozajı kullandınız?'), findsOneWidget);
    expect(find.text('Ziraat Mühendisi'), findsWidgets);

    // Send reply
    await tester.enterText(find.byType(TextField), 'Bakırlı preparat kullandım.');
    await tester.tap(find.byIcon(Icons.send));
    await tester.pumpAndSettle();

    expect(repo.sentMessage, isNotNull);
    expect(repo.sentMessage!.body, 'Bakırlı preparat kullandım.');
    expect(find.text('Bakırlı preparat kullandım.'), findsOneWidget);
  });

  testWidgets('locks message input when case status is closed', (tester) async {
    final closedDetail = CaseDetail(
      id: 'case-200',
      farmId: 'farm-1',
      farmName: 'Büyük Tarla',
      category: CaseCategory.disease,
      status: CaseStatus.closed,
      title: 'Çözülen Vaka',
      description: 'Sorun çözüldü.',
      createdAt: DateTime.now(),
      messages: [],
    );
    final repo = MockChatCaseRepository(detail: closedDetail);

    await tester.pumpWidget(
      MaterialApp(
        home: VakaDetayEkrani(
          caseId: 'case-200',
          caseRepository: repo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Bu sorun bildiriminiz çözümlenip kapatılmıştır.'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('shows error state when case fails to load and allows retry', (tester) async {
    final repo = MockChatCaseRepository(
      detail: sampleDetail,
      shouldFailLoad: true,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: VakaDetayEkrani(
          caseId: 'case-100',
          caseRepository: repo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Bildirim detayları yüklenemedi'), findsOneWidget);
    expect(find.text('Tekrar Dene'), findsOneWidget);

    repo.shouldFailLoad = false;
    await tester.tap(find.text('Tekrar Dene'));
    await tester.pumpAndSettle();

    expect(find.text('Yaprak Yanıklığı'), findsOneWidget);
  });

  testWidgets('shows snackbar on send error and retains message text', (tester) async {
    final repo = MockChatCaseRepository(
      detail: sampleDetail,
      shouldFailSend: true,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: VakaDetayEkrani(
          caseId: 'case-100',
          caseRepository: repo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'Test hata mesajı');
    await tester.tap(find.byIcon(Icons.send));
    await tester.pumpAndSettle();

    expect(find.byType(SnackBar), findsOneWidget);
    expect(find.text('Test hata mesajı'), findsOneWidget);
  });

  testWidgets('allows picking photo and sending along with message', (tester) async {
    final repo = MockChatCaseRepository(detail: sampleDetail);
    final mockPicker = MockChatImagePickerService(
      imageToReturn: PickedImageData(
        bytes: kTestImage,
        name: 'test_leaf.png',
        mimeType: 'image/png',
      ),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: VakaDetayEkrani(
          caseId: 'case-100',
          caseRepository: repo,
          imagePickerService: mockPicker,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Tap photo button
    await tester.tap(find.byIcon(Icons.camera_alt));
    await tester.pumpAndSettle();

    // Select camera in sheet
    expect(find.text('Kameradan Çek'), findsOneWidget);
    await tester.tap(find.text('Kameradan Çek'));
    await tester.pumpAndSettle();

    // Photo thumbnail should be visible
    expect(find.byIcon(Icons.close), findsOneWidget);

    // Enter message
    await tester.enterText(find.byType(TextField), 'Fotoğraftaki durum bu.');
    await tester.tap(find.byIcon(Icons.send));
    await tester.pumpAndSettle();

    expect(repo.sentMessage, isNotNull);
    expect(repo.sentImageBytes, isNotNull);
    expect(repo.sentImageFileName, 'test_leaf.png');
  });
}

import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/features/ai_assistant/data/image_picker_service.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/sorun_bildir_ekrani.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';

// Valid 1x1 transparent PNG bytes for Flutter Image.memory
final kTransparentImage = Uint8List.fromList(<int>[
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

class MockCaseRepository implements CaseRepository {
  CreateCaseInput? lastInput;
  @override
  Future<String> createCase(CreateCaseInput input) async {
    lastInput = input;
    return 'created-case-id';
  }
}

class MockTarlaRepository implements TarlaRepository {
  MockTarlaRepository(this.tarlalar);
  final List<Tarla> tarlalar;

  @override
  Future<List<Tarla>> getTarlalar() async => tarlalar;

  @override
  Future<void> addTarla(Tarla tarla) async {}
}

class MockImagePickerService implements ImagePickerService {
  MockImagePickerService({this.imageToReturn});
  final PickedImageData? imageToReturn;

  @override
  Future<PickedImageData?> pickImage({required ImageSource source}) async {
    return imageToReturn;
  }
}

void main() {
  final sampleTarla = Tarla(
    id: 'tarla-1',
    name: 'Büyük Tarla',
    size: 10.0,
    cropType: 'Domates',
    plantingDate: DateTime(2026, 4, 1),
  );

  testWidgets('renders all essential form fields', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Sorun Bildir'), findsWidgets);
    expect(find.text('Hastalık'), findsOneWidget);
    expect(find.text('Zararlı'), findsOneWidget);
    expect(find.text('Uzmana Gönder'), findsOneWidget);
    expect(find.byType(TextField), findsNWidgets(2)); // Title + Description
  });

  testWidgets('submits form successfully and calls repository', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Select category
    await tester.tap(find.text('Hastalık'));
    await tester.pump();

    // Enter title & description
    await tester.enterText(find.byType(TextField).at(0), 'Unlu Bit');
    await tester.enterText(find.byType(TextField).at(1), 'Gövdede beyaz unlanma var.');
    await tester.pump();

    // Ensure button is visible before tapping
    await tester.ensureVisible(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    // Tap submit
    await tester.tap(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    expect(caseRepo.lastInput, isNotNull);
    expect(caseRepo.lastInput!.title, 'Unlu Bit');
    expect(caseRepo.lastInput!.category, CaseCategory.disease);
    expect(caseRepo.lastInput!.farmId, 'tarla-1');
  });

  testWidgets('validates title length before submitting', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Attempt to submit empty form (title is empty)
    await tester.ensureVisible(find.text('Uzmana Gönder'));
    await tester.tap(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    expect(find.text('Lütfen en az 2 karakterli bir başlık girin.'), findsOneWidget);
    expect(caseRepo.lastInput, isNull);
  });

  testWidgets('validates description before submitting', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Enter valid title but leave description empty
    await tester.enterText(find.byType(TextField).at(0), 'Yaprak Sararması');
    await tester.pump();

    await tester.ensureVisible(find.text('Uzmana Gönder'));
    await tester.tap(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    expect(find.text('Lütfen sorununuzu en az 2 karakter ile açıklayın.'), findsOneWidget);
    expect(caseRepo.lastInput, isNull);
  });

  testWidgets('picks photo and submits with image data', (tester) async {
    final caseRepo = MockCaseRepository();
    final tarlaRepo = MockTarlaRepository([sampleTarla]);
    final mockPicker = MockImagePickerService(
      imageToReturn: PickedImageData(
        bytes: kTransparentImage,
        name: 'test_disease.png',
        mimeType: 'image/png',
      ),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: SorunBildirEkrani(
          initialTarlaId: 'tarla-1',
          caseRepository: caseRepo,
          tarlaRepository: tarlaRepo,
          imagePickerService: mockPicker,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Tap photo picker tile
    await tester.tap(find.text('Sorunun Fotoğrafını Çek'));
    await tester.pumpAndSettle();

    // Modal bottom sheet should show camera and gallery options
    expect(find.text('Kameradan Çek'), findsOneWidget);
    expect(find.text('Galeriden Seç'), findsOneWidget);

    await tester.tap(find.text('Kameradan Çek'));
    await tester.pumpAndSettle();

    // Photo preview should now show "Fotoğrafı Kaldır"
    expect(find.text('Fotoğrafı Kaldır'), findsOneWidget);

    // Fill title & description
    await tester.enterText(find.byType(TextField).at(0), 'Mantar Sorunu');
    await tester.enterText(find.byType(TextField).at(1), 'Alt yapraklarda yayılıyor.');
    await tester.pump();

    // Submit
    await tester.ensureVisible(find.text('Uzmana Gönder'));
    await tester.tap(find.text('Uzmana Gönder'));
    await tester.pumpAndSettle();

    expect(caseRepo.lastInput, isNotNull);
    expect(caseRepo.lastInput!.title, 'Mantar Sorunu');
    expect(caseRepo.lastInput!.imageBytes, isNotNull);
    expect(caseRepo.lastInput!.imageFileName, 'test_disease.png');
  });
}

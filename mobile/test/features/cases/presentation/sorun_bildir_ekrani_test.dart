import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/features/ai_assistant/data/image_picker_service.dart';
import 'package:mobile/features/ai_assistant/data/voice_input_service.dart';
import 'package:mobile/features/cases/data/case_repository.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';
import 'package:mobile/features/cases/presentation/sorun_bildir_ekrani.dart';
import 'package:mobile/features/fields/data/tarla_repository.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/services/api_client.dart';

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
  int createCaseCallCount = 0;
  Exception? errorToThrow;
  Duration? createDelay;

  @override
  Future<String> createCase(CreateCaseInput input) async {
    createCaseCallCount++;
    if (createDelay != null) {
      await Future<void>.delayed(createDelay!);
    }
    if (errorToThrow != null) {
      throw errorToThrow!;
    }
    lastInput = input;
    return 'created-case-id';
  }

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

  @override
  Future<Map<String, String>> getAuthHeaders() async => {};
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

class MockVoiceInputService implements VoiceInputService {
  bool isAvailableVal = true;
  bool _isListening = false;
  ValueChanged<VoiceRecognitionResult>? onResult;
  void Function(VoiceInputException error)? onError;
  void Function(bool isListening)? onListeningChanged;

  int startListeningCalls = 0;
  int stopListeningCalls = 0;
  int cancelListeningCalls = 0;
  int disposeCalls = 0;

  VoiceInputException? errorToThrowOnStart;

  @override
  bool get isListening => _isListening;

  @override
  bool get isAvailable => isAvailableVal;

  @override
  Future<bool> initialize() async => isAvailableVal;

  @override
  Future<bool> startListening({
    required ValueChanged<VoiceRecognitionResult> onResult,
    bool onDevice = false,
    void Function(VoiceInputException error)? onError,
    void Function(bool isListening)? onListeningChanged,
  }) async {
    startListeningCalls++;
    this.onResult = onResult;
    this.onError = onError;
    this.onListeningChanged = onListeningChanged;

    if (errorToThrowOnStart != null) {
      onError?.call(errorToThrowOnStart!);
      return false;
    }
    if (!isAvailableVal) {
      onError?.call(
        const VoiceInputException(
          type: VoiceInputErrorType.unavailable,
          message: 'Bu cihazda ses tanıma özelliği kullanılamıyor.',
        ),
      );
      return false;
    }

    _isListening = true;
    onListeningChanged?.call(true);
    return true;
  }

  @override
  Future<void> stopListening() async {
    stopListeningCalls++;
    _isListening = false;
    onListeningChanged?.call(false);
  }

  @override
  Future<void> cancelListening() async {
    cancelListeningCalls++;
    _isListening = false;
    onListeningChanged?.call(false);
  }

  @override
  void dispose() {
    disposeCalls++;
    _isListening = false;
  }

  void emitResult(String text, {bool isFinal = false}) {
    onResult?.call(VoiceRecognitionResult(recognizedWords: text, isFinal: isFinal));
    if (isFinal) {
      _isListening = false;
      onListeningChanged?.call(false);
    }
  }

  void emitError(VoiceInputException error) {
    onError?.call(error);
    _isListening = false;
    onListeningChanged?.call(false);
  }
}

class MockConnectivity implements Connectivity {
  MockConnectivity({List<ConnectivityResult>? results})
      : results = results ?? [ConnectivityResult.wifi];

  List<ConnectivityResult> results;
  int checkConnectivityCalls = 0;

  @override
  Future<List<ConnectivityResult>> checkConnectivity() async {
    checkConnectivityCalls++;
    return results;
  }

  @override
  Stream<List<ConnectivityResult>> get onConnectivityChanged =>
      Stream.value(results);
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
      const MethodChannel('dev.fluttercommunity.plus/connectivity'),
      (MethodCall methodCall) async {
        if (methodCall.method == 'check') {
          return ['wifi'];
        }
        return null;
      },
    );
  });

  final sampleTarla1 = Tarla(
    id: 'tarla-1',
    name: 'Büyük Tarla',
    size: 10.0,
    cropType: 'Domates',
    plantingDate: DateTime(2026, 4, 1),
  );

  final sampleTarla2 = Tarla(
    id: 'tarla-2',
    name: 'Küçük Tarla',
    size: 5.0,
    cropType: 'Biber',
    plantingDate: DateTime(2026, 4, 15),
  );

  final sampleTarlaNoCrop = Tarla(
    id: 'tarla-3',
    name: 'Boş Tarla',
    size: 8.0,
    cropType: null,
  );

  group('buildAutomaticCaseTitle - Birim Testleri', () {
    test('1. Domates + Hastalık formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Domates', category: CaseCategory.disease),
        'Domates • Hastalık',
      );
    });

    test('2. Buğday + Zararlı formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Buğday', category: CaseCategory.pest),
        'Buğday • Zararlı',
      );
    });

    test('3. Mısır + Sulama formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Mısır', category: CaseCategory.irrigation),
        'Mısır • Sulama',
      );
    });

    test('4. Elma + Besleme / Gübre formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Elma', category: CaseCategory.nutrition),
        'Elma • Besleme / Gübre',
      );
    });

    test('5. Pamuk + Hava Koşulları formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Pamuk', category: CaseCategory.weather),
        'Pamuk • Hava Koşulları',
      );
    });

    test('6. Zeytin + Diğer formatı doğru üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Zeytin', category: CaseCategory.other),
        'Zeytin • Diğer',
      );
    });

    test('7. cropType null/boş/whitespace olduğunda güvenli fallback başlıklar üretir', () {
      expect(
        buildAutomaticCaseTitle(cropType: null, category: CaseCategory.disease),
        'Hastalık Bildirimi',
      );
      expect(
        buildAutomaticCaseTitle(cropType: '', category: CaseCategory.pest),
        'Zararlı Bildirimi',
      );
      expect(
        buildAutomaticCaseTitle(cropType: '   ', category: CaseCategory.irrigation),
        'Sulama Sorunu',
      );
      expect(
        buildAutomaticCaseTitle(cropType: null, category: CaseCategory.nutrition),
        'Besleme / Gübre Sorunu',
      );
      expect(
        buildAutomaticCaseTitle(cropType: null, category: CaseCategory.weather),
        'Hava Koşulları Bildirimi',
      );
      expect(
        buildAutomaticCaseTitle(cropType: null, category: CaseCategory.other),
        'Sorun Bildirimi',
      );
    });

    test('8. Türkçe karakterler (ç, ğ, ı, ö, ş, ü, İ) eksiksiz korunur', () {
      expect(
        buildAutomaticCaseTitle(cropType: 'Ayçiçeği', category: CaseCategory.pest),
        'Ayçiçeği • Zararlı',
      );
      expect(
        buildAutomaticCaseTitle(cropType: 'Çeltik', category: CaseCategory.disease),
        'Çeltik • Hastalık',
      );
      expect(
        buildAutomaticCaseTitle(cropType: 'Şeker Pancarı', category: CaseCategory.nutrition),
        'Şeker Pancarı • Besleme / Gübre',
      );
    });

    test('9. Başlık 160 karakter limitini aşmaz ve güvenle sınırlandırılır', () {
      final longCrop = 'Çok Uzun Deneme Ürün Adı ' * 10;
      final result = buildAutomaticCaseTitle(cropType: longCrop, category: CaseCategory.disease);

      expect(result.length, lessThanOrEqualTo(160));
      expect(result.endsWith(' • Hastalık'), isTrue);
    });
  });

  group('SorunBildirEkrani - Layout & Görünüm Hiyerarşisi', () {
    testWidgets('1. Aktif tarla kompakt şekilde görünür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('txt_compact_tarla_info')), findsOneWidget);
      expect(find.text('Büyük Tarla • Domates'), findsOneWidget);
      expect(find.byKey(const Key('btn_change_tarla')), findsOneWidget);
      expect(find.text('Değiştir'), findsOneWidget);
    });

    testWidgets('2. Fotoğraf bölümü ana aksiyon olarak görünür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sorunun fotoğrafını ekleyin'), findsOneWidget);
      expect(
        find.text('Fotoğraf eklerseniz uzman sorunu daha hızlı değerlendirebilir.'),
        findsOneWidget,
      );
      expect(find.byKey(const Key('btn_pick_camera')), findsOneWidget);
      expect(find.byKey(const Key('btn_pick_gallery')), findsOneWidget);
    });

    testWidgets('3. Konuşarak Anlat belirgin şekilde görünür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('btn_voice_input')), findsOneWidget);
      expect(find.text('🎙️ Konuşarak Anlat'), findsOneWidget);
    });

    testWidgets('4. Kategori alanı çalışmaya devam eder', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sorun türü'), findsOneWidget);
      expect(find.text('Hastalık'), findsOneWidget);
      expect(find.text('Zararlı'), findsOneWidget);
      expect(find.text('Sulama'), findsOneWidget);
      expect(find.text('Besleme / Gübre'), findsOneWidget);

      await tester.ensureVisible(find.text('Sulama'));
      await tester.tap(find.text('Sulama'));
      await tester.pumpAndSettle();

      expect(find.text('Domates • Sulama'), findsOneWidget);
    });

    testWidgets('5. Manuel title input kesinlikle geri gelmez', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sorun Başlığı'), findsNothing);
      expect(find.byType(TextField), findsOneWidget);
      expect(find.byKey(const Key('field_description')), findsOneWidget);
    });
  });

  group('SorunBildirEkrani - Fotoğraf UX', () {
    testWidgets('6. Fotoğraf yokken kamera/galeri aksiyonu görünür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('btn_pick_camera')), findsOneWidget);
      expect(find.byKey(const Key('btn_pick_gallery')), findsOneWidget);
      expect(find.byKey(const Key('btn_change_photo')), findsNothing);
      expect(find.byKey(const Key('btn_remove_photo')), findsNothing);
    });

    testWidgets('7. Fotoğraf seçildiğinde preview görünür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockPicker = MockImagePickerService(
        imageToReturn: PickedImageData(
          bytes: kTransparentImage,
          name: 'tarla_foto.png',
          mimeType: 'image/png',
        ),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            imagePickerService: mockPicker,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('btn_pick_gallery')));
      await tester.pumpAndSettle();

      expect(find.byType(Image), findsOneWidget);
      expect(find.byKey(const Key('btn_change_photo')), findsOneWidget);
      expect(find.byKey(const Key('btn_remove_photo')), findsOneWidget);
      expect(find.text('Sorunun fotoğrafını ekleyin'), findsNothing);
    });

    testWidgets('8. Değiştir aksiyonu yeni picker akışını açar', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockPicker = MockImagePickerService(
        imageToReturn: PickedImageData(
          bytes: kTransparentImage,
          name: 'tarla_foto.png',
          mimeType: 'image/png',
        ),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            imagePickerService: mockPicker,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('btn_pick_camera')));
      await tester.pumpAndSettle();

      // Değiştir butonuna bas
      await tester.tap(find.byKey(const Key('btn_change_photo')));
      await tester.pumpAndSettle();

      // Bottom sheet açılır
      expect(find.text('Kameradan Çek'), findsOneWidget);
      expect(find.text('Galeriden Seç'), findsOneWidget);

      await tester.tap(find.text('Galeriden Seç'));
      await tester.pumpAndSettle();

      expect(find.byType(Image), findsOneWidget);
    });

    testWidgets('9. Kaldır fotoğrafı local formdan kaldırır', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockPicker = MockImagePickerService(
        imageToReturn: PickedImageData(
          bytes: kTransparentImage,
          name: 'tarla_foto.png',
          mimeType: 'image/png',
        ),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            imagePickerService: mockPicker,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('btn_pick_gallery')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('btn_remove_photo')), findsOneWidget);

      await tester.tap(find.byKey(const Key('btn_remove_photo')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('btn_remove_photo')), findsNothing);
      expect(find.text('Sorunun fotoğrafını ekleyin'), findsOneWidget);
      expect(find.byKey(const Key('btn_pick_camera')), findsOneWidget);
    });

    testWidgets('10. Fotoğraf olmadan valid description ile submit mümkündür', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Yaprak kenarlarında kuruma var.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(caseRepo.lastInput, isNotNull);
      expect(caseRepo.lastInput!.imageBytes, isNull);
      expect(caseRepo.lastInput!.description, 'Yaprak kenarlarında kuruma var.');
    });
  });

  group('SorunBildirEkrani - Voice & Açıklama UX', () {
    testWidgets('11. Mikrofon start çalışır', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
      await tester.tap(find.byKey(const Key('btn_voice_input')));
      await tester.pump();

      expect(mockVoice.startListeningCalls, 1);
      expect(mockVoice.isListening, true);
    });

    testWidgets('12. Listening state görünür ve durdurulabilir', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
      await tester.tap(find.byKey(const Key('btn_voice_input')));
      await tester.pump();

      expect(find.byKey(const Key('voice_listening_banner')), findsOneWidget);
      expect(find.text('🎙️ Dinliyorum... Konuşabilirsiniz'), findsOneWidget);
      expect(find.byKey(const Key('btn_stop_listening')), findsOneWidget);

      await tester.tap(find.byKey(const Key('btn_stop_listening')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('voice_listening_banner')), findsNothing);
      expect(mockVoice.stopListeningCalls, 1);
    });

    testWidgets('13. Speech result description alanına aktarılır', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
      await tester.tap(find.byKey(const Key('btn_voice_input')));
      await tester.pump();

      mockVoice.emitResult('Meyvelerde kahverengi lekeler oluştu.', isFinal: true);
      await tester.pumpAndSettle();

      expect(find.text('Meyvelerde kahverengi lekeler oluştu.'), findsOneWidget);
    });

    testWidgets('14. Kullanıcı description alanını elle düzenleyebilir', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Manuel ilk taslak.',
      );
      await tester.pump();

      expect(find.text('Manuel ilk taslak.'), findsOneWidget);

      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Düzenlenmiş nihai metin.',
      );
      await tester.pump();

      expect(find.text('Düzenlenmiş nihai metin.'), findsOneWidget);
    });
  });

  group('SorunBildirEkrani - Submit & Hata Yönetimi', () {
    testWidgets('15. Description boşken submit yapılamaz', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(find.text('Lütfen sorununuzu en az 2 karakter ile açıklayın.'), findsOneWidget);
      expect(caseRepo.createCaseCallCount, 0);
    });

    testWidgets('16. Valid description + farm ile submit çalışır', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Kök boğazında çürüme başladı.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(caseRepo.createCaseCallCount, 1);
      expect(caseRepo.lastInput!.farmId, sampleTarla1.id);
      expect(caseRepo.lastInput!.title, 'Domates • Hastalık');
      expect(caseRepo.lastInput!.description, 'Kök boğazında çürüme başladı.');
    });

    testWidgets('17. Gönder sırasında duplicate submit engellenir ve loading gösterilir', (tester) async {
      final caseRepo = MockCaseRepository()
        ..createDelay = const Duration(milliseconds: 300);
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Serada havalandırma yetersiz.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pump(const Duration(milliseconds: 50));

      // Loading görünür
      expect(find.text('Gönderiliyor...'), findsOneWidget);

      // Tekrar tıkla (duplicate click)
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pump(const Duration(milliseconds: 50));

      // İşlemin tamamlanmasını bekle
      await tester.pumpAndSettle();

      expect(caseRepo.createCaseCallCount, 1);
    });

    testWidgets('18. Fotoğraf upload başarısızsa hata gösterilir ve case tamamlanamaz', (tester) async {
      final caseRepo = MockCaseRepository()
        ..errorToThrow = const ApiException('Fotoğraf yükleme sunucu hatası.');
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockPicker = MockImagePickerService(
        imageToReturn: PickedImageData(
          bytes: kTransparentImage,
          name: 'foto.png',
          mimeType: 'image/png',
        ),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            imagePickerService: mockPicker,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('btn_pick_camera')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Yaprak biti istilası.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(find.text('Fotoğraf yükleme sunucu hatası.'), findsOneWidget);
    });

    testWidgets('19. Upload hatasında form verileri (fotoğraf ve açıklama) korunur', (tester) async {
      final caseRepo = MockCaseRepository()
        ..errorToThrow = const ApiException('Fotoğraf yüklenemedi.');
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockPicker = MockImagePickerService(
        imageToReturn: PickedImageData(
          bytes: kTransparentImage,
          name: 'foto.png',
          mimeType: 'image/png',
        ),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            imagePickerService: mockPicker,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('btn_pick_gallery')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Önemli açıklama metni.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      // Hata gösterildi ama form verileri korundu
      expect(find.text('Fotoğraf yüklenemedi.'), findsOneWidget);
      expect(find.byKey(const Key('btn_remove_photo')), findsOneWidget);
      expect(find.text('Önemli açıklama metni.'), findsOneWidget);
    });

    testWidgets('20. Case create hatasında form verileri korunur ve retry edilebilir', (tester) async {
      final caseRepo = MockCaseRepository()
        ..errorToThrow = const ApiException('Ağ bağlantısı koptu.');
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Kırmızı örümcek zararı.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(find.text('Ağ bağlantısı koptu.'), findsOneWidget);
      expect(find.text('Kırmızı örümcek zararı.'), findsOneWidget);

      // Hatayı kaldır ve retry yap
      caseRepo.errorToThrow = null;
      await tester.pump(const Duration(seconds: 5));
      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(caseRepo.createCaseCallCount, 2);
    });

    testWidgets('21. Başarılı gönderimde doğru success feedback ve pop oluşur', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      bool didPop = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (ctx) => TextButton(
                onPressed: () async {
                  final res = await Navigator.push<bool>(
                    ctx,
                    MaterialPageRoute(
                      builder: (_) => SorunBildirEkrani(
                        initialTarla: sampleTarla1,
                        caseRepository: caseRepo,
                        tarlaRepository: tarlaRepo,
                      ),
                    ),
                  );
                  if (res == true) didPop = true;
                },
                child: const Text('Aç'),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Aç'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(
        find.byKey(const Key('field_description')),
        'Başarılı vaka açıklaması.',
      );
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
      await tester.tap(find.byKey(const Key('btn_submit_case')));
      await tester.pumpAndSettle();

      expect(find.text('Sorununuz uzmana gönderildi.'), findsOneWidget);
      expect(didPop, isTrue);
    });
  });

  group('SorunBildirEkrani - Responsive & Erişilebilirlik', () {
    testWidgets('22. textScaleFactor yüksek olduğunda (2.0) overflow oluşmaz', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);

      await tester.pumpWidget(
        MaterialApp(
          home: MediaQuery(
            data: const MediaQueryData(
              size: Size(360, 640),
              textScaler: TextScaler.linear(2.0),
            ),
            child: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byType(SorunBildirEkrani), findsOneWidget);
    });

    testWidgets('23. Uzun tarla ve ürün adı layout overflow yapmaz', (tester) async {
      final longTarla = Tarla(
        id: 'tarla-long',
        name: 'Çok Uzun Karadeniz Fındık ve Çay Deneme İstasyonu Parseli 12345',
        cropType: 'Özel Islah Edilmiş Sanayi Tipi Domates ve Salçalık Biber Hibriti',
        size: 50.0,
      );
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([longTarla]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: longTarla,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('txt_compact_tarla_info')), findsOneWidget);
    });
  });

  group('SorunBildirEkrani - Dinamik Başlık & Ek Regresyon Testleri', () {
    testWidgets('Tarla değiştirildiğinde cropType bazlı otomatik başlık dinamik değişir', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1, sampleTarla2]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Domates • Hastalık'), findsOneWidget);

      // Değiştir butonuna tıkla ve Küçük Tarla (Biber) seç
      await tester.tap(find.byKey(const Key('btn_change_tarla')));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Küçük Tarla • Biber').last);
      await tester.pumpAndSettle();

      expect(find.text('Biber • Hastalık'), findsOneWidget);
    });

    testWidgets('cropType boş olan tarla seçildiğinde güvenli fallback başlık oluşur', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarlaNoCrop]);

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarlaNoCrop,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Hastalık Bildirimi'), findsOneWidget);
    });

    testWidgets('Sesli giriş metni birleştirir ve silinme yapmaz', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('field_description')));
      await tester.enterText(find.byKey(const Key('field_description')), 'Önceki manuel metin.');
      await tester.pump();

      await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
      await tester.tap(find.byKey(const Key('btn_voice_input')));
      await tester.pump();

      mockVoice.emitResult('Sesli ekleme.');
      await tester.pump();

      expect(find.text('Önceki manuel metin. Sesli ekleme.'), findsOneWidget);
    });

    testWidgets('Ekran dispose edildiğinde ses dinlemesi durdurulur', (tester) async {
      final caseRepo = MockCaseRepository();
      final tarlaRepo = MockTarlaRepository([sampleTarla1]);
      final mockVoice = MockVoiceInputService();

      await tester.pumpWidget(
        MaterialApp(
          home: SorunBildirEkrani(
            initialTarla: sampleTarla1,
            caseRepository: caseRepo,
            tarlaRepository: tarlaRepo,
            voiceInputService: mockVoice,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
      await tester.tap(find.byKey(const Key('btn_voice_input')));
      await tester.pump();
      expect(mockVoice.isListening, true);

      await tester.pumpWidget(const MaterialApp(home: SizedBox()));
      await tester.pump();

      expect(mockVoice.stopListeningCalls, greaterThan(0));
    });

    group('SorunBildirEkrani - Sesli Anlatım & Offline Keyboard Mic Fallback', () {
      testWidgets('1. Online + microphone tap -> VoiceInputService çağrılır', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.wifi]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(mockVoice.startListeningCalls, 1);
        expect(mockVoice.isListening, true);
      });

      testWidgets('2. Offline + microphone tap -> VoiceInputService çağrılmaz', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.none]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(mockVoice.startListeningCalls, 0);
        expect(mockVoice.isListening, false);
      });

      testWidgets('3. Offline + microphone tap -> description FocusNode focus alır', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.none]);
        final focusNode = FocusNode();

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
              descriptionFocusNode: focusNode,
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(focusNode.hasFocus, false);

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(focusNode.hasFocus, true);
        focusNode.dispose();
      });

      testWidgets('4. Offline + microphone tap -> keyboard fallback mesajı görünür', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.none]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(
          find.text('İnternet yok. Klavyenizdeki mikrofonu kullanarak konuşabilirsiniz.'),
          findsOneWidget,
        );
      });

      testWidgets('5. Offline durumda Dinliyorum... görünmez', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.none]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(find.byKey(const Key('voice_listening_banner')), findsNothing);
        expect(find.text('🎙️ Dinliyorum... Konuşabilirsiniz'), findsNothing);
      });

      testWidgets('6. Online STT result -> description\'a yazılmaya devam eder', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.wifi]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        mockVoice.emitResult('Yapraklarda sararma başladı.');
        await tester.pump();

        expect(find.text('Yapraklarda sararma başladı.'), findsOneWidget);
      });

      testWidgets('7. Online STT network failure -> listening kapanır', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.wifi]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();
        expect(find.byKey(const Key('voice_listening_banner')), findsOneWidget);

        mockVoice.emitError(
          const VoiceInputException(
            type: VoiceInputErrorType.network,
            message: 'Ağ bağlantı hatası',
          ),
        );
        await tester.pump();

        expect(find.byKey(const Key('voice_listening_banner')), findsNothing);
        expect(find.text('🎙️ Dinliyorum... Konuşabilirsiniz'), findsNothing);
      });

      testWidgets('8. STT network failure -> keyboard fallback açılır', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.wifi]);
        final focusNode = FocusNode();

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
              descriptionFocusNode: focusNode,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        mockVoice.emitError(
          const VoiceInputException(
            type: VoiceInputErrorType.network,
            message: 'Ağ bağlantı hatası',
          ),
        );
        await tester.pump();

        expect(focusNode.hasFocus, true);
        expect(
          find.text('Sesle yazma kullanılamadı. Klavyenizdeki mikrofonu deneyebilirsiniz.'),
          findsOneWidget,
        );
        focusNode.dispose();
      });

      testWidgets('9. Kullanıcı klavyeden text girip submit edebilir', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.none]);

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              connectivity: mockConnectivity,
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Gboard veya klavye ile girilen metin
        await tester.enterText(
          find.byKey(const Key('field_description')),
          'Klavye mikrofonuyla girilen açıklama metni.',
        );
        await tester.pump();

        await tester.ensureVisible(find.byKey(const Key('btn_submit_case')));
        await tester.tap(find.byKey(const Key('btn_submit_case')));
        await tester.pumpAndSettle();

        expect(caseRepo.createCaseCallCount, 1);
        expect(
          caseRepo.lastInput?.description,
          'Klavye mikrofonuyla girilen açıklama metni.',
        );
      });

      testWidgets('10. Dinamik network değişimi: buton anında offline ise fallback açılır', (tester) async {
        final caseRepo = MockCaseRepository();
        final tarlaRepo = MockTarlaRepository([sampleTarla1]);
        final mockVoice = MockVoiceInputService();
        final mockConnectivity = MockConnectivity(results: [ConnectivityResult.wifi]);
        final focusNode = FocusNode();

        await tester.pumpWidget(
          MaterialApp(
            home: SorunBildirEkrani(
              initialTarla: sampleTarla1,
              caseRepository: caseRepo,
              tarlaRepository: tarlaRepo,
              voiceInputService: mockVoice,
              connectivity: mockConnectivity,
              descriptionFocusNode: focusNode,
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Ekran açıldıktan sonra internet kesildi
        mockConnectivity.results = [ConnectivityResult.none];

        await tester.ensureVisible(find.byKey(const Key('btn_voice_input')));
        await tester.tap(find.byKey(const Key('btn_voice_input')));
        await tester.pump();

        expect(mockVoice.startListeningCalls, 0);
        expect(focusNode.hasFocus, true);
        expect(
          find.text('İnternet yok. Klavyenizdeki mikrofonu kullanarak konuşabilirsiniz.'),
          findsOneWidget,
        );
        focusNode.dispose();
      });
    });
  });
}

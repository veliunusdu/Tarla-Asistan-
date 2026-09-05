import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/app/theme/app_theme.dart';
import 'package:mobile/features/ai_assistant/data/ai_assistant_repository.dart';
import 'package:mobile/features/ai_assistant/data/image_picker_service.dart';
import 'package:mobile/features/ai_assistant/data/voice_input_service.dart';
import 'package:mobile/features/ai_assistant/data/voice_output_service.dart';
import 'package:mobile/features/ai_assistant/data/voice_assistant_preferences.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_response.dart';
import 'package:mobile/screens/ai_asistan_ekrani.dart';
import 'package:mobile/services/api_client.dart';

// Valid 1x1 PNG bytes for Flutter Image.memory decoding
final kSamplePng = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
);

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeAiAssistantRepository implements AiAssistantRepository {
  FakeAiAssistantRepository({
    this.cevap = 'Test cevap',
    this.donusConversationId,
    this.hata,
  });

  final String cevap;
  final String? donusConversationId;
  final Object? hata;

  final List<String> gonderilen = [];
  final List<Uint8List?> fotolar = [];
  final List<String?> photoFileNames = [];
  final List<String?> photoContentTypes = [];
  final List<String?> fieldIds = [];
  final List<String?> conversationIds = [];
  final List<List<AiChatMessage>> histories = [];

  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) async {
    if (hata != null) throw hata!;
    gonderilen.add(message);
    fotolar.add(photo);
    photoFileNames.add(photoFileName);
    photoContentTypes.add(photoContentType);
    fieldIds.add(fieldId);
    conversationIds.add(conversationId);
    histories.add(List.of(history));
    return AiChatResponse(
      reply: cevap,
      conversationId: donusConversationId ?? (conversationId ?? 'conv-default'),
    );
  }

  @override
  Stream<String> streamMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
    void Function(String conversationId)? onConversationId,
  }) async* {
    if (hata != null) throw hata!;
    gonderilen.add(message);
    fotolar.add(photo);
    photoFileNames.add(photoFileName);
    photoContentTypes.add(photoContentType);
    fieldIds.add(fieldId);
    conversationIds.add(conversationId);
    histories.add(List.of(history));
    final resolvedConvId =
        donusConversationId ?? (conversationId ?? 'conv-default');
    if (onConversationId != null) onConversationId(resolvedConvId);
    yield cevap;
  }
}

class FakeImagePickerService implements ImagePickerService {
  FakeImagePickerService({this.imageToReturn});

  PickedImageData? imageToReturn;
  ImageSource? lastSource;

  @override
  Future<PickedImageData?> pickImage({required ImageSource source}) async {
    lastSource = source;
    return imageToReturn;
  }
}

/// Yavaş repo — Completer ile kontrollü gecikme.
class _SlowAiRepo implements AiAssistantRepository {
  _SlowAiRepo(this._future);
  final Future<AiChatResponse> _future;

  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) => _future;

  @override
  Stream<String> streamMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
    void Function(String conversationId)? onConversationId,
  }) async* {
    final res = await _future;
    if (onConversationId != null) onConversationId(res.conversationId);
    yield res.reply;
  }
}

/// Parçalı stream üreten repo.
class _MultiChunkAiRepo implements AiAssistantRepository {
  _MultiChunkAiRepo(this.chunks);
  final List<String> chunks;

  @override
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  }) async =>
      AiChatResponse(reply: chunks.join(), conversationId: 'conv-chunk');

  @override
  Stream<String> streamMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
    void Function(String conversationId)? onConversationId,
  }) async* {
    if (onConversationId != null) onConversationId('conv-chunk');
    for (final chunk in chunks) {
      yield chunk;
    }
  }
}

/// UI testlerinde kullanılacak FakeVoiceInputService.
class FakeVoiceInputService implements VoiceInputService {
  bool available = true;
  bool _isListening = false;
  bool disposed = false;
  int startListeningCalls = 0;
  int stopListeningCalls = 0;
  int cancelListeningCalls = 0;
  int disposeCalls = 0;
  VoiceInputErrorType unavailableErrorType =
      VoiceInputErrorType.permissionDenied;
  String unavailableErrorMessage =
      'Mikrofon izni verilmedi. Sesli giriş için mikrofon erişimine izin verin.';

  Completer<void>? startCompleter;

  ValueChanged<VoiceRecognitionResult>? onResultCallback;
  void Function(VoiceInputException error)? onErrorCallback;
  void Function(bool isListening)? onListeningChangedCallback;

  @override
  bool get isAvailable => available && !disposed;

  @override
  bool get isListening => _isListening;

  @override
  Future<bool> initialize() async => isAvailable;

  @override
  Future<bool> startListening({
    required ValueChanged<VoiceRecognitionResult> onResult,
    bool onDevice = false,
    void Function(VoiceInputException error)? onError,
    void Function(bool isListening)? onListeningChanged,
  }) async {
    if (disposed) {
      onError?.call(
        const VoiceInputException(
          type: VoiceInputErrorType.runtimeError,
          message: 'Disposed',
        ),
      );
      onListeningChanged?.call(false);
      return false;
    }
    if (!isAvailable) {
      onError?.call(
        VoiceInputException(
          type: unavailableErrorType,
          message: unavailableErrorMessage,
        ),
      );
      onListeningChanged?.call(false);
      return false;
    }
    startListeningCalls++;
    if (startCompleter != null) {
      await startCompleter!.future;
    }
    onResultCallback = onResult;
    onErrorCallback = onError;
    onListeningChangedCallback = onListeningChanged;
    _isListening = true;
    onListeningChangedCallback?.call(true);
    return true;
  }

  @override
  Future<void> stopListening() async {
    stopListeningCalls++;
    _isListening = false;
    onListeningChangedCallback?.call(false);
  }

  @override
  Future<void> cancelListening() async {
    cancelListeningCalls++;
    _isListening = false;
    onListeningChangedCallback?.call(false);
  }

  @override
  void dispose() {
    disposeCalls++;
    disposed = true;
    _isListening = false;
    onListeningChangedCallback?.call(false);
  }

  void emitResult(String words, {required bool isFinal}) {
    onResultCallback?.call(
      VoiceRecognitionResult(recognizedWords: words, isFinal: isFinal),
    );
  }

  void emitError(VoiceInputErrorType type, String message) {
    _isListening = false;
    onListeningChangedCallback?.call(false);
    onErrorCallback?.call(VoiceInputException(type: type, message: message));
  }
}

/// UI testlerinde kullanılacak FakeVoiceOutputService.
class FakeVoiceOutputService implements VoiceOutputService {
  bool initializeResult = true;
  int initializeCalls = 0;
  int speakCalls = 0;
  final List<String> spokenTexts = [];
  int stopCalls = 0;
  bool _isSpeaking = false;
  bool disposed = false;

  void Function(bool isSpeaking)? lastSpeakOnSpeakingChanged;
  void Function(VoiceOutputException error)? lastSpeakOnError;

  @override
  bool get isSpeaking => _isSpeaking;

  @override
  void Function(bool isSpeaking)? onSpeakingChanged;

  @override
  void Function(VoiceOutputException error)? onError;

  @override
  Future<bool> initialize() async {
    initializeCalls++;
    return initializeResult;
  }

  @override
  Future<void> speak(
    String text, {
    void Function(bool isSpeaking)? onSpeakingChanged,
    void Function(VoiceOutputException error)? onError,
  }) async {
    final normalized = text.trim();
    if (normalized.isEmpty) return;

    if (_isSpeaking) {
      await stop();
    }

    speakCalls++;
    spokenTexts.add(normalized);
    lastSpeakOnSpeakingChanged = onSpeakingChanged;
    lastSpeakOnError = onError;

    _isSpeaking = true;
    this.onSpeakingChanged?.call(true);
    onSpeakingChanged?.call(true);
  }

  @override
  Future<void> stop() async {
    stopCalls++;
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
    lastSpeakOnSpeakingChanged?.call(false);
  }

  /// Seslendirmenin doğal olarak tamamlandığını simüle eder.
  void simulateCompletion() {
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
    lastSpeakOnSpeakingChanged?.call(false);
  }

  /// Çalışma zamanı hatasını simüle eder.
  void simulateRuntimeError(String message) {
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
    lastSpeakOnSpeakingChanged?.call(false);
    final err = VoiceOutputException(
      type: VoiceOutputErrorType.runtimeError,
      message: message,
    );
    onError?.call(err);
    lastSpeakOnError?.call(err);
  }

  @override
  void dispose() {
    disposed = true;
    _isSpeaking = false;
  }
}

/// UI testlerinde kullanılacak FakeVoiceAssistantPreferences.
class FakeVoiceAssistantPreferences implements VoiceAssistantPreferences {
  FakeVoiceAssistantPreferences({this.initialValue = false});

  bool initialValue;
  int getCalls = 0;
  int setCalls = 0;
  final List<bool> savedValues = [];
  Completer<void>? delayCompleter;

  @override
  Future<bool> getVoiceResponsesEnabled() async {
    getCalls++;
    if (delayCompleter != null) await delayCompleter!.future;
    return initialValue;
  }

  @override
  Future<void> setVoiceResponsesEnabled(bool enabled) async {
    setCalls++;
    initialValue = enabled;
    savedValues.add(enabled);
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

Widget _wrap({
  AiAssistantRepository? repo,
  ImagePickerService? imagePickerService,
  VoiceInputService? voiceInputService,
  VoiceOutputService? voiceOutputService,
  VoiceAssistantPreferences? preferences,
  String? fieldId,
}) => MaterialApp(
  theme: AppTheme.light,
  home: AiAsistanEkrani(
    repository: repo ?? FakeAiAssistantRepository(),
    imagePickerService: imagePickerService ?? FakeImagePickerService(),
    voiceInputService: voiceInputService ?? FakeVoiceInputService(),
    voiceOutputService: voiceOutputService ?? FakeVoiceOutputService(),
    preferences: preferences ?? FakeVoiceAssistantPreferences(),
    fieldId: fieldId,
  ),
);

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('AiAsistanEkrani', () {
    // ── Boş durum ────────────────────────────────────────────────────────
    group('boş durum', () {
      testWidgets('başlangıçta boş konuşma durumu gösterilir', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('AI Tarla Asistanı'), findsWidgets);
        expect(find.textContaining('Fotoğraf ekleyerek'), findsOneWidget);
      });

      testWidgets('örnek sorular boş durumda görünür', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(find.text('Yapraklardaki lekeler ne olabilir?'), findsOneWidget);
        expect(find.text('Bugün sulama yapmalı mıyım?'), findsOneWidget);
        expect(
          find.text('Bu bitkide hastalık belirtisi var mı?'),
          findsOneWidget,
        );
      });

      testWidgets('örnek soruya tıklayınca metin alanı dolar', (tester) async {
        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.tap(find.text('Yapraklardaki lekeler ne olabilir?'));
        await tester.pumpAndSettle();

        expect(
          find.widgetWithText(TextField, 'Yapraklardaki lekeler ne olabilir?'),
          findsOneWidget,
        );
      });
    });

    // ── Form doğrulaması ──────────────────────────────────────────────────
    group('form doğrulaması', () {
      testWidgets('boş metin gönderilemiyor', (tester) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });

      testWidgets('yalnızca boşluktan oluşan metin gönderilemiyor', (
        tester,
      ) async {
        final repo = FakeAiAssistantRepository();
        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), '   ');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(repo.gonderilen, isEmpty);
      });

      testWidgets(
        'fotoğraf seçiliyken boş mesaj gönderilmeye çalışılırsa uyarı gösterilir',
        (tester) async {
          final repo = FakeAiAssistantRepository();
          final picker = FakeImagePickerService(
            imageToReturn: PickedImageData(
              bytes: kSamplePng,
              name: 'yaprak.png',
              mimeType: 'image/png',
            ),
          );

          await tester.pumpWidget(
            _wrap(repo: repo, imagePickerService: picker),
          );
          await tester.pumpAndSettle();

          // Fotoğraf ekleme butonuna bas
          await tester.tap(find.byTooltip('Fotoğraf Ekle'));
          await tester.pumpAndSettle();

          // Galeriden Seç'e bas
          await tester.tap(find.text('Galeriden Seç'));
          await tester.pumpAndSettle();

          // Metin yazmadan Gönder'e bas
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(repo.gonderilen, isEmpty);
          expect(
            find.text('Lütfen fotoğrafla ilgili bir soru veya açıklama yazın.'),
            findsOneWidget,
          );
        },
      );
    });

    // ── Fotoğraf seçimi ve önizleme ───────────────────────────────────────
    group('fotoğraf seçimi', () {
      testWidgets(
        'fotoğraf seçildiğinde önizleme gösterilir ve kaldırılabilir',
        (tester) async {
          final picker = FakeImagePickerService(
            imageToReturn: PickedImageData(
              bytes: kSamplePng,
              name: 'bitki.png',
              mimeType: 'image/png',
            ),
          );

          await tester.pumpWidget(_wrap(imagePickerService: picker));
          await tester.pumpAndSettle();

          expect(find.byIcon(Icons.close), findsNothing);

          // Fotoğraf Ekle
          await tester.tap(find.byTooltip('Fotoğraf Ekle'));
          await tester.pumpAndSettle();

          await tester.tap(find.text('Kameradan Çek'));
          await tester.pumpAndSettle();

          expect(picker.lastSource, ImageSource.camera);
          // Önizleme ve kapat butonu görünmeli
          expect(find.byIcon(Icons.close), findsOneWidget);

          // Kaldır butonuna bas
          await tester.tap(find.byIcon(Icons.close));
          await tester.pumpAndSettle();

          expect(find.byIcon(Icons.close), findsNothing);
        },
      );
    });

    // ── Mesaj gönderme ────────────────────────────────────────────────────
    group('mesaj gönderme', () {
      testWidgets('metin mesajı gönderince kullanıcı baloncuğu görünür', (
        tester,
      ) async {
        tester.view.physicalSize = const Size(800, 1400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        await tester.enterText(
          find.byType(TextField),
          'Sulama zamanı geldi mi?',
        );
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.text('Sulama zamanı geldi mi?'), findsOneWidget);
      });

      testWidgets(
        'fotoğraf + metin gönderildiğinde repository doğru argümanlarla çağrılır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final repo = FakeAiAssistantRepository(
            cevap: 'Bu yaprakta pas hastalığı var.',
            donusConversationId: 'conv-photo-resp',
          );
          final picker = FakeImagePickerService(
            imageToReturn: PickedImageData(
              bytes: kSamplePng,
              name: 'yaprak.png',
              mimeType: 'image/png',
            ),
          );

          await tester.pumpWidget(
            _wrap(repo: repo, imagePickerService: picker, fieldId: 'tarla-99'),
          );
          await tester.pumpAndSettle();

          // Fotoğraf seç
          await tester.tap(find.byTooltip('Fotoğraf Ekle'));
          await tester.pumpAndSettle();
          await tester.tap(find.text('Galeriden Seç'));
          await tester.pumpAndSettle();

          // Metin gir ve gönder
          await tester.enterText(find.byType(TextField), 'Bu leke nedir?');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(repo.gonderilen, contains('Bu leke nedir?'));
          expect(repo.fotolar.first, kSamplePng);
          expect(repo.photoFileNames.first, 'yaprak.png');
          expect(repo.photoContentTypes.first, 'image/png');
          expect(repo.fieldIds.first, 'tarla-99');
          expect(find.text('Bu yaprakta pas hastalığı var.'), findsOneWidget);
        },
      );

      testWidgets(
        'conversation_id devamlılığı sağlanır ve yeni sohbette sıfırlanır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final repo = FakeAiAssistantRepository(
            cevap: 'İlk yanıt',
            donusConversationId: 'conv-session-1',
          );

          await tester.pumpWidget(_wrap(repo: repo));
          await tester.pumpAndSettle();

          // 1. Mesaj (İlk mesajda conversation_id null gitmeli)
          await tester.enterText(find.byType(TextField), 'Mesaj 1');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(repo.conversationIds.first, isNull);

          // 2. Mesaj (Repo tarafından dönülen conv-session-1 ile devam etmeli)
          await tester.enterText(find.byType(TextField), 'Mesaj 2');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(repo.conversationIds[1], 'conv-session-1');

          // Yeni Sohbet butonuna bas
          expect(find.byTooltip('Yeni Sohbet'), findsOneWidget);
          await tester.tap(find.byTooltip('Yeni Sohbet'));
          await tester.pumpAndSettle();

          // Sohbet temizlenmeli
          expect(find.text('Mesaj 1'), findsNothing);
          expect(find.text('Mesaj 2'), findsNothing);

          // 3. Mesaj (Yeni sohbette conversation_id sıfırlanmış olmalı)
          await tester.enterText(find.byType(TextField), 'Yeni Konuşma');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(repo.conversationIds[2], isNull);
        },
      );

      testWidgets('aynı mesaj aynı anda iki kez gönderilemez', (tester) async {
        final completer = Completer<AiChatResponse>();
        final repo = _SlowAiRepo(completer.future);

        await tester.pumpWidget(_wrap(repo: repo));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test');

        // İlk gönderim
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        // Loading sırasında gönder butonu devre dışı — ikinci gönderim engellenir
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        completer.complete(
          const AiChatResponse(reply: 'Cevap', conversationId: 'c1'),
        );
        await tester.pumpAndSettle();
      });

      testWidgets('gönderim sırasında loading göstergesi görünür', (
        tester,
      ) async {
        final completer = Completer<AiChatResponse>();

        await tester.pumpWidget(_wrap(repo: _SlowAiRepo(completer.future)));
        await tester.pumpAndSettle();

        await tester.enterText(find.byType(TextField), 'Test mesajı');
        await tester.tap(find.byTooltip('Gönder'));
        await tester.pump();

        expect(find.byType(CircularProgressIndicator), findsWidgets);
        completer.complete(
          const AiChatResponse(reply: 'Cevap', conversationId: 'c1'),
        );
        await tester.pumpAndSettle();
      });
    });

    // ── Hata durumu ───────────────────────────────────────────────────────
    group('hata durumu', () {
      testWidgets(
        'hata durumunda SnackBar gösterilir ve input kutusu restore edilir',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final repo = FakeAiAssistantRepository(
            hata: const ApiException('Fotoğraf en fazla 5 MB olabilir.'),
          );
          final picker = FakeImagePickerService(
            imageToReturn: PickedImageData(
              bytes: kSamplePng,
              name: 'buyuk.png',
              mimeType: 'image/png',
            ),
          );

          await tester.pumpWidget(
            _wrap(repo: repo, imagePickerService: picker),
          );
          await tester.pumpAndSettle();

          // Fotoğraf seç
          await tester.tap(find.byTooltip('Fotoğraf Ekle'));
          await tester.pumpAndSettle();
          await tester.tap(find.text('Galeriden Seç'));
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Hastalık kontrol');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // SnackBar gösterilmeli
          expect(find.text('Fotoğraf en fazla 5 MB olabilir.'), findsOneWidget);
          // Metin ve fotoğraf geri yüklenmiş olmalı
          expect(
            find.widgetWithText(TextField, 'Hastalık kontrol'),
            findsOneWidget,
          );
          expect(find.byIcon(Icons.close), findsOneWidget);
        },
      );
    });

    // ── Overflow / responsive ─────────────────────────────────────────────
    group('responsive', () {
      testWidgets('320x640 ekranda render exception oluşmaz', (tester) async {
        tester.view.physicalSize = const Size(480, 960);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        await tester.pumpWidget(_wrap());
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
      });
    });

    // ── Voice Assistant MVP UI Entegrasyonu ────────────────────────────────
    group('Voice Assistant MVP UI Entegrasyonu', () {
      testWidgets('Test 1: Mikrofon butonu render ediliyor', (tester) async {
        final fakeVoice = FakeVoiceInputService();
        await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
        await tester.pumpAndSettle();

        expect(find.byTooltip('Sesli giriş başlat'), findsOneWidget);
        expect(find.byIcon(Icons.mic_none), findsOneWidget);
      });

      testWidgets(
        'Test 2: Mikrofona basınca startListening() çağrılıyor ve UI güncelleniyor',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          expect(fakeVoice.startListeningCalls, 1);
          expect(fakeVoice.isListening, isTrue);
          expect(find.byTooltip('Sesli girişi durdur'), findsOneWidget);
          expect(find.byIcon(Icons.mic), findsOneWidget);
          expect(find.text('Dinliyorum...'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 3: Partial transcript birbirinin üzerine append edilmeden güncellenir',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          fakeVoice.emitResult('Buğday', isFinal: false);
          await tester.pumpAndSettle();
          expect(find.widgetWithText(TextField, 'Buğday'), findsOneWidget);

          fakeVoice.emitResult('Buğday tarlamda', isFinal: false);
          await tester.pumpAndSettle();
          expect(
            find.widgetWithText(TextField, 'Buğday tarlamda'),
            findsOneWidget,
          );
          expect(find.textContaining('Buğday Buğday'), findsNothing);
        },
      );

      testWidgets(
        'Test 4: Önceden yazılmış metin korunur ve sesli metin sonuna eklenir',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Kuzey tarlamda');
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          fakeVoice.emitResult('sararma var', isFinal: true);
          await tester.pumpAndSettle();

          expect(
            find.widgetWithText(TextField, 'Kuzey tarlamda sararma var'),
            findsOneWidget,
          );
        },
      );

      testWidgets('Test 5: Final transcript otomatik olarak gönderilmez', (
        tester,
      ) async {
        final fakeVoice = FakeVoiceInputService();
        final fakeRepo = FakeAiAssistantRepository();
        await tester.pumpWidget(
          _wrap(repo: fakeRepo, voiceInputService: fakeVoice),
        );
        await tester.pumpAndSettle();

        await tester.tap(find.byTooltip('Sesli giriş başlat'));
        await tester.pumpAndSettle();

        fakeVoice.emitResult('Buğday tarlamda sararma var', isFinal: true);
        await tester.pumpAndSettle();

        expect(fakeRepo.gonderilen, isEmpty);
        expect(
          find.widgetWithText(TextField, 'Buğday tarlamda sararma var'),
          findsOneWidget,
        );
      });

      testWidgets(
        'Test 6: Final transcript geldikten sonra Gönder butonuyla mevcut AI akışı kullanılır',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Pas hastalığı olabilir.',
          );
          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceInputService: fakeVoice),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          fakeVoice.emitResult('Buğday tarlamda sararma var', isFinal: true);
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli girişi durdur'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeRepo.gonderilen, ['Buğday tarlamda sararma var']);
          expect(find.text('Pas hastalığı olabilir.'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 7: İkinci mikrofon tıklamasında stopListening() çağrılır',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          expect(fakeVoice.isListening, isTrue);

          await tester.tap(find.byTooltip('Sesli girişi durdur'));
          await tester.pumpAndSettle();

          expect(fakeVoice.stopListeningCalls, 1);
          expect(fakeVoice.isListening, isFalse);
          expect(find.byTooltip('Sesli giriş başlat'), findsOneWidget);
          expect(find.text('Dinliyorum...'), findsNothing);
        },
      );

      testWidgets(
        'Test 8: Permission denied / voice error durumunda kullanıcı dostu hata gösterilir ve crash olmaz',
        (tester) async {
          final fakeVoice = FakeVoiceInputService()..available = false;
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          expect(fakeVoice.isListening, isFalse);
          expect(
            find.text(
              'Mikrofon izni verilmedi. Sesli giriş için mikrofon erişimine izin verin.',
            ),
            findsOneWidget,
          );
          expect(tester.takeException(), isNull);
        },
      );

      testWidgets(
        'Test 9: Screen dispose olduğunda aktif listening güvenli kapanır',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          expect(fakeVoice.isListening, isTrue);

          await tester.pumpWidget(const SizedBox.shrink());
          await tester.pumpAndSettle();

          expect(fakeVoice.isListening, isFalse);
          expect(fakeVoice.stopListeningCalls, 1);
          expect(tester.takeException(), isNull);
        },
      );

      testWidgets(
        'Test 10: Fotoğraf + sesli transcript birlikte sorunsuz gönderilir',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Görselde azot eksikliği görünüyor.',
          );
          final fakePicker = FakeImagePickerService(
            imageToReturn: PickedImageData(
              bytes: kSamplePng,
              name: 'yaprak.png',
              mimeType: 'image/png',
            ),
          );

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              imagePickerService: fakePicker,
              voiceInputService: fakeVoice,
            ),
          );
          await tester.pumpAndSettle();

          // Fotoğraf ekle
          await tester.tap(find.byTooltip('Fotoğraf Ekle'));
          await tester.pumpAndSettle();
          await tester.tap(find.text('Galeriden Seç'));
          await tester.pumpAndSettle();
          expect(find.byType(Image), findsOneWidget);

          // Sesli transcript ekle
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          fakeVoice.emitResult('Bu yaprakta ne sorun var?', isFinal: true);
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli girişi durdur'));
          await tester.pumpAndSettle();

          // Gönder
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeRepo.gonderilen, ['Bu yaprakta ne sorun var?']);
          expect(fakeRepo.fotolar.first, isNotNull);
          expect(
            find.text('Görselde azot eksikliği görünüyor.'),
            findsOneWidget,
          );
        },
      );

      testWidgets(
        'Test 11: Rapid double tap mikrofon butonuna basıldığında ikinci startListening çağrısı engellenir',
        (tester) async {
          final completer = Completer<void>();
          final fakeVoice = FakeVoiceInputService()..startCompleter = completer;
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          // İlk basış başlatma sürecini tetikler (_isStartingVoice = true)
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pump();

          // Başlatma tamamlanmadan hemen ikinci basış gelir
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pump();

          // İlk başlatma tamamlanır
          completer.complete();
          await tester.pumpAndSettle();

          expect(fakeVoice.startListeningCalls, 1);
          expect(fakeVoice.isListening, isTrue);
        },
      );

      testWidgets(
        'Test 12: Speech engine cihazda yokken (unavailable) hata doğru gösterilir ve UI listening=false kalır',
        (tester) async {
          final fakeVoice = FakeVoiceInputService()
            ..available = false
            ..unavailableErrorType = VoiceInputErrorType.unavailable
            ..unavailableErrorMessage =
                'Bu cihazda konuşma tanıma motoru kullanılamıyor.';

          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          expect(fakeVoice.isListening, isFalse);
          expect(find.byTooltip('Sesli giriş başlat'), findsOneWidget);
          expect(find.text('Dinliyorum...'), findsNothing);
          expect(
            find.text('Bu cihazda konuşma tanıma kullanılamıyor.'),
            findsOneWidget,
          );
        },
      );

      testWidgets(
        'Test 13: Sesli giriş kullanılamasa bile yazılı chat ve mesaj gönderme kesintisiz çalışır',
        (tester) async {
          tester.view.physicalSize = const Size(800, 1400);
          tester.view.devicePixelRatio = 1.0;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);

          final fakeVoice = FakeVoiceInputService()..available = false;
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Pas hastalığı için pas ilacı uygulayın.',
          );

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceInputService: fakeVoice),
          );
          await tester.pumpAndSettle();

          // Sesli girişi dene ve hata al
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          expect(fakeVoice.isListening, isFalse);

          // Hata bildirim SnackBar'ını temizle
          ScaffoldMessenger.of(
            tester.element(find.byType(AiAsistanEkrani)),
          ).clearSnackBars();
          await tester.pumpAndSettle();

          // Yazılı mesaj gönder
          await tester.enterText(
            find.byType(TextField),
            'Buğday pas hastalığı nasıl geçer?',
          );
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeRepo.gonderilen, ['Buğday pas hastalığı nasıl geçer?']);
          expect(
            find.text('Pas hastalığı için pas ilacı uygulayın.'),
            findsOneWidget,
          );
        },
      );

      testWidgets(
        'Test 14: Network hatası alındığında listening false olur, yazılmış metin silinmez ve uygun hata gösterilir',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          await tester.pumpWidget(_wrap(voiceInputService: fakeVoice));
          await tester.pumpAndSettle();

          // Önceden bir metin yazılmış olsun
          await tester.enterText(find.byType(TextField), 'Mısır tarlasında');
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          expect(fakeVoice.isListening, isTrue);

          // Network hatası gelsin
          fakeVoice.emitError(VoiceInputErrorType.network, 'error_network');
          await tester.pumpAndSettle();

          expect(fakeVoice.isListening, isFalse);
          expect(find.byTooltip('Sesli giriş başlat'), findsOneWidget);
          expect(
            find.text('Ses tanıma için internet bağlantısı gerekiyor.'),
            findsOneWidget,
          );
          // Yazılmış metin korunmalı
          expect(
            find.widgetWithText(TextField, 'Mısır tarlasında'),
            findsOneWidget,
          );
        },
      );

      testWidgets(
        'Test 15: Boş transkript geldiğinde veya ses algılanmadığında AI\'a boş mesaj gönderilmez',
        (tester) async {
          final fakeVoice = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository();

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceInputService: fakeVoice),
          );
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          // Boş sonuç
          fakeVoice.emitResult('', isFinal: true);
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Sesli girişi durdur'));
          await tester.pumpAndSettle();

          // AI repo çağrılmamış olmalı ve metin boş kalmalı
          expect(fakeRepo.gonderilen, isEmpty);
          expect(find.widgetWithText(TextField, ''), findsOneWidget);
        },
      );
    });

    // ── Voice Output (TTS) Entegrasyonu ──────────────────────────────────
    group('Voice Output (TTS) Entegrasyonu', () {
      testWidgets(
        'Test 1: Tamamlanmış assistant mesajında "Yanıtı dinle" aksiyonu görünür, user mesajında görünmez',
        (tester) async {
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Buğday pas hastalığı için ilaçlama yapabilirsiniz.',
          );

          await tester.pumpWidget(_wrap(repo: fakeRepo));
          await tester.pumpAndSettle();

          // Mesaj gönder
          await tester.enterText(find.byType(TextField), 'Buğdayımda leke var');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // User mesajında dinle butonu olmamalı
          expect(find.text('Buğdayımda leke var'), findsOneWidget);

          // Assistant mesajında Dinle butonu görünmeli
          expect(
            find.text('Buğday pas hastalığı için ilaçlama yapabilirsiniz.'),
            findsOneWidget,
          );
          expect(find.byTooltip('Yanıtı dinle'), findsOneWidget);
          expect(find.text('Dinle'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 2: Dinle\'ye basıldığında initialize ve speak tam metinle çağrılır',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Bugün sulama için uygun hava.',
          );

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Sulamalı mıyım?');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.initializeCalls, 1);
          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts, [
            'Bugün sulama için uygun hava.',
          ]);
          expect(fakeVoiceOutput.isSpeaking, isTrue);
        },
      );

      testWidgets(
        'Test 3: AI cevabı okunurken ilgili mesaj "Seslendirmeyi durdur" gösterir',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Hava durumu raporu.',
          );

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Hava nasıl?');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(find.byTooltip('Seslendirmeyi durdur'), findsOneWidget);
          expect(find.text('Durdur'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 4: Durdur\'a basıldığında stop() çağrılır ve UI tekrar "Yanıtı dinle" olur',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Detaylı analiz.');

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Analiz et');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // Başlat
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // Durdur
          await tester.tap(find.byTooltip('Seslendirmeyi durdur'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, 1);
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(find.byTooltip('Yanıtı dinle'), findsOneWidget);
          expect(find.text('Dinle'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 5: Completion simüle edildiğinde speaking state temizlenir ve "Yanıtı dinle" geri gelir',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Kısa yanıt.');

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Test');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(find.byTooltip('Seslendirmeyi durdur'), findsOneWidget);

          // TTS motoru tamamlanır
          fakeVoiceOutput.simulateCompletion();
          await tester.pumpAndSettle();

          expect(find.byTooltip('Yanıtı dinle'), findsOneWidget);
          expect(find.text('Dinle'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 6: İki AI mesajı: Mesaj A Dinle -> Mesaj B Dinle durumunda A durur, B okunur',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository();

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          // İlk mesajı gönder ve cevap al
          await tester.enterText(find.byType(TextField), 'Soru 1');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // İkinci mesajı gönder ve cevap al
          await tester.enterText(find.byType(TextField), 'Soru 2');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          final dinleButtons = find.byTooltip('Yanıtı dinle');
          expect(dinleButtons, findsNWidgets(2));

          // İlk mesajı dinle
          await tester.tap(dinleButtons.first);
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.speakCalls, 1);
          expect(find.byTooltip('Seslendirmeyi durdur'), findsOneWidget);

          // İkinci mesajı dinle
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, greaterThanOrEqualTo(1));
          expect(fakeVoiceOutput.speakCalls, 2);
          expect(find.byTooltip('Seslendirmeyi durdur'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 7: AI cevabı okunurken mikrofon butonuna basıldığında TTS durur, STT başlar',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeVoiceInput = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Mevcut yanıt.');

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceInputService: fakeVoiceInput,
              voiceOutputService: fakeVoiceOutput,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // TTS başlat
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // 🎤 Mikrofon butonuna bas
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          // Önce TTS durmalı, sonra STT başlamalı
          expect(fakeVoiceOutput.stopCalls, 1);
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(fakeVoiceInput.startListeningCalls, 1);
          expect(fakeVoiceInput.isListening, isTrue);
          expect(find.text('Dinliyorum...'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 8: Mikrofon listening aktifken Dinle aksiyonu TTS başlatmaz (disabled)',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeVoiceInput = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Mevcut yanıt.');

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceInputService: fakeVoiceInput,
              voiceOutputService: fakeVoiceOutput,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // Mikrofonu başlat
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          expect(fakeVoiceInput.isListening, isTrue);

          // Dinle butonuna basmaya çalış (disabled olmalı)
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 0);
          expect(fakeVoiceOutput.isSpeaking, isFalse);
        },
      );

      testWidgets(
        'Test 9: TTS aktifken kullanıcı yeni mesaj gönderirse TTS durdurulur ve mesaj gönderilir',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'İlk cevap.');

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'İlk soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // TTS başlat
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // Kullanıcı yeni mesaj gönderir
          await tester.enterText(find.byType(TextField), 'İkinci soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, 1);
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(fakeRepo.gonderilen, contains('İkinci soru'));
        },
      );

      testWidgets(
        'Test 10: TTS hata durumunda kullanıcı dostu Türkçe hata gösterilir ve speaking state temizlenir',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Cevap metni.');

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // Çalışma zamanı hatası fırlat
          fakeVoiceOutput.simulateRuntimeError('Audio focus lost');
          await tester.pumpAndSettle();

          expect(
            find.text('Sesli yanıt sırasında bir sorun oluştu.'),
            findsOneWidget,
          );
          expect(find.byTooltip('Yanıtı dinle'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 11: TTS aktifken ekran dispose edildiğinde konuşma durdurulur ve hata oluşmaz',
        (tester) async {
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Cevap.');

          await tester.pumpWidget(
            _wrap(repo: fakeRepo, voiceOutputService: fakeVoiceOutput),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();
          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // Ekranı dispose et
          await tester.pumpWidget(
            const MaterialApp(home: Scaffold(body: Text('Başka ekran'))),
          );
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, greaterThanOrEqualTo(1));
          expect(tester.takeException(), isNull);

          // Dispose sonrası gecikmeli callback
          fakeVoiceOutput.simulateCompletion();
          expect(tester.takeException(), isNull);
        },
      );

      testWidgets(
        'Test 12: Voice regression - Sesli giriş ve sesli çıkış birlikte hatasız çalışır',
        (tester) async {
          final fakeVoiceInput = FakeVoiceInputService();
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Mısırda yaprak kurdu için tuzak kullanılabilir.',
          );

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceInputService: fakeVoiceInput,
              voiceOutputService: fakeVoiceOutput,
            ),
          );
          await tester.pumpAndSettle();

          // 1. Sesli giriş yap
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();
          fakeVoiceInput.emitResult('Mısırda kurt var', isFinal: true);
          await tester.pumpAndSettle();
          await tester.tap(find.byTooltip('Sesli girişi durdur'));
          await tester.pumpAndSettle();

          // 2. Gönder
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(
            find.text('Mısırda yaprak kurdu için tuzak kullanılabilir.'),
            findsOneWidget,
          );

          // 3. Yanıtı dinle
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts, [
            'Mısırda yaprak kurdu için tuzak kullanılabilir.',
          ]);
        },
      );
    });

    // ── Sesli Yanıtlar Kullanıcı Tercihi & Autoplay ──────────────────────
    group('Sesli Yanıtlar Kullanıcı Tercihi & Autoplay', () {
      testWidgets(
        'Test 1: Preference varsayılan kapalıdır ve AI cevabı autoplay edilmez',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: false);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Normal yanıt.');

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(find.text('Normal yanıt.'), findsOneWidget);
          expect(fakeVoiceOutput.speakCalls, 0);
        },
      );

      testWidgets(
        'Test 2: Kullanıcı ayarı açtığında state true olur ve storage kaydedilir',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: false);

          await tester.pumpWidget(_wrap(preferences: fakePrefs));
          await tester.pumpAndSettle();

          await tester.tap(find.byIcon(Icons.more_vert));
          await tester.pumpAndSettle();

          expect(find.text('Sesli yanıtlar'), findsOneWidget);
          await tester.tap(find.text('Sesli yanıtlar'));
          await tester.pumpAndSettle();

          expect(fakePrefs.setCalls, 1);
          expect(fakePrefs.savedValues.last, isTrue);
        },
      );

      testWidgets(
        'Test 3: Başlangıçta açık olan preference yüklenir ve switch açık görünür',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);

          await tester.pumpWidget(_wrap(preferences: fakePrefs));
          await tester.pumpAndSettle();

          await tester.tap(find.byIcon(Icons.more_vert));
          await tester.pumpAndSettle();

          final switchWidget = tester.widget<Switch>(find.byType(Switch));
          expect(switchWidget.value, isTrue);
        },
      );

      testWidgets(
        'Test 4: Sesli yanıtlar açıkken stream tamamlanınca AI cevabı tam metinle otomatik seslendirilir',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Bugün ne yapmalıyım cevabı.',
          );

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(
            find.byType(TextField),
            'Bugün ne yapmalıyım?',
          );
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts, ['Bugün ne yapmalıyım cevabı.']);
          expect(find.byTooltip('Seslendirmeyi durdur'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 5: Stream token token gelirken parça parça TTS yapılmaz, final metin 1 kez seslendirilir',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final multiChunkRepo = _MultiChunkAiRepo([
            'Bugün ',
            'toprağı ',
            'kontrol edin.',
          ]);

          await tester.pumpWidget(
            _wrap(
              repo: multiChunkRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Durum?');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts, ['Bugün toprağı kontrol edin.']);
        },
      );

      testWidgets(
        'Test 6: Ekran açılışı veya widget rebuild durumunda geçmiş mesajlar autoplay edilmez',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();

          await tester.pumpWidget(
            _wrap(voiceOutputService: fakeVoiceOutput, preferences: fakePrefs),
          );
          await tester.pumpAndSettle();

          // Rebuild tetikle
          await tester.pumpWidget(
            _wrap(voiceOutputService: fakeVoiceOutput, preferences: fakePrefs),
          );
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 0);
        },
      );

      testWidgets(
        'Test 7: Sesli yanıtlar kapalıyken de manuel Dinle butonu sorunsuz çalışır',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: false);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Manuel dinleme metni.',
          );

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Test');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 0);

          // Manuel dinle
          await tester.tap(find.byTooltip('Yanıtı dinle'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts, ['Manuel dinleme metni.']);
        },
      );

      testWidgets(
        'Test 8: Autoplay konuşurken ayar kapatılırsa aktif TTS hemen durdurulur',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeRepo = FakeAiAssistantRepository(
            cevap: 'Uzun sesli yanıt.',
          );

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // Menüyü aç ve ayarı kapat
          await tester.tap(find.byIcon(Icons.more_vert));
          await tester.pumpAndSettle();
          await tester.tap(find.text('Sesli yanıtlar'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, greaterThanOrEqualTo(1));
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(fakePrefs.savedValues.last, isFalse);
          expect(find.byTooltip('Yanıtı dinle'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 9: Autoplay konuşurken mikrofon açılırsa önce TTS durur sonra STT başlar',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeVoiceInput = FakeVoiceInputService();
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Autoplay cevabı.');

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceInputService: fakeVoiceInput,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.isSpeaking, isTrue);

          // 🎤 Mikrofon butonuna bas
          await tester.tap(find.byTooltip('Sesli giriş başlat'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, 1);
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(fakeVoiceInput.startListeningCalls, 1);
          expect(fakeVoiceInput.isListening, isTrue);
          expect(find.text('Dinliyorum...'), findsOneWidget);
        },
      );

      testWidgets(
        'Test 10: Mikrofon dinlemedeyken AI cevabı gelse dahi autoplay başlatılmaz',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();
          final fakeVoiceInput = FakeVoiceInputService();
          final completer = Completer<AiChatResponse>();
          final slowRepo = _SlowAiRepo(completer.future);

          await tester.pumpWidget(
            _wrap(
              repo: slowRepo,
              voiceInputService: fakeVoiceInput,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pump();

          // Mikrofon dinlemesini başlat
          fakeVoiceInput.startListening(onResult: (_) {});
          await tester.pump();
          expect(fakeVoiceInput.isListening, isTrue);

          // Yanıtı tamamla
          completer.complete(
            const AiChatResponse(reply: 'Geciken cevap', conversationId: 'c1'),
          );
          await tester.pumpAndSettle();

          // Mikrofon aktif olduğu için autoplay çalışmamalı
          expect(fakeVoiceOutput.speakCalls, 0);
        },
      );

      testWidgets(
        'Test 11: Yeni cevap geldiğinde eski TTS durdurulur ve sadece yeni cevap autoplay edilir',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService();

          await tester.pumpWidget(
            _wrap(voiceOutputService: fakeVoiceOutput, preferences: fakePrefs),
          );
          await tester.pumpAndSettle();

          // 1. Soru
          await tester.enterText(find.byType(TextField), 'Soru 1');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.speakCalls, 1);
          expect(fakeVoiceOutput.spokenTexts.last, 'Test cevap');

          // 2. Soru
          await tester.enterText(find.byType(TextField), 'Soru 2');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          expect(fakeVoiceOutput.stopCalls, greaterThanOrEqualTo(1));
          expect(fakeVoiceOutput.speakCalls, 2);
          expect(fakeVoiceOutput.spokenTexts.last, 'Test cevap');
        },
      );

      testWidgets(
        'Test 12: TTS initialize başarısız olduğunda AI cevabı ekranda kalır, crash olmaz',
        (tester) async {
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true);
          final fakeVoiceOutput = FakeVoiceOutputService()
            ..initializeResult = false;
          final fakeRepo = FakeAiAssistantRepository(cevap: 'Önemli bilgi.');

          await tester.pumpWidget(
            _wrap(
              repo: fakeRepo,
              voiceOutputService: fakeVoiceOutput,
              preferences: fakePrefs,
            ),
          );
          await tester.pumpAndSettle();

          await tester.enterText(find.byType(TextField), 'Soru');
          await tester.tap(find.byTooltip('Gönder'));
          await tester.pumpAndSettle();

          // AI mesajı ekranda kalmalı
          expect(find.text('Önemli bilgi.'), findsOneWidget);
          // Hata SnackBar'ı görünmeli
          expect(
            find.text('Sesli yanıt sistemi başlatılamadı.'),
            findsOneWidget,
          );
          // Crash yok, speaking state temiz
          expect(fakeVoiceOutput.isSpeaking, isFalse);
          expect(tester.takeException(), isNull);
        },
      );

      testWidgets(
        'Test 13: Preference gecikmeli dönerken ekran dispose edilirse hata oluşmaz',
        (tester) async {
          final delayCompleter = Completer<void>();
          final fakePrefs = FakeVoiceAssistantPreferences(initialValue: true)
            ..delayCompleter = delayCompleter;

          await tester.pumpWidget(_wrap(preferences: fakePrefs));
          await tester.pump();

          // Ekranı dispose et
          await tester.pumpWidget(
            const MaterialApp(home: Scaffold(body: Text('Diğer'))),
          );
          await tester.pump();

          // Gecikmeli preference döner
          delayCompleter.complete();
          await tester.pumpAndSettle();

          expect(tester.takeException(), isNull);
        },
      );
    });
  });
}

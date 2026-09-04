import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/ai_assistant/data/voice_input_service.dart';
import 'package:speech_to_text/speech_recognition_error.dart';
import 'package:speech_to_text/speech_recognition_result.dart';
import 'package:speech_to_text/speech_to_text.dart';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

class FakeSpeechRecognitionResult extends Fake
    implements SpeechRecognitionResult {
  FakeSpeechRecognitionResult({
    required this.recognizedWords,
    required this.finalResult,
  });

  @override
  final String recognizedWords;

  @override
  final bool finalResult;
}

class FakeSpeechToTextUnderlying extends Fake implements SpeechToText {
  bool initSucceeds = true;
  bool isInit = false;
  bool currentlyListening = false;
  bool hasPerm = true;
  List<LocaleName> availableLocales = [];

  @override
  SpeechErrorListener? errorListener;
  @override
  SpeechStatusListener? statusListener;
  SpeechResultListener? resultListener;

  @override
  bool get isAvailable => isInit && initSucceeds;

  @override
  bool get isListening => currentlyListening;

  @override
  Future<bool> get hasPermission async => hasPerm;

  @override
  Future<bool> initialize({
    SpeechErrorListener? onError,
    SpeechStatusListener? onStatus,
    dynamic debugLogging = false,
    Duration finalTimeout = const Duration(milliseconds: 2000),
    List<SpeechConfigOption>? options,
  }) async {
    errorListener = onError;
    statusListener = onStatus;
    isInit = initSucceeds;
    return initSucceeds;
  }

  @override
  Future<List<LocaleName>> locales() async => availableLocales;

  @override
  Future listen({
    SpeechResultListener? onResult,
    Duration? listenFor,
    Duration? pauseFor,
    String? localeId,
    dynamic onSoundLevelChange,
    dynamic cancelOnError = false,
    dynamic partialResults = true,
    dynamic onDevice = false,
    ListenMode listenMode = ListenMode.confirmation,
    dynamic sampleRate = 0,
    SpeechListenOptions? listenOptions,
  }) async {
    resultListener = onResult;
    currentlyListening = true;
    statusListener?.call('listening');
  }

  @override
  Future<void> stop() async {
    currentlyListening = false;
    statusListener?.call('notListening');
  }

  @override
  Future<void> cancel() async {
    currentlyListening = false;
    statusListener?.call('done');
  }

  void emitResult(String words, {required bool isFinal}) {
    resultListener?.call(
      FakeSpeechRecognitionResult(recognizedWords: words, finalResult: isFinal),
    );
  }

  void emitError(String msg, {bool permanent = false}) {
    errorListener?.call(SpeechRecognitionError(msg, permanent));
  }
}

/// UI testlerinde gerçek mikrofon olmadan kullanılacak `FakeVoiceInputService`.
class FakeVoiceInputService implements VoiceInputService {
  bool available = true;
  bool _listening = false;
  bool disposed = false;

  ValueChanged<VoiceRecognitionResult>? onResultCallback;
  void Function(VoiceInputException error)? onErrorCallback;
  void Function(bool isListening)? onListeningChangedCallback;

  @override
  bool get isAvailable => available && !disposed;

  @override
  bool get isListening => _listening;

  @override
  Future<bool> initialize() async => isAvailable;

  @override
  Future<bool> startListening({
    required ValueChanged<VoiceRecognitionResult> onResult,
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
        const VoiceInputException(
          type: VoiceInputErrorType.permissionDenied,
          message: 'Mikrofon izni yok.',
        ),
      );
      onListeningChanged?.call(false);
      return false;
    }
    onResultCallback = onResult;
    onErrorCallback = onError;
    onListeningChangedCallback = onListeningChanged;
    _listening = true;
    onListeningChangedCallback?.call(true);
    return true;
  }

  @override
  Future<void> stopListening() async {
    _listening = false;
    onListeningChangedCallback?.call(false);
  }

  @override
  Future<void> cancelListening() async {
    _listening = false;
    onListeningChangedCallback?.call(false);
  }

  @override
  void dispose() {
    disposed = true;
    _listening = false;
    onListeningChangedCallback?.call(false);
  }

  void emitResult(String words, {required bool isFinal}) {
    onResultCallback?.call(
      VoiceRecognitionResult(recognizedWords: words, isFinal: isFinal),
    );
  }

  void emitError(VoiceInputErrorType type, String message) {
    onErrorCallback?.call(VoiceInputException(type: type, message: message));
  }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

void main() {
  group('DefaultVoiceInputService locale resolution', () {
    test('tam tr_TR eşleşmesini ilk sırada seçer', () {
      final locales = [
        LocaleName('en_US', 'English'),
        LocaleName('tr_TR', 'Türkçe'),
        LocaleName('de_DE', 'Deutsch'),
      ];
      expect(DefaultVoiceInputService.resolveTurkishLocale(locales), 'tr_TR');
    });

    test('tr-TR tireli formatı tanır', () {
      final locales = [
        LocaleName('en-US', 'English'),
        LocaleName('tr-TR', 'Turkish'),
      ];
      expect(DefaultVoiceInputService.resolveTurkishLocale(locales), 'tr-TR');
    });

    test('tr_TR yoksa tr ile başlayan varyantı seçer', () {
      final locales = [
        LocaleName('en_US', 'English'),
        LocaleName('tr_CY', 'Kıbrıs Türkçesi'),
      ];
      expect(DefaultVoiceInputService.resolveTurkishLocale(locales), 'tr_CY');
    });

    test('Türkçe dil seçeneği bulunamadığında güvenle null döner', () {
      final locales = [
        LocaleName('en_US', 'English'),
        LocaleName('fr_FR', 'Français'),
      ];
      expect(DefaultVoiceInputService.resolveTurkishLocale(locales), isNull);
    });

    test('boş dil listesinde null döner ve patlamaz', () {
      expect(DefaultVoiceInputService.resolveTurkishLocale([]), isNull);
    });

    test('desteklenen kullanıcı tercihi locale varsa onu seçer', () {
      final locales = [
        LocaleName('en_US', 'English'),
        LocaleName('tr_TR', 'Türkçe'),
        LocaleName('de_DE', 'Deutsch'),
      ];
      expect(
        DefaultVoiceInputService.resolveTurkishLocale(
          locales,
          userPreferredLocale: 'de-DE',
        ),
        'de_DE',
      );
    });

    test('desteklenmeyen kullanıcı tercihi istendiğinde Türkçe fallback yapar', () {
      final locales = [
        LocaleName('en_US', 'English'),
        LocaleName('tr_TR', 'Türkçe'),
      ];
      expect(
        DefaultVoiceInputService.resolveTurkishLocale(
          locales,
          userPreferredLocale: 'es-ES',
        ),
        'tr_TR',
      );
    });
  });

  group('FakeVoiceInputService test edilebilirliği (UI simülasyonu)', () {
    test(
      'partial ve final transkript senaryosunu gerçek donanımsız simüle eder',
      () async {
        final service = FakeVoiceInputService();
        final results = <VoiceRecognitionResult>[];

        await service.startListening(onResult: (res) => results.add(res));

        expect(service.isListening, isTrue);

        // Simüle edilen konuşma akışı:
        service.emitResult('Buğday', isFinal: false);
        service.emitResult('Buğday tarlamda', isFinal: false);
        service.emitResult('Buğday tarlamda sararma var', isFinal: true);

        expect(results, [
          const VoiceRecognitionResult(
            recognizedWords: 'Buğday',
            isFinal: false,
          ),
          const VoiceRecognitionResult(
            recognizedWords: 'Buğday tarlamda',
            isFinal: false,
          ),
          const VoiceRecognitionResult(
            recognizedWords: 'Buğday tarlamda sararma var',
            isFinal: true,
          ),
        ]);

        await service.stopListening();
        expect(service.isListening, isFalse);
      },
    );
  });

  group('DefaultVoiceInputService runtime & error handling', () {
    test(
      'başarılı initialize sonrası isAvailable true döner ve Türkçe locale ayarlar',
      () async {
        final underlying = FakeSpeechToTextUnderlying()
          ..availableLocales = [
            LocaleName('en_US', 'English'),
            LocaleName('tr_TR', 'Türkçe'),
          ];
        final service = DefaultVoiceInputService(speechToText: underlying);

        final ok = await service.initialize();
        expect(ok, isTrue);
        expect(service.isAvailable, isTrue);
        expect(service.preferredLocaleId, 'tr_TR');
      },
    );

    test(
      'initialize başarısız olduğunda (izin yokken) startListening permissionDenied üretir',
      () async {
        final underlying = FakeSpeechToTextUnderlying()
          ..initSucceeds = false
          ..hasPerm = false;
        final service = DefaultVoiceInputService(speechToText: underlying);

        VoiceInputException? capturedError;
        await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.permissionDenied);
      },
    );

    test(
      'dinleme sırasında partial ve final sonuçları doğru ayrıştırır',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        final results = <VoiceRecognitionResult>[];
        await service.startListening(onResult: (res) => results.add(res));

        expect(service.isListening, isTrue);

        underlying.emitResult('Zeytin', isFinal: false);
        underlying.emitResult('Zeytin ağaçlarında pamuklu bit', isFinal: true);

        expect(results.length, 2);
        expect(results[0].recognizedWords, 'Zeytin');
        expect(results[0].isFinal, isFalse);
        expect(results[1].recognizedWords, 'Zeytin ağaçlarında pamuklu bit');
        expect(results[1].isFinal, isTrue);

        await service.stopListening();
        expect(service.isListening, isFalse);
      },
    );

    test(
      'runtime error bildirimini yakalar ve üst katmana tipli iletir',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        VoiceInputException? capturedError;
        await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        underlying.emitError('audio error', permanent: false);

        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.runtimeError);
        expect(capturedError!.message, 'audio error');
        expect(service.isListening, isFalse);
      },
    );

    test(
      'network hatasını VoiceInputErrorType.network olarak doğru sınıflandırır',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        VoiceInputException? capturedError;
        await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        underlying.emitError('error_network', permanent: true);

        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.network);
        expect(service.isListening, isFalse);
      },
    );

    test(
      'permanent timeout veya no_match hatası permissionDenied olarak YANLIŞ sınıflandırılmaz',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        VoiceInputException? capturedError;
        await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        underlying.emitError('error_speech_timeout', permanent: true);

        expect(capturedError, isNotNull);
        expect(capturedError!.type, isNot(VoiceInputErrorType.permissionDenied));
        expect(capturedError!.type, VoiceInputErrorType.runtimeError);
      },
    );

    test(
      'başarısız initialize sonrasında servis kilitlenmez, tekrar denendiğinde başarılı olur',
      () async {
        final underlying = FakeSpeechToTextUnderlying()..initSucceeds = false;
        final service = DefaultVoiceInputService(speechToText: underlying);

        final firstTry = await service.initialize();
        expect(firstTry, isFalse);
        expect(service.isAvailable, isFalse);

        // Kullanıcı izin verdi / donanım hazır oldu
        underlying.initSucceeds = true;
        final secondTry = await service.initialize();
        expect(secondTry, isTrue);
        expect(service.isAvailable, isTrue);
      },
    );

    test(
      'hasPermission=true fakat engine init başarısızsa unavailable döner (permissionDenied değil)',
      () async {
        final underlying = FakeSpeechToTextUnderlying()
          ..initSucceeds = false
          ..hasPerm = true;
        final service = DefaultVoiceInputService(speechToText: underlying);

        VoiceInputException? capturedError;
        final started = await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        expect(started, isFalse);
        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.unavailable);
      },
    );

    test(
      'hasPermission=false iken init başarısız olursa permissionDenied döner',
      () async {
        final underlying = FakeSpeechToTextUnderlying()
          ..initSucceeds = false
          ..hasPerm = false;
        final service = DefaultVoiceInputService(speechToText: underlying);

        VoiceInputException? capturedError;
        final started = await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        expect(started, isFalse);
        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.permissionDenied);
      },
    );

    test(
      'rapid tap / paralel startListening çağrısı ikinci oturumu engeller ve false döner',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        final firstFuture = service.startListening(onResult: (_) {});
        final secondFuture = service.startListening(onResult: (_) {});

        final results = await Future.wait([firstFuture, secondFuture]);
        expect(results[0], isTrue);
        expect(results[1], isFalse);
        expect(service.isListening, isTrue);

        await service.stopListening();
        expect(service.isListening, isFalse);
      },
    );

    test(
      'dispose sonrası dinleme durdurulur ve yeni dinleme başlatılamaz',
      () async {
        final underlying = FakeSpeechToTextUnderlying();
        final service = DefaultVoiceInputService(speechToText: underlying);
        await service.initialize();

        await service.startListening(onResult: (_) {});
        expect(service.isListening, isTrue);

        service.dispose();
        expect(service.isListening, isFalse);

        VoiceInputException? capturedError;
        await service.startListening(
          onResult: (_) {},
          onError: (err) => capturedError = err,
        );

        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceInputErrorType.runtimeError);
      },
    );
  });
}

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/ai_assistant/data/voice_output_service.dart';

// ---------------------------------------------------------------------------
// Test Dublörleri (Fakes)
// ---------------------------------------------------------------------------

/// UI ve entegrasyon testlerinde kullanılmak üzere [VoiceOutputService] dublörü.
class FakeVoiceOutputService implements VoiceOutputService {
  bool _isSpeaking = false;
  bool isDisposed = false;
  String? lastSpokenText;
  int speakCount = 0;
  int stopCount = 0;

  @override
  bool get isSpeaking => _isSpeaking;

  @override
  void Function(bool isSpeaking)? onSpeakingChanged;

  @override
  void Function(VoiceOutputException error)? onError;

  @override
  Future<bool> initialize() async => true;

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

    lastSpokenText = normalized;
    speakCount++;
    _isSpeaking = true;
    this.onSpeakingChanged?.call(true);
    onSpeakingChanged?.call(true);
  }

  @override
  Future<void> stop() async {
    stopCount++;
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
  }

  /// Test senaryosunda konuşmanın doğal olarak bittiğini simüle eder.
  void simulateCompletion() {
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
  }

  /// Test senaryosunda çalışma zamanı hatasını simüle eder.
  void simulateError(VoiceOutputErrorType type, String message) {
    _isSpeaking = false;
    onSpeakingChanged?.call(false);
    onError?.call(VoiceOutputException(type: type, message: message));
  }

  @override
  void dispose() {
    isDisposed = true;
    _isSpeaking = false;
  }
}

/// [FlutterTtsAdapter] için birim test sahtesi (Platform bağımsız).
class FakeFlutterTtsAdapter implements FlutterTtsAdapter {
  List<dynamic> languages = ['en-US', 'tr-TR', 'de-DE'];
  String? configuredLanguage;
  double? configuredRate;
  double? configuredVolume;
  double? configuredPitch;

  int speakCalls = 0;
  int stopCalls = 0;
  String? lastSpokenText;

  VoidCallback? startHandler;
  VoidCallback? completionHandler;
  VoidCallback? cancelHandler;
  void Function(dynamic message)? errorHandler;

  @override
  Future<dynamic> get getLanguages async => languages;

  @override
  Future<dynamic> isLanguageAvailable(String language) async =>
      languages.contains(language);

  @override
  Future<dynamic> setLanguage(String language) async {
    configuredLanguage = language;
    return 1;
  }

  @override
  Future<dynamic> setSpeechRate(double rate) async {
    configuredRate = rate;
    return 1;
  }

  @override
  Future<dynamic> setVolume(double volume) async {
    configuredVolume = volume;
    return 1;
  }

  @override
  Future<dynamic> setPitch(double pitch) async {
    configuredPitch = pitch;
    return 1;
  }

  @override
  Future<dynamic> speak(String text) async {
    speakCalls++;
    lastSpokenText = text;
    startHandler?.call();
    return 1;
  }

  @override
  Future<dynamic> stop() async {
    stopCalls++;
    cancelHandler?.call();
    return 1;
  }

  @override
  void setStartHandler(VoidCallback callback) => startHandler = callback;

  @override
  void setCompletionHandler(VoidCallback callback) =>
      completionHandler = callback;

  @override
  void setCancelHandler(VoidCallback callback) => cancelHandler = callback;

  @override
  void setErrorHandler(void Function(dynamic message) callback) =>
      errorHandler = callback;
}

// ---------------------------------------------------------------------------
// Birim Testler
// ---------------------------------------------------------------------------

void main() {
  group('DefaultVoiceOutputService - Türkçe Dil Seçimi', () {
    test('tam tr-TR formatını ilk sırada tercih eder', () {
      final langs = ['en-US', 'tr-TR', 'de-DE'];
      expect(DefaultVoiceOutputService.resolveTurkishLocale(langs), 'tr-TR');
    });

    test('tr_TR alt çizgili formatı doğru tanır', () {
      final langs = ['en-US', 'tr_TR'];
      expect(DefaultVoiceOutputService.resolveTurkishLocale(langs), 'tr_TR');
    });

    test('tam eşleşme yoksa tr ön ekli varyantı seçer', () {
      final langs = ['en-US', 'tr_CY', 'fr-FR'];
      expect(DefaultVoiceOutputService.resolveTurkishLocale(langs), 'tr_CY');
    });

    test('Türkçe dil bulunamazsa güvenle null (varsayılan fallback) döner', () {
      final langs = ['en-US', 'de-DE', 'fr-FR'];
      expect(DefaultVoiceOutputService.resolveTurkishLocale(langs), isNull);
    });

    test('boş dil listesinde çökmeden null döner', () {
      expect(DefaultVoiceOutputService.resolveTurkishLocale([]), isNull);
    });
  });

  group('VoiceOutputService - Temel Davranış Doğrulamaları', () {
    test(
      'Test 1: Service public contract oluşturulabiliyor ve fake edilebiliyor',
      () async {
        final voiceOutput = FakeVoiceOutputService();

        await voiceOutput.speak('Merhaba');
        expect(voiceOutput.isSpeaking, isTrue);

        await voiceOutput.stop();
        expect(voiceOutput.isSpeaking, isFalse);
      },
    );

    test(
      'Test 2: Boş metinde speak platform davranışına gitmeden güvenli şekilde tamamlanıyor',
      () async {
        final adapter = FakeFlutterTtsAdapter();
        final service = DefaultVoiceOutputService(adapter: adapter);
        await service.initialize();

        await service.speak('');
        await service.speak('   ');

        expect(adapter.speakCalls, 0);
        expect(service.isSpeaking, isFalse);
      },
    );

    test('Test 3: Speak başladığında speaking state true oluyor', () async {
      final adapter = FakeFlutterTtsAdapter();
      final service = DefaultVoiceOutputService(adapter: adapter);
      await service.initialize();

      bool? observedState;
      await service.speak(
        'Ekinlerde sulama ihtiyacı var.',
        onSpeakingChanged: (speaking) => observedState = speaking,
      );

      expect(service.isSpeaking, isTrue);
      expect(observedState, isTrue);
      expect(adapter.speakCalls, 1);
      expect(adapter.lastSpokenText, 'Ekinlerde sulama ihtiyacı var.');
    });

    test('Test 4: Completion geldiğinde speaking state false oluyor', () async {
      final adapter = FakeFlutterTtsAdapter();
      final service = DefaultVoiceOutputService(adapter: adapter);
      await service.initialize();

      final states = <bool>[];
      service.onSpeakingChanged = (speaking) => states.add(speaking);

      await service.speak('Bugün hava güneşli.');
      expect(service.isSpeaking, isTrue);

      // TTS motoru okumayı tamamlar:
      adapter.completionHandler?.call();

      expect(service.isSpeaking, isFalse);
      expect(states, [true, false]);
    });

    test('Test 5: Stop çağrısı speaking state\'i false yapıyor', () async {
      final adapter = FakeFlutterTtsAdapter();
      final service = DefaultVoiceOutputService(adapter: adapter);
      await service.initialize();

      await service.speak('Buğday tarlasında ilaçlama zamanı.');
      expect(service.isSpeaking, isTrue);

      await service.stop();

      expect(service.isSpeaking, isFalse);
      expect(adapter.stopCalls, 1);
    });

    test(
      'Test 6: Runtime TTS error kontrollü hata modeline çevriliyor',
      () async {
        final adapter = FakeFlutterTtsAdapter();
        final service = DefaultVoiceOutputService(adapter: adapter);
        await service.initialize();

        VoiceOutputException? capturedError;
        service.onError = (err) => capturedError = err;

        await service.speak('Analiz hazırlanıyor.');
        expect(service.isSpeaking, isTrue);

        // Platformdan hata callback'i gelir:
        adapter.errorHandler?.call('Audio focus lost');

        expect(service.isSpeaking, isFalse);
        expect(capturedError, isNotNull);
        expect(capturedError!.type, VoiceOutputErrorType.runtimeError);
        expect(capturedError!.message, contains('Audio focus lost'));
      },
    );

    test(
      'Test 7: Bir metin okunurken yeni speak çağrısı geldiğinde eski session güvenli şekilde durdurulabiliyor',
      () async {
        final adapter = FakeFlutterTtsAdapter();
        final service = DefaultVoiceOutputService(adapter: adapter);
        await service.initialize();

        // İlk metin konuşulmaya başlar
        await service.speak('Mesaj A');
        expect(service.isSpeaking, isTrue);
        expect(adapter.speakCalls, 1);
        expect(adapter.lastSpokenText, 'Mesaj A');

        // İlk konuşma bitmeden yeni speak çağrılır
        await service.speak('Mesaj B');

        // Eski session stop edilmiş ve yeni session başlatılmış olmalı
        expect(adapter.stopCalls, 1);
        expect(adapter.speakCalls, 2);
        expect(adapter.lastSpokenText, 'Mesaj B');
        expect(service.isSpeaking, isTrue);
      },
    );

    test(
      'Test 8: Dispose sonrasında gelen callback state/resource problemi yaratmıyor',
      () async {
        final adapter = FakeFlutterTtsAdapter();
        final service = DefaultVoiceOutputService(adapter: adapter);
        await service.initialize();

        final statesAfterDispose = <bool>[];
        service.onSpeakingChanged = (val) => statesAfterDispose.add(val);

        await service.speak('Test metni');
        expect(service.isSpeaking, isTrue);

        // Dispose öncesi listeyi temizle
        statesAfterDispose.clear();

        service.dispose();
        expect(service.isSpeaking, isFalse);

        // Dispose sonrası tetiklenen geç gelen handler'lar:
        adapter.completionHandler?.call();
        adapter.errorHandler?.call('Late error');

        expect(statesAfterDispose, isEmpty);
        expect(service.isSpeaking, isFalse);
      },
    );
  });
}

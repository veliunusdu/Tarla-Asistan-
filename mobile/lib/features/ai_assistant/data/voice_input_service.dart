import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:speech_to_text/speech_recognition_error.dart';
import 'package:speech_to_text/speech_recognition_result.dart';
import 'package:speech_to_text/speech_to_text.dart';

/// Konuşma tanıma sonucunu (ara veya nihai transkript) temsil eder.
class VoiceRecognitionResult {
  const VoiceRecognitionResult({
    required this.recognizedWords,
    required this.isFinal,
  });

  final String recognizedWords;
  final bool isFinal;

  @override
  String toString() =>
      'VoiceRecognitionResult(recognizedWords: $recognizedWords, isFinal: $isFinal)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is VoiceRecognitionResult &&
          runtimeType == other.runtimeType &&
          recognizedWords == other.recognizedWords &&
          isFinal == other.isFinal;

  @override
  int get hashCode => Object.hash(recognizedWords, isFinal);
}

/// Sesli giriş hata türleri.
enum VoiceInputErrorType {
  permissionDenied,
  unavailable,
  initializeFailed,
  startListeningFailed,
  runtimeError,
  notListening,
}

/// Sesli giriş işleminde oluşan güvenli ve tipli hata nesnesi.
class VoiceInputException implements Exception {
  const VoiceInputException({
    required this.type,
    required this.message,
    this.cause,
  });

  final VoiceInputErrorType type;
  final String message;
  final Object? cause;

  @override
  String toString() => 'VoiceInputException($type): $message';
}

/// Mikrofon sesini metne dönüştüren servis sözleşmesi.
///
/// AI veya backend bağımlılığı içermez. Yalnızca ses-metin dönüşümü ve
/// donanım/izin yönetimi sağlar.
abstract class VoiceInputService {
  /// Servisi ilklendirir ve mikrofon izinlerini/kullanılabilirliğini kontrol eder.
  Future<bool> initialize();

  /// Dinlemeyi başlatır.
  ///
  /// [onResult] ara (partial) ve nihai (final) metin sonuçlarını bildirir.
  /// [onError] platform veya donanım hatalarını fırlatmadan üst katmana iletir.
  /// [onListeningChanged] dinleme durumu başladığında (true) ve bittiğinde (false) tetiklenir.
  Future<void> startListening({
    required ValueChanged<VoiceRecognitionResult> onResult,
    void Function(VoiceInputException error)? onError,
    void Function(bool isListening)? onListeningChanged,
  });

  /// Aktif dinlemeyi sonlandırır ve konuşmayı durdurur.
  Future<void> stopListening();

  /// Dinlemeyi iptal eder ve son tanınan oturumu temizler.
  Future<void> cancelListening();

  /// Servisin o an dinleme yapıp yapmadığı.
  bool get isListening;

  /// Konuşma tanıma motorunun kullanılabilir ve izinli olup olmadığı.
  bool get isAvailable;

  /// Servis kaynaklarını temizler ve dinlemeyi güvenli şekilde durdurur.
  void dispose();
}

/// [SpeechToText] paketini kullanan varsayılan [VoiceInputService] implementasyonu.
class DefaultVoiceInputService implements VoiceInputService {
  DefaultVoiceInputService({SpeechToText? speechToText})
    : _speechToText = speechToText ?? SpeechToText();

  final SpeechToText _speechToText;
  bool _isInitialized = false;
  bool _isListening = false;
  bool _isDisposed = false;
  String? _preferredLocaleId;

  ValueChanged<VoiceRecognitionResult>? _onResult;
  void Function(VoiceInputException error)? _onError;
  void Function(bool isListening)? _onListeningChanged;

  @override
  bool get isListening => _isListening;

  @override
  bool get isAvailable => _isInitialized && _speechToText.isAvailable;

  @visibleForTesting
  String? get preferredLocaleId => _preferredLocaleId;

  @override
  Future<bool> initialize() async {
    if (_isDisposed) return false;
    if (_isInitialized) return isAvailable;

    try {
      final hasPermission = await _speechToText.initialize(
        onError: _handleSpeechError,
        onStatus: _handleSpeechStatus,
      );

      _isInitialized = true;

      if (hasPermission) {
        await _resolveTurkishLocale();
      }

      return isAvailable;
    } catch (e) {
      _isInitialized = false;
      return false;
    }
  }

  Future<void> _resolveTurkishLocale() async {
    try {
      final locales = await _speechToText.locales();
      _preferredLocaleId = resolveTurkishLocale(locales);
    } catch (_) {
      _preferredLocaleId = null;
    }
  }

  /// Cihazda bulunan diller arasından Türkçe yerelini güvenle seçer.
  ///
  /// Sırasıyla tam `tr_TR`/`tr-TR`, ardından herhangi bir `tr_...` varyantı
  /// ve son olarak `tr` ön ekini arar. Bulunamazsa cihazın varsayılan dilini
  /// kullanmak üzere `null` döner.
  @visibleForTesting
  static String? resolveTurkishLocale(List<LocaleName> locales) {
    if (locales.isEmpty) return null;

    // 1. Öncelik: Tam 'tr_TR' veya 'tr-TR' eşleşmesi
    for (final loc in locales) {
      final normalized = loc.localeId.toLowerCase().replaceAll('-', '_');
      if (normalized == 'tr_tr') {
        return loc.localeId;
      }
    }

    // 2. Öncelik: 'tr_' ile başlayan bölgesel varyantlar (ör. tr_CY)
    for (final loc in locales) {
      final normalized = loc.localeId.toLowerCase().replaceAll('-', '_');
      if (normalized.startsWith('tr_') || normalized.startsWith('tr-')) {
        return loc.localeId;
      }
    }

    // 3. Öncelik: 'tr' ile başlayan herhangi bir locale kodu
    for (final loc in locales) {
      if (loc.localeId.toLowerCase().startsWith('tr')) {
        return loc.localeId;
      }
    }

    return null;
  }

  @override
  Future<void> startListening({
    required ValueChanged<VoiceRecognitionResult> onResult,
    void Function(VoiceInputException error)? onError,
    void Function(bool isListening)? onListeningChanged,
  }) async {
    if (_isDisposed) {
      onError?.call(
        const VoiceInputException(
          type: VoiceInputErrorType.runtimeError,
          message: 'VoiceInputService disposed edilmiş durumda.',
        ),
      );
      return;
    }

    _onResult = onResult;
    _onError = onError;
    _onListeningChanged = onListeningChanged;

    if (!_isInitialized) {
      final ok = await initialize();
      if (!ok) {
        final err = VoiceInputException(
          type: VoiceInputErrorType.permissionDenied,
          message:
              'Mikrofon izni verilmedi veya konuşma tanıma servisi kullanılamıyor.',
        );
        _onError?.call(err);
        return;
      }
    }

    if (!isAvailable) {
      _onError?.call(
        const VoiceInputException(
          type: VoiceInputErrorType.unavailable,
          message: 'Konuşma tanıma motoru kullanılamıyor.',
        ),
      );
      return;
    }

    try {
      _isListening = true;
      _onListeningChanged?.call(true);

      await _speechToText.listen(
        onResult: (SpeechRecognitionResult result) {
          if (_isDisposed) return;
          _onResult?.call(
            VoiceRecognitionResult(
              recognizedWords: result.recognizedWords,
              isFinal: result.finalResult,
            ),
          );
        },
        listenOptions: SpeechListenOptions(
          listenMode: ListenMode.dictation,
          partialResults: true,
          cancelOnError: true,
          localeId: _preferredLocaleId,
        ),
      );
    } catch (e) {
      _isListening = false;
      _onListeningChanged?.call(false);
      _onError?.call(
        VoiceInputException(
          type: VoiceInputErrorType.startListeningFailed,
          message: 'Dinleme başlatılırken hata oluştu: $e',
          cause: e,
        ),
      );
    }
  }

  @override
  Future<void> stopListening() async {
    if (!_isListening) return;
    try {
      await _speechToText.stop();
    } catch (_) {}
    _isListening = false;
    _onListeningChanged?.call(false);
  }

  @override
  Future<void> cancelListening() async {
    if (!_isListening) return;
    try {
      await _speechToText.cancel();
    } catch (_) {}
    _isListening = false;
    _onListeningChanged?.call(false);
  }

  void _handleSpeechError(SpeechRecognitionError error) {
    if (_isDisposed) return;
    _isListening = false;
    _onListeningChanged?.call(false);

    final errorType = error.permanent
        ? VoiceInputErrorType.permissionDenied
        : VoiceInputErrorType.runtimeError;

    _onError?.call(
      VoiceInputException(type: errorType, message: error.errorMsg),
    );
  }

  void _handleSpeechStatus(String status) {
    if (_isDisposed) return;
    if (status == 'listening') {
      if (!_isListening) {
        _isListening = true;
        _onListeningChanged?.call(true);
      }
    } else if (status == 'notListening' || status == 'done') {
      if (_isListening) {
        _isListening = false;
        _onListeningChanged?.call(false);
      }
    }
  }

  @override
  void dispose() {
    _isDisposed = true;
    if (_isListening) {
      _isListening = false;
      try {
        _speechToText.stop();
      } catch (_) {}
      _onListeningChanged?.call(false);
    }
    _onResult = null;
    _onError = null;
    _onListeningChanged = null;
  }
}

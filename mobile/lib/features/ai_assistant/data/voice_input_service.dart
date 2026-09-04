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
  network,
  languageUnsupported,
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
  ///
  /// Dinleme oturumu başarıyla başlatıldıysa `true`, başlatılamadıysa `false` döner.
  Future<bool> startListening({
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
  bool _isStarting = false;
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
  Future<bool> initialize({String? userPreferredLocale}) async {
    if (_isDisposed) return false;
    if (_isInitialized && _speechToText.isAvailable) return true;

    try {
      final available = await _speechToText.initialize(
        onError: _handleSpeechError,
        onStatus: _handleSpeechStatus,
      );

      if (available && _speechToText.isAvailable) {
        _isInitialized = true;
        await _resolveTurkishLocale(userPreferredLocale: userPreferredLocale);
        return true;
      } else {
        _isInitialized = false;
        return false;
      }
    } catch (e) {
      _isInitialized = false;
      return false;
    }
  }

  Future<void> _resolveTurkishLocale({String? userPreferredLocale}) async {
    try {
      final locales = await _speechToText.locales();
      _preferredLocaleId = resolveTurkishLocale(
        locales,
        userPreferredLocale: userPreferredLocale,
      );
    } catch (_) {
      _preferredLocaleId = null;
    }
  }

  /// Cihazda bulunan diller arasından Türkçe yerelini güvenle seçer.
  ///
  /// Öncelik:
  /// 1. Kullanıcının voice preference yereli (cihazda destekleniyorsa)
  /// 2. Tam 'tr_TR' / 'tr-TR' eşleşmesi
  /// 3. 'tr_' bölgesel varyantı (ör. tr_CY)
  /// 4. 'tr' ile başlayan herhangi bir yerel kodu
  /// 5. Bulunamazsa cihazın varsayılan dilini kullanmak üzere `null`
  @visibleForTesting
  static String? resolveTurkishLocale(
    List<LocaleName> locales, {
    String? userPreferredLocale,
  }) {
    if (locales.isEmpty) return null;

    // 1. Öncelik: Kullanıcının tercih ettiği desteklenen locale
    if (userPreferredLocale != null && userPreferredLocale.trim().isNotEmpty) {
      final target =
          userPreferredLocale.trim().toLowerCase().replaceAll('-', '_');
      for (final loc in locales) {
        final norm = loc.localeId.toLowerCase().replaceAll('-', '_');
        if (norm == target) {
          return loc.localeId;
        }
      }
    }

    // 2. Öncelik: Tam 'tr_TR' veya 'tr-TR' eşleşmesi
    for (final loc in locales) {
      final normalized = loc.localeId.toLowerCase().replaceAll('-', '_');
      if (normalized == 'tr_tr') {
        return loc.localeId;
      }
    }

    // 3. Öncelik: 'tr_' ile başlayan bölgesel varyantlar (ör. tr_CY)
    for (final loc in locales) {
      final normalized = loc.localeId.toLowerCase().replaceAll('-', '_');
      if (normalized.startsWith('tr_') || normalized.startsWith('tr-')) {
        return loc.localeId;
      }
    }

    // 4. Öncelik: 'tr' ile başlayan herhangi bir locale kodu
    for (final loc in locales) {
      if (loc.localeId.toLowerCase().startsWith('tr')) {
        return loc.localeId;
      }
    }

    return null;
  }

  /// Hata metnini ve kalıcılık bilgisini anlamlı tipli hata türüne dönüştürür.
  @visibleForTesting
  static VoiceInputErrorType mapSpeechError(String errorMsg, bool permanent) {
    final lower = errorMsg.toLowerCase();
    if (lower.contains('permission') ||
        lower.contains('not_allowed') ||
        lower.contains('denied')) {
      return VoiceInputErrorType.permissionDenied;
    }
    if (lower.contains('network') || lower.contains('server')) {
      return VoiceInputErrorType.network;
    }
    if (lower.contains('language') ||
        lower.contains('not_supported') ||
        lower.contains('unsupported')) {
      return VoiceInputErrorType.languageUnsupported;
    }
    if (lower.contains('disabled') ||
        lower.contains('unavailable') ||
        lower.contains('not_available') ||
        lower.contains('no_speech_engine') ||
        lower.contains('client')) {
      return VoiceInputErrorType.unavailable;
    }
    if (lower.contains('no_match') ||
        lower.contains('timeout') ||
        lower.contains('speech_timeout') ||
        lower.contains('busy')) {
      return VoiceInputErrorType.runtimeError;
    }
    return VoiceInputErrorType.runtimeError;
  }

  @override
  Future<bool> startListening({
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
      return false;
    }

    if (_isListening || _isStarting) {
      return false;
    }

    _isStarting = true;
    _onResult = onResult;
    _onError = onError;
    _onListeningChanged = onListeningChanged;

    if (!_isInitialized || !_speechToText.isAvailable) {
      final ok = await initialize();
      if (!ok) {
        _isStarting = false;
        final hasPerm = await _speechToText.hasPermission;
        final VoiceInputErrorType errorType;
        final String errorMsg;
        if (!hasPerm) {
          errorType = VoiceInputErrorType.permissionDenied;
          errorMsg =
              'Mikrofon izni verilmedi. Sesli giriş için mikrofon erişimine izin verin.';
        } else {
          errorType = VoiceInputErrorType.unavailable;
          errorMsg = 'Bu cihazda konuşma tanıma motoru kullanılamıyor.';
        }
        final err = VoiceInputException(type: errorType, message: errorMsg);
        _onError?.call(err);
        _onListeningChanged?.call(false);
        return false;
      }
    }

    if (!isAvailable) {
      _isStarting = false;
      _onError?.call(
        const VoiceInputException(
          type: VoiceInputErrorType.unavailable,
          message: 'Konuşma tanıma motoru kullanılamıyor.',
        ),
      );
      _onListeningChanged?.call(false);
      return false;
    }

    try {
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
      _isListening = true;
      _isStarting = false;
      _onListeningChanged?.call(true);
      return true;
    } catch (e) {
      _isListening = false;
      _isStarting = false;
      _onListeningChanged?.call(false);
      _onError?.call(
        VoiceInputException(
          type: VoiceInputErrorType.startListeningFailed,
          message: 'Dinleme başlatılırken hata oluştu: $e',
          cause: e,
        ),
      );
      return false;
    }
  }

  @override
  Future<void> stopListening() async {
    if (!_isListening && !_isStarting) return;
    try {
      await _speechToText.stop();
    } catch (_) {}
    _isListening = false;
    _isStarting = false;
    _onListeningChanged?.call(false);
  }

  @override
  Future<void> cancelListening() async {
    if (!_isListening && !_isStarting) return;
    try {
      await _speechToText.cancel();
    } catch (_) {}
    _isListening = false;
    _isStarting = false;
    _onListeningChanged?.call(false);
  }

  void _handleSpeechError(SpeechRecognitionError error) {
    if (_isDisposed) return;
    _isListening = false;
    _isStarting = false;
    _onListeningChanged?.call(false);

    final errorType = mapSpeechError(error.errorMsg, error.permanent);
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
    _isStarting = false;
    if (_isListening) {
      _isListening = false;
      try {
        _speechToText.cancel();
      } catch (_) {}
      _onListeningChanged?.call(false);
    }
    _onResult = null;
    _onError = null;
    _onListeningChanged = null;
  }
}

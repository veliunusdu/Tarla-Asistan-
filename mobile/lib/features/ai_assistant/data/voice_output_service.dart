import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';

// ---------------------------------------------------------------------------
// Hata Modeli
// ---------------------------------------------------------------------------

/// Sesli çıktı (TTS) hata türleri.
enum VoiceOutputErrorType {
  unavailable,
  initializeFailed,
  speakFailed,
  stopFailed,
  runtimeError,
}

/// Sesli çıktı işleminde oluşan güvenli ve tipli hata nesnesi.
class VoiceOutputException implements Exception {
  const VoiceOutputException({
    required this.type,
    required this.message,
    this.cause,
  });

  final VoiceOutputErrorType type;
  final String message;
  final Object? cause;

  @override
  String toString() => 'VoiceOutputException($type): $message';
}

// ---------------------------------------------------------------------------
// Sözleşme (Contract / Interface)
// ---------------------------------------------------------------------------

/// Metni sese dönüştüren (Text-to-Speech) servis sözleşmesi.
///
/// DeepSeek, AI repository veya UI widget bağımlılığı içermez.
/// Yalnızca verilen metni seslendirmek ve oynatma durumunu bildirmekten sorumludur.
abstract class VoiceOutputService {
  /// TTS motorunu ilklendirir ve dil/ses ayarlarını hazırlar.
  Future<bool> initialize();

  /// Verilen [text] metnini seslendirir.
  ///
  /// Boş veya yalnızca boşluklardan oluşan metinlerde işlem yapmaz.
  /// Eğer o sırada devam eden bir seslendirme varsa önce onu durdurur,
  /// ardından yeni metni okumaya başlar.
  Future<void> speak(
    String text, {
    void Function(bool isSpeaking)? onSpeakingChanged,
    void Function(VoiceOutputException error)? onError,
  });

  /// Aktif seslendirmeyi durdurur.
  Future<void> stop();

  /// Servisin o an seslendirme yapıp yapmadığı.
  bool get isSpeaking;

  /// Seslendirme durumu değiştiğinde tetiklenen callback.
  void Function(bool isSpeaking)? onSpeakingChanged;

  /// Hata oluştuğunda tetiklenen callback.
  void Function(VoiceOutputException error)? onError;

  /// Servis kaynaklarını temizler ve aktif konuşmayı durdurur.
  void dispose();
}

// ---------------------------------------------------------------------------
// Platform Adaptörü (Test Edilebilirlik)
// ---------------------------------------------------------------------------

/// [FlutterTts] çağrılarını soyutlayan ve testlerde sahtelenmesini sağlayan adaptör.
abstract class FlutterTtsAdapter {
  Future<dynamic> get getLanguages;
  Future<dynamic> isLanguageAvailable(String language);
  Future<dynamic> setLanguage(String language);
  Future<dynamic> setSpeechRate(double rate);
  Future<dynamic> setVolume(double volume);
  Future<dynamic> setPitch(double pitch);
  Future<dynamic> speak(String text);
  Future<dynamic> stop();
  void setStartHandler(VoidCallback callback);
  void setCompletionHandler(VoidCallback callback);
  void setCancelHandler(VoidCallback callback);
  void setErrorHandler(void Function(dynamic message) callback);
}

/// [FlutterTts] paketini doğrudan kullanan varsayılan adaptör.
class StandardFlutterTtsAdapter implements FlutterTtsAdapter {
  StandardFlutterTtsAdapter([FlutterTts? tts]) : _tts = tts ?? FlutterTts();

  final FlutterTts _tts;

  @override
  Future<dynamic> get getLanguages => _tts.getLanguages;

  @override
  Future<dynamic> isLanguageAvailable(String language) =>
      _tts.isLanguageAvailable(language);

  @override
  Future<dynamic> setLanguage(String language) => _tts.setLanguage(language);

  @override
  Future<dynamic> setSpeechRate(double rate) => _tts.setSpeechRate(rate);

  @override
  Future<dynamic> setVolume(double volume) => _tts.setVolume(volume);

  @override
  Future<dynamic> setPitch(double pitch) => _tts.setPitch(pitch);

  @override
  Future<dynamic> speak(String text) => _tts.speak(text);

  @override
  Future<dynamic> stop() => _tts.stop();

  @override
  void setStartHandler(VoidCallback callback) => _tts.setStartHandler(callback);

  @override
  void setCompletionHandler(VoidCallback callback) =>
      _tts.setCompletionHandler(callback);

  @override
  void setCancelHandler(VoidCallback callback) =>
      _tts.setCancelHandler(callback);

  @override
  void setErrorHandler(void Function(dynamic message) callback) =>
      _tts.setErrorHandler(callback);
}

// ---------------------------------------------------------------------------
// Varsayılan İmplementasyon (DefaultVoiceOutputService)
// ---------------------------------------------------------------------------

/// [FlutterTtsAdapter] üzerinden [FlutterTts] kullanan varsayılan [VoiceOutputService].
class DefaultVoiceOutputService implements VoiceOutputService {
  DefaultVoiceOutputService({FlutterTtsAdapter? adapter})
    : _adapter = adapter ?? StandardFlutterTtsAdapter();

  final FlutterTtsAdapter _adapter;

  // Doğal konuşma hız ve ton sabitleri
  static const double _kDefaultSpeechRate = 0.5;
  static const double _kDefaultVolume = 1.0;
  static const double _kDefaultPitch = 1.0;

  bool _isInitialized = false;
  bool _isSpeaking = false;
  bool _isDisposed = false;
  String? _preferredLanguage;

  @override
  bool get isSpeaking => _isSpeaking;

  @visibleForTesting
  String? get preferredLanguage => _preferredLanguage;

  @override
  void Function(bool isSpeaking)? onSpeakingChanged;

  @override
  void Function(VoiceOutputException error)? onError;

  void Function(bool isSpeaking)? _speakCallSpeakingChanged;
  void Function(VoiceOutputException error)? _speakCallError;

  @override
  Future<bool> initialize() async {
    if (_isDisposed) return false;
    if (_isInitialized) return true;

    try {
      _adapter.setStartHandler(_handleStart);
      _adapter.setCompletionHandler(_handleCompletion);
      _adapter.setCancelHandler(_handleCancel);
      _adapter.setErrorHandler(_handleError);

      await _configureAudioAndLanguage();

      _isInitialized = true;
      return true;
    } catch (e) {
      _isInitialized = false;
      _notifyError(
        VoiceOutputErrorType.initializeFailed,
        'TTS motoru başlatılamadı: $e',
        e,
      );
      return false;
    }
  }

  Future<void> _configureAudioAndLanguage() async {
    try {
      final dynamic langsRaw = await _adapter.getLanguages;
      if (langsRaw is List) {
        _preferredLanguage = resolveTurkishLocale(langsRaw);
      }
    } catch (_) {
      _preferredLanguage = null;
    }

    if (_preferredLanguage != null) {
      try {
        await _adapter.setLanguage(_preferredLanguage!);
      } catch (_) {
        // Türkçe ayarlanamazsa cihaz varsayılanında güvenle kalır
      }
    }

    try {
      await _adapter.setSpeechRate(_kDefaultSpeechRate);
      await _adapter.setVolume(_kDefaultVolume);
      await _adapter.setPitch(_kDefaultPitch);
    } catch (_) {}
  }

  /// Cihazda bulunan diller arasından Türkçe TTS dilini güvenle seçer.
  ///
  /// Sırasıyla tam `tr-TR`, ardından `tr_TR` ve son olarak `tr` ile başlayan
  /// herhangi bir kodu arar. Bulunamazsa cihazın varsayılan sesini kullanmak
  /// üzere `null` döner.
  @visibleForTesting
  static String? resolveTurkishLocale(List<dynamic> languages) {
    if (languages.isEmpty) return null;

    final list = languages.map((e) => e.toString()).toList();

    // 1. Öncelik: tr-TR
    for (final lang in list) {
      if (lang.toLowerCase() == 'tr-tr') return lang;
    }

    // 2. Öncelik: tr_TR
    for (final lang in list) {
      if (lang.toLowerCase() == 'tr_tr') return lang;
    }

    // 3. Öncelik: 'tr' ile başlayan herhangi bir varyant
    for (final lang in list) {
      final lower = lang.toLowerCase();
      if (lower.startsWith('tr-') || lower.startsWith('tr_') || lower == 'tr') {
        return lang;
      }
    }

    return null;
  }

  @override
  Future<void> speak(
    String text, {
    void Function(bool isSpeaking)? onSpeakingChanged,
    void Function(VoiceOutputException error)? onError,
  }) async {
    if (_isDisposed) {
      _notifyError(
        VoiceOutputErrorType.runtimeError,
        'VoiceOutputService disposed edilmiş durumda.',
      );
      return;
    }

    _speakCallSpeakingChanged = onSpeakingChanged;
    _speakCallError = onError;

    final normalized = text.trim();
    if (normalized.isEmpty) {
      // Boş metinde platform methodu çağrılmaz, sessizce tamamlanır.
      return;
    }

    if (!_isInitialized) {
      final ok = await initialize();
      if (!ok) {
        _notifyError(
          VoiceOutputErrorType.initializeFailed,
          'Seslendirme motoru kullanılamıyor.',
        );
        return;
      }
    }

    // Eğer o sırada başka bir konuşma varsa önce durdur
    if (_isSpeaking) {
      await stop();
    }

    try {
      _setSpeaking(true);
      await _adapter.speak(normalized);
    } catch (e) {
      _setSpeaking(false);
      _notifyError(
        VoiceOutputErrorType.speakFailed,
        'Seslendirme başlatılırken hata oluştu: $e',
        e,
      );
    }
  }

  @override
  Future<void> stop() async {
    if (_isDisposed) return;
    try {
      await _adapter.stop();
    } catch (e) {
      _notifyError(
        VoiceOutputErrorType.stopFailed,
        'Seslendirme durdurulurken hata oluştu: $e',
        e,
      );
    } finally {
      _setSpeaking(false);
    }
  }

  void _handleStart() {
    if (_isDisposed) return;
    _setSpeaking(true);
  }

  void _handleCompletion() {
    if (_isDisposed) return;
    _setSpeaking(false);
  }

  void _handleCancel() {
    if (_isDisposed) return;
    _setSpeaking(false);
  }

  void _handleError(dynamic errorMsg) {
    if (_isDisposed) return;
    _setSpeaking(false);
    _notifyError(
      VoiceOutputErrorType.runtimeError,
      'TTS çalışma zamanı hatası: $errorMsg',
      errorMsg,
    );
  }

  void _setSpeaking(bool speaking) {
    if (_isDisposed) return;
    if (_isSpeaking == speaking) return;
    _isSpeaking = speaking;
    _speakCallSpeakingChanged?.call(speaking);
    onSpeakingChanged?.call(speaking);
  }

  void _notifyError(
    VoiceOutputErrorType type,
    String message, [
    Object? cause,
  ]) {
    if (_isDisposed) return;
    final exception = VoiceOutputException(
      type: type,
      message: message,
      cause: cause,
    );
    _speakCallError?.call(exception);
    onError?.call(exception);
  }

  @override
  void dispose() {
    if (_isDisposed) return;
    _isDisposed = true;

    if (_isSpeaking) {
      _isSpeaking = false;
      try {
        _adapter.stop();
      } catch (_) {}
    }

    _speakCallSpeakingChanged = null;
    _speakCallError = null;
    onSpeakingChanged = null;
    onError = null;
  }
}

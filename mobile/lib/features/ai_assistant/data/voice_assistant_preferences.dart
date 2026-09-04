import 'package:shared_preferences/shared_preferences.dart';

/// Sesli asistan kullanıcı tercihlerini yöneten sözleşme.
abstract class VoiceAssistantPreferences {
  /// AI yanıtlarının otomatik seslendirilip seslendirilmeyeceğini döner.
  /// Varsayılan değer mutlaka `false` olmalıdır.
  Future<bool> getVoiceResponsesEnabled();

  /// AI yanıtlarının otomatik seslendirme tercihini kaydeder.
  Future<void> setVoiceResponsesEnabled(bool enabled);
}

/// [SharedPreferences] kullanan varsayılan tercih implementasyonu.
class SharedPreferencesVoiceAssistantPreferences
    implements VoiceAssistantPreferences {
  const SharedPreferencesVoiceAssistantPreferences();

  static const String keyVoiceResponsesEnabled = 'ai_voice_responses_enabled';

  @override
  Future<bool> getVoiceResponsesEnabled() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      return prefs.getBool(keyVoiceResponsesEnabled) ?? false;
    } catch (_) {
      return false;
    }
  }

  @override
  Future<void> setVoiceResponsesEnabled(bool enabled) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(keyVoiceResponsesEnabled, enabled);
    } catch (_) {}
  }
}

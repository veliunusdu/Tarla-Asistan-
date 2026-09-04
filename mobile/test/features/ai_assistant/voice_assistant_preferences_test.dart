import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/ai_assistant/data/voice_assistant_preferences.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  group('SharedPreferencesVoiceAssistantPreferences', () {
    setUp(() {
      SharedPreferences.setMockInitialValues({});
    });

    test('varsayılan olarak false döner (kayıt yokken)', () async {
      const prefsService = SharedPreferencesVoiceAssistantPreferences();
      final enabled = await prefsService.getVoiceResponsesEnabled();
      expect(enabled, isFalse);
    });

    test('true kaydedildiğinde ve tekrar okunduğunda true döner', () async {
      const prefsService = SharedPreferencesVoiceAssistantPreferences();
      await prefsService.setVoiceResponsesEnabled(true);
      final enabled = await prefsService.getVoiceResponsesEnabled();
      expect(enabled, isTrue);
    });

    test('false kaydedildiğinde ve tekrar okunduğunda false döner', () async {
      SharedPreferences.setMockInitialValues({
        SharedPreferencesVoiceAssistantPreferences.keyVoiceResponsesEnabled:
            true,
      });

      const prefsService = SharedPreferencesVoiceAssistantPreferences();
      expect(await prefsService.getVoiceResponsesEnabled(), isTrue);

      await prefsService.setVoiceResponsesEnabled(false);
      expect(await prefsService.getVoiceResponsesEnabled(), isFalse);
    });
  });
}

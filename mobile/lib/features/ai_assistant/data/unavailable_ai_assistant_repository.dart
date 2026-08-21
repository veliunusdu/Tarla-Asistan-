import 'dart:typed_data';

import '../domain/ai_chat_message.dart';
import 'ai_assistant_repository.dart';

/// API dokümanında AI sohbet endpoint'i tanımlanmadığı için
/// production'da bu implementasyon kullanılır.
///
/// Backend AI endpoint'i eklendikten sonra bu sınıfın yerine
/// gerçek implementasyon geçirilmelidir.
class UnavailableAiAssistantRepository implements AiAssistantRepository {
  const UnavailableAiAssistantRepository();

  @override
  Future<String> sendMessage({
    required String message,
    Uint8List? photo,
    List<AiChatMessage> history = const [],
  }) async {
    throw Exception('AI Asistan bağlantısı henüz yapılandırılmadı.');
  }
}

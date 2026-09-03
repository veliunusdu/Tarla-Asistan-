import 'dart:typed_data';

import '../domain/ai_chat_message.dart';
import '../domain/ai_chat_response.dart';
import 'ai_assistant_repository.dart';

/// API dokümanında AI sohbet endpoint'i tanımlanmadığı için
/// fallback olarak bu implementasyon kullanılır.
class UnavailableAiAssistantRepository implements AiAssistantRepository {
  const UnavailableAiAssistantRepository();

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
    throw Exception('AI Asistan bağlantısı henüz yapılandırılmadı.');
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
    throw Exception('AI Asistan bağlantısı henüz yapılandırılmadı.');
  }
}

import 'dart:typed_data';

import '../../../services/api_client.dart';
import '../domain/ai_chat_message.dart';
import 'ai_assistant_repository.dart';

class BackendAiAssistantRepository implements AiAssistantRepository {
  const BackendAiAssistantRepository({required ApiClient apiClient})
    : _client = apiClient;

  final ApiClient _client;

  @override
  Future<String> sendMessage({
    required String message,
    Uint8List? photo,
    List<AiChatMessage> history = const [],
  }) async {
    if (photo != null) {
      throw const ApiException(
        'Fotoğraflı AI soruları yakında etkinleştirilecek.',
      );
    }
    final response = await _client.postJson('/ai/chat', {
      'message': message,
      'history': history
          .map(
            (item) => {
              'role': item.isUser ? 'user' : 'assistant',
              'content': item.text,
            },
          )
          .toList(),
    });
    final reply = response['reply'];
    if (reply is! String || reply.trim().isEmpty) {
      throw const ApiException('AI yanıtı boş geldi.');
    }
    return reply;
  }
}

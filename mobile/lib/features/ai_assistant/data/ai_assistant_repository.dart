import 'dart:typed_data';

import '../domain/ai_chat_message.dart';
import '../domain/ai_chat_response.dart';

/// AI sohbet repository arayüzü.
///
/// Backend sözleşmesi:
///   POST /api/v1/ai/chat
///   Content-Type: multipart/form-data (fotoğraf varsa) / application/json (yalnızca metin)
///   Alanlar:
///     - message        : string  — zorunlu kullanıcı metni
///     - photo          : file    — isteğe bağlı JPEG/PNG, maks 5 MB
///     - field_id       : string  — isteğe bağlı tarla kimliği
///     - conversation_id: string  — isteğe bağlı konuşma kimliği
///     - history        : array   — önceki mesajlar [{role, content}]
///   Response 200:
///     { "reply": "...", "conversation_id": "..." }
abstract class AiAssistantRepository {
  Future<AiChatResponse> sendMessage({
    required String message,
    Uint8List? photo,
    String? photoContentType,
    String? photoFileName,
    String? fieldId,
    String? conversationId,
    List<AiChatMessage> history = const [],
  });
}

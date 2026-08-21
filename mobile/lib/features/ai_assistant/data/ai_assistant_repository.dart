import 'dart:typed_data';

import '../domain/ai_chat_message.dart';

/// AI sohbet repository arayüzü.
///
/// Beklenen backend sözleşmesi (henüz endpoint mevcut değil):
///   POST /ai/chat  (veya /assistant/chat)
///   Content-Type: multipart/form-data (fotoğraf varsa) / application/json
///   Body:
///     - message        : string  — kullanıcı metni
///     - photo          : file    — isteğe bağlı JPEG/PNG, maks 5 MB
///     - field_id       : string  — isteğe bağlı tarla kimliği
///     - conversation_id: string  — isteğe bağlı konuşma kimliği
///     - history        : array   — önceki mesajlar [{role, content}]
///   Response 200:
///     { "reply": "...", "conversation_id": "..." }
///   Response 4xx/5xx:
///     { "detail": "..." }
abstract class AiAssistantRepository {
  Future<String> sendMessage({
    required String message,
    Uint8List? photo,
    List<AiChatMessage> history,
  });
}

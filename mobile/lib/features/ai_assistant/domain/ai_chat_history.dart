import 'ai_chat_message.dart';

const int maxAiChatMessages = 20;

List<AiChatMessage> limitAiChatHistory(List<AiChatMessage> messages) {
  if (messages.length <= maxAiChatMessages) {
    return List.unmodifiable(messages);
  }

  return List.unmodifiable(
    messages.sublist(messages.length - maxAiChatMessages),
  );
}

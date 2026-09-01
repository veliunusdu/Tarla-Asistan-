class AiChatResponse {
  const AiChatResponse({
    required this.reply,
    required this.conversationId,
  });

  final String reply;
  final String conversationId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AiChatResponse &&
          runtimeType == other.runtimeType &&
          reply == other.reply &&
          conversationId == other.conversationId;

  @override
  int get hashCode => reply.hashCode ^ conversationId.hashCode;

  @override
  String toString() =>
      'AiChatResponse(reply: $reply, conversationId: $conversationId)';
}

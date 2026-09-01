import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_history.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';

void main() {
  test('sohbet geçmişi en yeni 20 mesajı tutar', () {
    final messages = List.generate(
      25,
      (index) => AiChatMessage(
        text: '$index',
        isUser: index.isEven,
        timestamp: DateTime(2026, 1, 1, 0, index),
        photo: index.isEven ? Uint8List.fromList([index]) : null,
      ),
    );

    final limited = limitAiChatHistory(messages);

    expect(limited, hasLength(20));
    expect(limited.first.text, '5');
    expect(limited.last.text, '24');
  });
}

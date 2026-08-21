import 'dart:typed_data';

/// Tek bir sohbet mesajını temsil eder.
class AiChatMessage {
  const AiChatMessage({
    required this.text,
    required this.isUser,
    required this.timestamp,
    this.photo,
  });

  /// Mesaj metni. Fotoğraf varken boş olabilir.
  final String text;

  /// true → kullanıcı mesajı, false → asistan mesajı.
  final bool isUser;

  /// Yalnızca kullanıcı mesajlarında geçerli.
  final Uint8List? photo;

  final DateTime timestamp;
}

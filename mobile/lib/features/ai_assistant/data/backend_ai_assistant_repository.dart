import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import '../../../services/api_client.dart';
import '../domain/ai_chat_message.dart';
import '../domain/ai_chat_response.dart';
import 'ai_assistant_repository.dart';

class BackendAiAssistantRepository implements AiAssistantRepository {
  const BackendAiAssistantRepository({required ApiClient apiClient})
      : _client = apiClient;

  final ApiClient _client;

  static const int _maxPhotoBytes = 5 * 1024 * 1024; // 5 MB

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
    final trimmedMessage = message.trim();
    if (trimmedMessage.isEmpty) {
      throw const ApiException('Lütfen bir soru veya açıklama yazın.');
    }

    final trimmedFieldId = fieldId?.trim();
    final trimmedConversationId = conversationId?.trim();

    if (photo == null) {
      final payload = <String, dynamic>{
        'message': trimmedMessage,
        if (trimmedFieldId != null && trimmedFieldId.isNotEmpty)
          'field_id': trimmedFieldId,
        if (trimmedConversationId != null && trimmedConversationId.isNotEmpty)
          'conversation_id': trimmedConversationId,
        if (history.isNotEmpty)
          'history': history
              .map(
                (item) => {
                  'role': item.isUser ? 'user' : 'assistant',
                  'content': item.text,
                },
              )
              .toList(),
      };

      final response = await _client.postJson('/ai/chat', payload);
      return _parseResponse(response);
    }

    // Photo validation & multipart request
    if (photo.lengthInBytes > _maxPhotoBytes) {
      throw const ApiException('Fotoğraf en fazla 5 MB olabilir.');
    }

    final resolvedContentType = _resolveContentType(
      photo: photo,
      declaredType: photoContentType,
      fileName: photoFileName,
    );

    final fields = <String, String>{
      'message': trimmedMessage,
      if (trimmedFieldId != null && trimmedFieldId.isNotEmpty)
        'field_id': trimmedFieldId,
      if (trimmedConversationId != null && trimmedConversationId.isNotEmpty)
        'conversation_id': trimmedConversationId,
      if (history.isNotEmpty)
        'history': jsonEncode(
          history
              .map(
                (item) => {
                  'role': item.isUser ? 'user' : 'assistant',
                  'content': item.text,
                },
              )
              .toList(),
        ),
    };

    final resolvedFileName = photoFileName?.trim().isNotEmpty == true
        ? photoFileName!.trim()
        : (resolvedContentType == 'image/png' ? 'photo.png' : 'photo.jpg');

    final uploadFile = ApiMultipartFile(
      field: 'photo',
      bytes: photo,
      filename: resolvedFileName,
      contentType: resolvedContentType,
    );

    final response = await _client.postMultipart(
      '/ai/chat',
      fields: fields,
      files: [uploadFile],
    );

    return _parseResponse(response);
  }

  static String _resolveContentType({
    required Uint8List photo,
    String? declaredType,
    String? fileName,
  }) {
    // 1. Check for HEIC signature
    if (_isHeic(photo, fileName)) {
      throw const ApiException(
        'Yalnızca JPEG ve PNG fotoğrafları destekleniyor. HEIC formatı desteklenmiyor.',
      );
    }

    // 2. Sniff magic bytes
    if (_isJpeg(photo)) return 'image/jpeg';
    if (_isPng(photo)) return 'image/png';

    // 3. Fallback to declared type if valid
    final normalizedDeclared = declaredType?.trim().toLowerCase();
    if (normalizedDeclared == 'image/jpeg' || normalizedDeclared == 'image/jpg') {
      return 'image/jpeg';
    }
    if (normalizedDeclared == 'image/png') {
      return 'image/png';
    }

    // 4. Fallback to filename extension if valid
    final normalizedName = fileName?.trim().toLowerCase() ?? '';
    if (normalizedName.endsWith('.jpg') || normalizedName.endsWith('.jpeg')) {
      return 'image/jpeg';
    }
    if (normalizedName.endsWith('.png')) {
      return 'image/png';
    }

    throw const ApiException(
      'Yalnızca JPEG ve PNG fotoğrafları destekleniyor.',
    );
  }

  static bool _isJpeg(Uint8List bytes) {
    if (bytes.length < 3) return false;
    return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
  }

  static bool _isPng(Uint8List bytes) {
    if (bytes.length < 4) return false;
    return bytes[0] == 0x89 &&
        bytes[1] == 0x50 &&
        bytes[2] == 0x4E &&
        bytes[3] == 0x47;
  }

  static bool _isHeic(Uint8List bytes, String? fileName) {
    final name = fileName?.toLowerCase() ?? '';
    if (name.endsWith('.heic') || name.endsWith('.heif')) return true;
    if (bytes.length >= 12) {
      // Check for ISO Base Media File Format containing 'ftyp' followed by 'heic', 'mif1', 'heix', 'msf1'
      final header = String.fromCharCodes(bytes.sublist(4, 12));
      if (header.startsWith('ftyp')) {
        final brand = header.substring(4).toLowerCase();
        if (brand.contains('heic') ||
            brand.contains('mif1') ||
            brand.contains('heix') ||
            brand.contains('msf1')) {
          return true;
        }
      }
    }
    return false;
  }

  static AiChatResponse _parseResponse(Map<String, dynamic> json) {
    final reply = json['reply'];
    if (reply is! String || reply.trim().isEmpty) {
      throw const ApiException('AI yanıtı boş geldi.');
    }
    final conversationId = json['conversation_id']?.toString() ?? '';
    return AiChatResponse(
      reply: reply.trim(),
      conversationId: conversationId,
    );
  }
}

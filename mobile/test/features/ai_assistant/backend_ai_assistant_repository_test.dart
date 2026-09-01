import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/ai_assistant/data/backend_ai_assistant_repository.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_message.dart';
import 'package:mobile/features/ai_assistant/domain/ai_chat_response.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  // Valid JPEG header: FF D8 FF E0
  final validJpegBytes = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46]);
  // Valid PNG header: 89 50 4E 47 0D 0A 1A 0A
  final validPngBytes = Uint8List.fromList([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
  // HEIC header: ....ftypheic
  final heicBytes = Uint8List.fromList([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63]);

  group('BackendAiAssistantRepository', () {
    group('metin sohbeti (text-only)', () {
      test('yalnızca metin gönderildiğinde JSON request gönderir ve cevabı döner', () async {
        http.Request? capturedRequest;

        final client = ApiClient(
          httpClient: MockClient((request) async {
            capturedRequest = request;
            return http.Response(
              jsonEncode({
                'reply': 'Tarlanız için sulama önerilir.',
                'conversation_id': 'conv-101',
              }),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        final response = await repo.sendMessage(
          message: 'Bugün sulama yapmalı mıyım?',
          fieldId: 'field-1',
          conversationId: 'conv-100',
          history: [
            AiChatMessage(
              text: 'Merhaba',
              isUser: true,
              timestamp: DateTime.now(),
            ),
            AiChatMessage(
              text: 'Merhaba! Nasıl yardımcı olabilirim?',
              isUser: false,
              timestamp: DateTime.now(),
            ),
          ],
        );

        expect(response, const AiChatResponse(
          reply: 'Tarlanız için sulama önerilir.',
          conversationId: 'conv-101',
        ));

        expect(capturedRequest, isNotNull);
        expect(capturedRequest!.headers['content-type'], contains('application/json'));
        final body = jsonDecode(capturedRequest!.body) as Map<String, dynamic>;
        expect(body['message'], 'Bugün sulama yapmalı mıyım?');
        expect(body['field_id'], 'field-1');
        expect(body['conversation_id'], 'conv-100');
        expect(body['history'], hasLength(2));
        expect(body['history'][0]['role'], 'user');
        expect(body['history'][0]['content'], 'Merhaba');
        expect(body['history'][1]['role'], 'assistant');
        expect(body['history'][1]['content'], 'Merhaba! Nasıl yardımcı olabilirim?');
      });

      test('boş mesaj girildiğinde istek göndermez ve hata fırlatır', () async {
        var requestSent = false;
        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestSent = true;
            return http.Response('{}', 200);
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        expect(
          () => repo.sendMessage(message: '   '),
          throwsA(isA<ApiException>().having((e) => e.message, 'message', contains('Lütfen bir soru veya açıklama yazın'))),
        );
        expect(requestSent, isFalse);
      });
    });

    group('fotoğraflı analiz (multipart)', () {
      test('JPEG fotoğrafı multipart/form-data olarak doğru alanlarla gönderir', () async {
        http.BaseRequest? capturedRequest;
        List<int>? capturedBytes;

        final client = ApiClient(
          httpClient: MockClient.streaming((request, bodyStream) async {
            capturedRequest = request;
            if (request is http.MultipartRequest) {
              capturedBytes = await request.files.first.finalize().toBytes();
            }
            final responseBody = utf8.encode(
              jsonEncode({
                'reply': 'Fotoğrafta mildiyö hastalığı belirtileri tespit edildi.',
                'conversation_id': 'conv-photo-1',
              }),
            );
            return http.StreamedResponse(
              Stream.value(responseBody),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        final response = await repo.sendMessage(
          message: 'Bu yapraktaki lekeler nedir?',
          photo: validJpegBytes,
          photoFileName: 'yaprak.jpg',
          photoContentType: 'image/jpeg',
          fieldId: 'field-42',
          conversationId: 'conv-init',
          history: [
            AiChatMessage(
              text: 'Önceki mesaj',
              isUser: true,
              timestamp: DateTime.now(),
            ),
          ],
        );

        expect(response, const AiChatResponse(
          reply: 'Fotoğrafta mildiyö hastalığı belirtileri tespit edildi.',
          conversationId: 'conv-photo-1',
        ));

        expect(capturedRequest, isA<http.MultipartRequest>());
        final mp = capturedRequest as http.MultipartRequest;
        expect(mp.fields['message'], 'Bu yapraktaki lekeler nedir?');
        expect(mp.fields['field_id'], 'field-42');
        expect(mp.fields['conversation_id'], 'conv-init');
        expect(mp.fields['history'], isNotNull);
        final historyList = jsonDecode(mp.fields['history']!) as List;
        expect(historyList, hasLength(1));
        expect(historyList[0]['role'], 'user');
        expect(historyList[0]['content'], 'Önceki mesaj');

        expect(mp.files, hasLength(1));
        expect(mp.files.first.field, 'photo');
        expect(mp.files.first.filename, 'yaprak.jpg');
        expect(mp.files.first.contentType.mimeType, 'image/jpeg');
        expect(capturedBytes, validJpegBytes);
      });

      test('PNG fotoğrafı da multipart olarak başarıyla gönderir', () async {
        http.BaseRequest? capturedRequest;

        final client = ApiClient(
          httpClient: MockClient.streaming((request, bodyStream) async {
            capturedRequest = request;
            final responseBody = utf8.encode(
              jsonEncode({
                'reply': 'Bitki sağlıklı görünüyor.',
                'conversation_id': 'conv-png',
              }),
            );
            return http.StreamedResponse(
              Stream.value(responseBody),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        final response = await repo.sendMessage(
          message: 'Durum nedir?',
          photo: validPngBytes,
        );

        expect(response.reply, 'Bitki sağlıklı görünüyor.');
        expect(capturedRequest, isA<http.MultipartRequest>());
        final mp = capturedRequest as http.MultipartRequest;
        expect(mp.files.first.contentType.mimeType, 'image/png');
      });

      test('5 MB üzerindeki fotoğrafı göndermez ve kullanıcıya açık hata fırlatır', () async {
        var requestSent = false;
        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestSent = true;
            return http.Response('{}', 200);
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);
        final hugePhoto = Uint8List((5 * 1024 * 1024) + 1);
        // Put valid JPEG header
        hugePhoto[0] = 0xFF;
        hugePhoto[1] = 0xD8;
        hugePhoto[2] = 0xFF;

        expect(
          () => repo.sendMessage(message: 'İncele', photo: hugePhoto),
          throwsA(isA<ApiException>().having((e) => e.message, 'message', 'Fotoğraf en fazla 5 MB olabilir.')),
        );
        expect(requestSent, isFalse);
      });

      test('HEIC formatındaki fotoğrafı açık hata ile reddeder', () async {
        var requestSent = false;
        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestSent = true;
            return http.Response('{}', 200);
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        expect(
          () => repo.sendMessage(message: 'İncele', photo: heicBytes, photoFileName: 'test.heic'),
          throwsA(isA<ApiException>().having((e) => e.message, 'message', contains('HEIC formatı desteklenmiyor'))),
        );
        expect(requestSent, isFalse);
      });

      test('Desteklenmeyen rastgele dosya formatını reddeder', () async {
        var requestSent = false;
        final client = ApiClient(
          httpClient: MockClient((request) async {
            requestSent = true;
            return http.Response('{}', 200);
          }),
          idTokenProvider: () async => 'dummy-token',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);
        final randomBytes = Uint8List.fromList([0x01, 0x02, 0x03, 0x04]);

        expect(
          () => repo.sendMessage(message: 'İncele', photo: randomBytes, photoFileName: 'file.txt'),
          throwsA(isA<ApiException>().having((e) => e.message, 'message', contains('Yalnızca JPEG ve PNG'))),
        );
        expect(requestSent, isFalse);
      });

      test('401 durumunda token refresh sonrası fotoğraf baytlarını eksiksiz tekrar gönderir', () async {
        var callCount = 0;
        List<int>? firstPhotoBytes;
        List<int>? secondPhotoBytes;
        String? firstAuth;
        String? secondAuth;

        final client = ApiClient(
          httpClient: MockClient.streaming((request, bodyStream) async {
            callCount++;
            if (callCount == 1) {
              firstAuth = request.headers['Authorization'];
              if (request is http.MultipartRequest) {
                firstPhotoBytes = await request.files.first.finalize().toBytes();
              }
              return http.StreamedResponse(
                Stream.value(utf8.encode(jsonEncode({'detail': 'expired'}))),
                401,
                headers: {'content-type': 'application/json'},
              );
            }
            secondAuth = request.headers['Authorization'];
            if (request is http.MultipartRequest) {
              secondPhotoBytes = await request.files.first.finalize().toBytes();
            }
            return http.StreamedResponse(
              Stream.value(utf8.encode(jsonEncode({
                'reply': 'Yeniden deneme başarılı.',
                'conversation_id': 'conv-retry',
              }))),
              200,
              headers: {'content-type': 'application/json'},
            );
          }),
          idTokenProvider: () async => 'token-1',
          forceRefreshTokenProvider: () async => 'token-2',
        );

        final repo = BackendAiAssistantRepository(apiClient: client);

        final response = await repo.sendMessage(
          message: 'Bu bitki ne?',
          photo: validJpegBytes,
          photoFileName: 'bitki.jpg',
        );

        expect(response.reply, 'Yeniden deneme başarılı.');
        expect(response.conversationId, 'conv-retry');
        expect(callCount, 2);
        expect(firstAuth, 'Bearer token-1');
        expect(secondAuth, 'Bearer token-2');
        expect(firstPhotoBytes, validJpegBytes);
        expect(secondPhotoBytes, validJpegBytes);
      });
    });
  });
}

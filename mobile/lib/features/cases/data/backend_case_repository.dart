import 'package:uuid/uuid.dart';

import '../../../services/api_client.dart';
import '../domain/models/case_category.dart';
import '../domain/models/case_detail.dart';
import '../domain/models/case_message.dart';
import '../domain/models/case_status.dart';
import '../domain/models/case_summary.dart';
import '../domain/models/create_case_input.dart';
import 'case_repository.dart';

class BackendCaseRepository implements CaseRepository {
  const BackendCaseRepository({
    required ApiClient apiClient,
    Uuid uuid = const Uuid(),
  })  : _api = apiClient,
        _uuid = uuid;

  final ApiClient _api;
  final Uuid _uuid;

  @override
  Future<String> createCase(CreateCaseInput input) async {
    List<String>? mediaIds;

    if (input.imageBytes != null && input.imageBytes!.isNotEmpty) {
      final fileName = input.imageFileName ?? 'case_image.jpg';
      final mediaResponse = await _api.postMultipart(
        '/media',
        files: [
          ApiMultipartFile(
            field: 'file',
            bytes: input.imageBytes!,
            filename: fileName,
            contentType: 'image/jpeg',
          ),
        ],
      );
      final mediaId = mediaResponse['id']?.toString();
      if (mediaId == null || mediaId.isEmpty) {
        throw const ApiException('Fotoğraf yüklendi ancak medya kimliği alınamadı.');
      }
      mediaIds = [mediaId];
    }

    final payload = <String, dynamic>{
      'farm_id': input.farmId,
      'category': input.category.backendValue,
      'title': input.title.trim(),
      'description': input.description.trim(),
      'media_ids': mediaIds,
      'client_operation_id': _uuid.v4(),
    };

    final response = await _api.postJson('/cases', payload);
    final caseId = response['id']?.toString();
    if (caseId == null || caseId.isEmpty) {
      throw const ApiException('Vaka oluşturuldu ancak yanıt kimliği alınamadı.');
    }
    return caseId;
  }

  @override
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status}) async {
    final query = <String, String>{};
    if (farmId != null && farmId.isNotEmpty) query['farmId'] = farmId;
    if (status != null) query['status'] = status.backendValue;

    final response = await _api.getJson('/cases', queryParameters: query.isNotEmpty ? query : null);
    final items = response['items'] as List<dynamic>? ?? [];

    return items.map((raw) {
      final m = raw as Map<String, dynamic>;
      final catStr = m['category']?.toString().toLowerCase();
      final cat = CaseCategory.values.firstWhere(
        (c) => c.name.toLowerCase() == catStr || c.backendValue.toLowerCase() == catStr,
        orElse: () => CaseCategory.other,
      );

      return CaseSummary(
        id: m['id']?.toString() ?? '',
        farmId: m['farm_id']?.toString() ?? '',
        farmName: m['farm_name']?.toString() ?? '',
        category: cat,
        status: CaseStatus.fromString(m['status']?.toString()),
        title: m['title']?.toString() ?? '',
        createdAt: DateTime.tryParse(m['created_at_utc']?.toString() ?? '') ?? DateTime.now(),
        updatedAt: DateTime.tryParse(m['updated_at_utc']?.toString() ?? '') ?? DateTime.now(),
        messageCount: (m['message_count'] as num?)?.toInt() ?? 0,
        mediaCount: (m['media_count'] as num?)?.toInt() ?? 0,
      );
    }).toList();
  }

  @override
  Future<CaseDetail> getCaseById(String caseId) async {
    final m = await _api.getJson('/cases/$caseId');
    final catStr = m['category']?.toString().toLowerCase();
    final cat = CaseCategory.values.firstWhere(
      (c) => c.name.toLowerCase() == catStr || c.backendValue.toLowerCase() == catStr,
      orElse: () => CaseCategory.other,
    );

    final mediaList = (m['media'] as List<dynamic>?)
            ?.map((e) => (e as Map<String, dynamic>)['url']?.toString())
            .whereType<String>()
            .toList() ??
        [];

    final messagesList = (m['messages'] as List<dynamic>?)?.map((rawMsg) {
          final msgMap = rawMsg as Map<String, dynamic>;
          final msgMedia = (msgMap['media'] as List<dynamic>?)
                  ?.map((e) => (e as Map<String, dynamic>)['url']?.toString())
                  .whereType<String>()
                  .toList() ??
              [];

          final msgType = CaseMessageType.fromString(msgMap['message_type']?.toString());
          final isFromExpert = msgType == CaseMessageType.expertResponse ||
              msgType == CaseMessageType.additionalInfoRequest;

          return CaseMessage(
            id: msgMap['id']?.toString() ?? '',
            caseId: msgMap['case_id']?.toString() ?? caseId,
            senderId: msgMap['sender_id']?.toString() ?? '',
            senderName: msgMap['sender_name']?.toString() ?? '',
            messageType: msgType,
            body: msgMap['body']?.toString() ?? '',
            mediaUrls: msgMedia,
            createdAt: DateTime.tryParse(msgMap['created_at_utc']?.toString() ?? '') ?? DateTime.now(),
            isCurrentUser: !isFromExpert,
          );
        }).toList() ??
        [];

    return CaseDetail(
      id: m['id']?.toString() ?? caseId,
      farmId: m['farm_id']?.toString() ?? '',
      farmName: m['farm_name']?.toString() ?? '',
      category: cat,
      status: CaseStatus.fromString(m['status']?.toString()),
      title: m['title']?.toString() ?? '',
      description: m['description']?.toString() ?? '',
      initialMediaUrls: mediaList,
      messages: messagesList,
      createdAt: DateTime.tryParse(m['created_at_utc']?.toString() ?? '') ?? DateTime.now(),
    );
  }

  @override
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  }) async {
    List<String>? mediaIds;

    if (imageBytes != null && imageBytes.isNotEmpty) {
      final fileName = imageFileName ?? 'message_image.jpg';
      final mediaResponse = await _api.postMultipart(
        '/media',
        files: [
          ApiMultipartFile(
            field: 'file',
            bytes: imageBytes,
            filename: fileName,
            contentType: 'image/jpeg',
          ),
        ],
      );
      final mediaId = mediaResponse['id']?.toString();
      if (mediaId == null || mediaId.isEmpty) {
        throw const ApiException('Fotoğraf yüklendi ancak medya kimliği alınamadı.');
      }
      mediaIds = [mediaId];
    }

    final payload = <String, dynamic>{
      'body': body.trim(),
      'message_type': 'Comment',
      'media_ids': mediaIds,
      'client_operation_id': _uuid.v4(),
    };

    final response = await _api.postJson('/cases/$caseId/messages', payload);

    final mediaUrls = (response['media'] as List<dynamic>?)
            ?.map((e) => (e as Map<String, dynamic>)['url']?.toString())
            .whereType<String>()
            .toList() ??
        [];

    final msgType = CaseMessageType.fromString(response['message_type']?.toString());
    final isFromExpert = msgType == CaseMessageType.expertResponse ||
        msgType == CaseMessageType.additionalInfoRequest;

    return CaseMessage(
      id: response['id']?.toString() ?? '',
      caseId: response['case_id']?.toString() ?? caseId,
      senderId: response['sender_id']?.toString() ?? '',
      senderName: response['sender_name']?.toString() ?? '',
      messageType: msgType,
      body: response['body']?.toString() ?? '',
      mediaUrls: mediaUrls,
      createdAt: DateTime.tryParse(response['created_at_utc']?.toString() ?? '') ?? DateTime.now(),
      isCurrentUser: !isFromExpert,
    );
  }
}

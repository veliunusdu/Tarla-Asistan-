import 'package:uuid/uuid.dart';

import '../../../services/api_client.dart';
import '../domain/models/case_category.dart';
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
      if (mediaId != null && mediaId.isNotEmpty) {
        mediaIds = [mediaId];
      }
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
}

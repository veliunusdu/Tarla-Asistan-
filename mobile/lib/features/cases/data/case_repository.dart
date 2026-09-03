import '../domain/models/case_detail.dart';
import '../domain/models/case_message.dart';
import '../domain/models/case_status.dart';
import '../domain/models/case_summary.dart';
import '../domain/models/create_case_input.dart';

abstract interface class CaseRepository {
  Future<String> createCase(CreateCaseInput input);
  Future<List<CaseSummary>> getCases({String? farmId, CaseStatus? status});
  Future<CaseDetail> getCaseById(String caseId);
  Future<CaseMessage> sendMessage(
    String caseId, {
    required String body,
    List<int>? imageBytes,
    String? imageFileName,
  });
}

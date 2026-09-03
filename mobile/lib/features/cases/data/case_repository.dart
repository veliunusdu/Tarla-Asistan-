import '../domain/models/create_case_input.dart';

abstract interface class CaseRepository {
  Future<String> createCase(CreateCaseInput input);
}

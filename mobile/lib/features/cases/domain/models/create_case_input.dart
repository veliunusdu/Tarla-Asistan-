import 'case_category.dart';

class CreateCaseInput {
  const CreateCaseInput({
    required this.farmId,
    required this.category,
    required this.title,
    required this.description,
    this.imageBytes,
    this.imageFileName,
  });

  final String farmId;
  final CaseCategory category;
  final String title;
  final String description;
  final List<int>? imageBytes;
  final String? imageFileName;
}

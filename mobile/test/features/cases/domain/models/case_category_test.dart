import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/create_case_input.dart';

void main() {
  group('CaseCategory', () {
    test('enum values map correctly to display names and backend strings', () {
      expect(CaseCategory.disease.displayName, 'Hastalık');
      expect(CaseCategory.disease.backendValue, 'Disease');

      expect(CaseCategory.pest.displayName, 'Zararlı');
      expect(CaseCategory.pest.backendValue, 'Pest');

      expect(CaseCategory.irrigation.displayName, 'Sulama');
      expect(CaseCategory.irrigation.backendValue, 'Irrigation');

      expect(CaseCategory.nutrition.displayName, 'Besleme / Gübre');
      expect(CaseCategory.nutrition.backendValue, 'Nutrition');

      expect(CaseCategory.weather.displayName, 'Hava Koşulları');
      expect(CaseCategory.weather.backendValue, 'Weather');

      expect(CaseCategory.other.displayName, 'Diğer');
      expect(CaseCategory.other.backendValue, 'Other');
    });
  });

  group('CreateCaseInput', () {
    test('instantiates with required and optional properties', () {
      const input = CreateCaseInput(
        farmId: 'farm-123',
        category: CaseCategory.disease,
        title: 'Yaprak Lekesi',
        description: 'Alt yapraklarda sarı lekeler oluştu.',
        imageBytes: [1, 2, 3],
        imageFileName: 'foto.jpg',
      );

      expect(input.farmId, 'farm-123');
      expect(input.category, CaseCategory.disease);
      expect(input.title, 'Yaprak Lekesi');
      expect(input.description, 'Alt yapraklarda sarı lekeler oluştu.');
      expect(input.imageBytes, [1, 2, 3]);
      expect(input.imageFileName, 'foto.jpg');
    });
  });
}

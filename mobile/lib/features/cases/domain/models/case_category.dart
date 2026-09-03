enum CaseCategory {
  disease,
  pest,
  irrigation,
  nutrition,
  weather,
  other,
}

extension CaseCategoryX on CaseCategory {
  String get displayName => switch (this) {
    CaseCategory.disease => 'Hastalık',
    CaseCategory.pest => 'Zararlı',
    CaseCategory.irrigation => 'Sulama',
    CaseCategory.nutrition => 'Besleme / Gübre',
    CaseCategory.weather => 'Hava Koşulları',
    CaseCategory.other => 'Diğer',
  };

  String get backendValue => switch (this) {
    CaseCategory.disease => 'Disease',
    CaseCategory.pest => 'Pest',
    CaseCategory.irrigation => 'Irrigation',
    CaseCategory.nutrition => 'Nutrition',
    CaseCategory.weather => 'Weather',
    CaseCategory.other => 'Other',
  };
}

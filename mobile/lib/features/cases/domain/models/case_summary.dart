import 'case_category.dart';
import 'case_status.dart';

class CaseSummary {
  const CaseSummary({
    required this.id,
    required this.farmId,
    required this.farmName,
    required this.category,
    required this.status,
    required this.title,
    required this.createdAt,
    required this.updatedAt,
    required this.messageCount,
    required this.mediaCount,
  });

  final String id;
  final String farmId;
  final String farmName;
  final CaseCategory category;
  final CaseStatus status;
  final String title;
  final DateTime createdAt;
  final DateTime updatedAt;
  final int messageCount;
  final int mediaCount;
}

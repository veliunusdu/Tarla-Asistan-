import 'case_category.dart';
import 'case_message.dart';
import 'case_status.dart';

class CaseDetail {
  const CaseDetail({
    required this.id,
    required this.farmId,
    required this.farmName,
    required this.category,
    required this.status,
    required this.title,
    required this.description,
    this.initialMediaUrls = const [],
    this.messages = const [],
    required this.createdAt,
  });

  final String id;
  final String farmId;
  final String farmName;
  final CaseCategory category;
  final CaseStatus status;
  final String title;
  final String description;
  final List<String> initialMediaUrls;
  final List<CaseMessage> messages;
  final DateTime createdAt;
}

enum CaseMessageType {
  comment,
  additionalInfoRequest,
  expertResponse;

  static CaseMessageType fromString(String? val) => switch (val?.toLowerCase()) {
    'additionalinforequest' => CaseMessageType.additionalInfoRequest,
    'expertresponse' => CaseMessageType.expertResponse,
    _ => CaseMessageType.comment,
  };
}

class CaseMessage {
  const CaseMessage({
    required this.id,
    required this.caseId,
    required this.senderId,
    required this.senderName,
    required this.messageType,
    required this.body,
    this.mediaUrls = const [],
    required this.createdAt,
    this.isCurrentUser = false,
  });

  final String id;
  final String caseId;
  final String senderId;
  final String senderName;
  final CaseMessageType messageType;
  final String body;
  final List<String> mediaUrls;
  final DateTime createdAt;
  final bool isCurrentUser;

  bool get isFromExpert =>
      messageType == CaseMessageType.expertResponse ||
      messageType == CaseMessageType.additionalInfoRequest;
}

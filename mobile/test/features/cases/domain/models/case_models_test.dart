import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/cases/domain/models/case_category.dart';
import 'package:mobile/features/cases/domain/models/case_detail.dart';
import 'package:mobile/features/cases/domain/models/case_message.dart';
import 'package:mobile/features/cases/domain/models/case_status.dart';
import 'package:mobile/features/cases/domain/models/case_summary.dart';

void main() {
  group('CaseStatus', () {
    test('parses from backend string and provides display name', () {
      expect(CaseStatus.fromString('Open'), CaseStatus.open);
      expect(CaseStatus.fromString('InReview'), CaseStatus.inReview);
      expect(CaseStatus.fromString('WaitingFarmer'), CaseStatus.waitingFarmer);
      expect(CaseStatus.fromString('Answered'), CaseStatus.answered);
      expect(CaseStatus.fromString('Closed'), CaseStatus.closed);

      expect(CaseStatus.waitingFarmer.displayName, 'Bilgi Bekliyor');
      expect(CaseStatus.waitingFarmer.backendValue, 'WaitingFarmer');
      expect(CaseStatus.closed.displayName, 'Çözüldü / Kapalı');
    });
  });

  group('CaseMessage', () {
    test('correctly identifies expert vs farmer messages', () {
      final expertMsg = CaseMessage(
        id: 'msg-1',
        caseId: 'case-1',
        senderId: 'expert-1',
        senderName: 'Dr. Ayşe',
        messageType: CaseMessageType.expertResponse,
        body: 'İlaçlama tavsiyem ektedir.',
        createdAt: DateTime(2026, 9, 3, 10, 0),
      );

      final farmerMsg = CaseMessage(
        id: 'msg-2',
        caseId: 'case-1',
        senderId: 'farmer-1',
        senderName: 'Mehmet Çiftçi',
        messageType: CaseMessageType.comment,
        body: 'Teşekkürler, uyguladım.',
        createdAt: DateTime(2026, 9, 3, 10, 30),
        isCurrentUser: true,
      );

      expect(expertMsg.isFromExpert, isTrue);
      expect(expertMsg.isCurrentUser, isFalse);
      expect(farmerMsg.isFromExpert, isFalse);
      expect(farmerMsg.isCurrentUser, isTrue);
    });
  });

  group('CaseSummary and CaseDetail', () {
    test('instantiates with expected properties', () {
      final now = DateTime(2026, 9, 3, 10, 0);
      final summary = CaseSummary(
        id: 'c-1',
        farmId: 'f-1',
        farmName: 'Zeytinlik',
        category: CaseCategory.pest,
        status: CaseStatus.waitingFarmer,
        title: 'Zeytin Sineği',
        createdAt: now,
        updatedAt: now,
        messageCount: 3,
        mediaCount: 1,
      );

      expect(summary.title, 'Zeytin Sineği');
      expect(summary.messageCount, 3);
      expect(summary.status, CaseStatus.waitingFarmer);

      final detail = CaseDetail(
        id: 'c-1',
        farmId: 'f-1',
        farmName: 'Zeytinlik',
        category: CaseCategory.pest,
        status: CaseStatus.waitingFarmer,
        title: 'Zeytin Sineği',
        description: 'Tuzaklarda sinek sayısı arttı.',
        createdAt: now,
        initialMediaUrls: ['https://example.com/sinegi.jpg'],
        messages: [],
      );

      expect(detail.description, 'Tuzaklarda sinek sayısı arttı.');
      expect(detail.initialMediaUrls.length, 1);
    });
  });
}

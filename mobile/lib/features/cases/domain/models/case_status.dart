enum CaseStatus {
  open,
  inReview,
  waitingFarmer,
  answered,
  closed;

  String get displayName => switch (this) {
    CaseStatus.open => 'Yeni',
    CaseStatus.inReview => 'İnceleniyor',
    CaseStatus.waitingFarmer => 'Bilgi Bekliyor',
    CaseStatus.answered => 'Yanıtlandı',
    CaseStatus.closed => 'Çözüldü / Kapalı',
  };

  String get backendValue => switch (this) {
    CaseStatus.open => 'Open',
    CaseStatus.inReview => 'InReview',
    CaseStatus.waitingFarmer => 'WaitingFarmer',
    CaseStatus.answered => 'Answered',
    CaseStatus.closed => 'Closed',
  };

  static CaseStatus fromString(String? value) => switch (value?.toLowerCase()) {
    'open' => CaseStatus.open,
    'inreview' => CaseStatus.inReview,
    'waitingfarmer' => CaseStatus.waitingFarmer,
    'answered' => CaseStatus.answered,
    'closed' => CaseStatus.closed,
    _ => CaseStatus.open,
  };
}

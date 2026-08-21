class Faaliyet {
  final String id;
  final String tarlaId;
  final String type;
  final String note;
  final DateTime timestamp;
  final DateTime? dueDate;
  final bool isCompleted;
  final String inputMethod;

  Faaliyet({
    required this.id,
    required this.tarlaId,
    required this.type,
    required this.note,
    required this.timestamp,
    this.dueDate,
    this.isCompleted = false,
    this.inputMethod = 'MANUAL',
  });

  factory Faaliyet.fromJson(Map<String, dynamic> json) {
    return Faaliyet(
      id: json['id'].toString(),
      tarlaId: json['tarlaId'].toString(),
      type: json['type'],
      note: json['note'] ?? "",
      timestamp: DateTime.parse(json['timestamp']),
      dueDate: json['dueDate'] != null ? DateTime.parse(json['dueDate']) : null,
      isCompleted: json['isCompleted'] == 1,
      inputMethod: json['inputMethod']?.toString() ?? 'MANUAL',
    );
  }

  /// SQLite serialization. [inputMethod] is intentionally omitted because the
  /// faaliyetler table has no inputMethod column (no migration added yet).
  /// Firestore accesses [inputMethod] directly via the field, not via toJson.
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'tarlaId': tarlaId,
      'type': type,
      'note': note,
      'timestamp': timestamp.toIso8601String(),
      'dueDate': dueDate?.toIso8601String(),
      'isCompleted': isCompleted ? 1 : 0,
    };
  }
}

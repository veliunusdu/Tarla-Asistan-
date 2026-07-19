class Faaliyet {
  final String id;
  final String tarlaId;
  final String type; // Faaliyet türü (Örn: Gübreleme, Sulama)
  final String note; // Notlar
  final DateTime timestamp; // Oluşturulma veya Tamamlanma tarihi
  final DateTime? dueDate; // Yapılacak faaliyet için planlanan tarih (Opsiyonel)
  final bool isCompleted; // Faaliyet tamamlandı mı?

  Faaliyet({
    required this.id,
    required this.tarlaId,
    required this.type,
    required this.note,
    required this.timestamp,
    this.dueDate,
    this.isCompleted = false,
  });

  // Veritabanından gelen veriyi modele dönüştürme
  factory Faaliyet.fromJson(Map<String, dynamic> json) {
    return Faaliyet(
      id: json['id'].toString(),
      tarlaId: json['tarlaId'].toString(),
      type: json['type'],
      note: json['note'] ?? "",
      timestamp: DateTime.parse(json['timestamp']),
      // dueDate boş olabilir, kontrol ederek parse ediyoruz
      dueDate: json['dueDate'] != null ? DateTime.parse(json['dueDate']) : null,
      // SQLite'dan 0 veya 1 gelir, biz bunu bool'a çeviriyoruz
      isCompleted: json['isCompleted'] == 1,
    );
  }

  // Modeli veritabanına kaydetmek için JSON'a dönüştürme
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'tarlaId': tarlaId,
      'type': type,
      'note': note,
      'timestamp': timestamp.toIso8601String(),
      'dueDate': dueDate?.toIso8601String(),
      // bool değerini SQLite'ın anlayacağı 0 veya 1'e çeviriyoruz
      'isCompleted': isCompleted ? 1 : 0,
    };
  }
}
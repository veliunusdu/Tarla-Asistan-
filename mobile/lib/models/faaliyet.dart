class Faaliyet {
  final String id;
  final String tarlaId;
  final String type;
  final String? note;
  final String? audioPath;
  final String? photos;
  final DateTime timestamp;

  Faaliyet({
    required this.id,
    required this.tarlaId,
    required this.type,
    this.note,
    this.audioPath,
    this.photos,
    required this.timestamp,
  });

  // Veritabanına kaydederken kullanacağımız yapı
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'tarlaId': tarlaId,
      'type': type,
      'note': note,
      'audioPath': audioPath,
      'photos': photos,
      'timestamp': timestamp.toIso8601String(), // DateTime -> String dönüşümü
    };
  }

  // Veritabanından okurken kullanacağımız yapı
  factory Faaliyet.fromJson(Map<String, dynamic> json) {
    return Faaliyet(
      id: json['id'],
      tarlaId: json['tarlaId'],
      type: json['type'],
      note: json['note'],
      audioPath: json['audioPath'],
      photos: json['photos'],
      timestamp: DateTime.parse(json['timestamp']), // String -> DateTime dönüşümü
    );
  }
}
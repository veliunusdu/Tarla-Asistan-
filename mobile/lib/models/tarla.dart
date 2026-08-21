class Tarla {
  final String id;
  final String name;
  final double latitude; // Konum verisi için temel
  final double longitude;
  final double size; // Büyüklük
  final String cropType; // Ürün türü[cite: 1]
  final DateTime plantingDate; // Ekim tarihi[cite: 1]

  Tarla({
    required this.id,
    required this.name,
    required this.latitude,
    required this.longitude,
    required this.size,
    required this.cropType,
    required this.plantingDate,
  });

  // Veritabanından veya API'den gelen veriyi modele dönüştürmek için[cite: 1]
  factory Tarla.fromJson(Map<String, dynamic> json) {
    return Tarla(
      id: json['id'],
      name: json['name'],
      latitude: json['latitude'],
      longitude: json['longitude'],
      size: json['size'],
      cropType: json['cropType'],
      plantingDate: DateTime.parse(json['plantingDate']),
    );
  }

  // Modeli veritabanına veya API'ye kaydetmek için JSON'a dönüştürme[cite: 1]
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'latitude': latitude,
      'longitude': longitude,
      'size': size,
      'cropType': cropType,
      'plantingDate': plantingDate.toIso8601String(),
    };
  }
}

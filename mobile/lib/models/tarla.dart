class Tarla {
  final String id;
  final String name;
  final double latitude; // Konum verisi için temel
  final double longitude;
  final double size; // Büyüklük
  final String cropType;
  final DateTime plantingDate;

  Tarla({
    required this.id,
    required this.name,
    required this.latitude,
    required this.longitude,
    required this.size,
    required this.cropType,
    required this.plantingDate,
  });

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

  factory Tarla.fromApi(Map<String, dynamic> json) {
    final currentCrop = json['current_crop'] is Map
        ? json['current_crop'] as Map
        : const <String, dynamic>{};
    return Tarla(
      id: json['id'].toString(),
      name: json['name']?.toString() ?? 'Tarla',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
      size: (json['size_in_hectares'] as num?)?.toDouble() ?? 0,
      cropType: currentCrop['crop_type']?.toString() ?? 'Ürün belirtilmedi',
      plantingDate:
          DateTime.tryParse(currentCrop['planted_at']?.toString() ?? '') ??
          DateTime.now(),
    );
  }

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

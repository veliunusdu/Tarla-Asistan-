/// Domain model for a farm (tarla).
///
/// `id` and `name` are required — the backend API also marks them as required.
/// All other fields are nullable because:
///   - The backend API allows latitude, longitude, size_in_hectares to be null.
///   - current_crop (and therefore cropType / plantingDate) may be absent.
///   - Legacy local records created before GPS support used 0.0 for coordinates.
///
/// Callers must not generate fake defaults (0.0, empty string, DateTime.now())
/// for null fields; display a fallback label in the UI instead.
class Tarla {
  const Tarla({
    required this.id,
    required this.name,
    this.latitude,
    this.longitude,
    this.size,
    this.cropType,
    this.plantingDate,
  });

  final String id;
  final String name;

  /// Decimal degrees. Null when the user has not set a location yet.
  final double? latitude;

  /// Decimal degrees. Null when the user has not set a location yet.
  final double? longitude;

  /// Farm area in dönüm. Null when not provided.
  final double? size;

  /// Active crop type string. Null when no crop period is active.
  final String? cropType;

  /// Date the current crop was planted. Null when no crop period is active.
  final DateTime? plantingDate;

  /// Constructs a [Tarla] from a SQLite row or a legacy JSON map.
  ///
  /// Numeric columns may arrive as [int] or [double] from sqflite; both are
  /// handled safely via [num.toDouble].  A null or absent value does not
  /// produce a fake default.
  factory Tarla.fromJson(Map<String, dynamic> json) {
    return Tarla(
      id: json['id'] as String,
      name: json['name'] as String,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      size: (json['size'] as num?)?.toDouble(),
      cropType: json['cropType'] as String?,
      plantingDate: json['plantingDate'] == null
          ? null
          : DateTime.parse(json['plantingDate'] as String),
    );
  }

  /// Serialises to a SQLite-compatible map.
  ///
  /// Null fields are written as SQL NULL (not as 0 or empty string).
  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'latitude': latitude,
      'longitude': longitude,
      'size': size,
      'cropType': cropType,
      'plantingDate': plantingDate?.toIso8601String(),
    };
  }
}

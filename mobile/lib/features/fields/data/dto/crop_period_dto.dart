/// OpenAPI: CropPeriodResponse
///
/// Enum fields (crop_type, status) are kept as raw [String] so that unknown
/// server values never cause a runtime crash.  The caller is responsible for
/// validating or displaying them.
class CropPeriodResponseDto {
  const CropPeriodResponseDto({
    required this.id,
    required this.farmId,
    String? cropName,
    required this.cropType,
    required this.variety,
    required this.plantedAt,
    required this.harvestedAt,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  }) : cropName = cropName ?? cropType;

  /// UUID string — OpenAPI: id (uuid, required)
  final String id;

  /// UUID string — OpenAPI: farm_id (uuid, required)
  final String farmId;

  /// Farmer-entered product name (e.g. 'Nohut', 'Buğday').
  final String cropName;

  /// Raw string for the CropType enum value.
  /// Known values: WHEAT, BARLEY, CORN, SUNFLOWER, TOMATO.
  final String cropType;

  /// OpenAPI: variety (string | null, required in envelope but nullable value)
  final String? variety;

  /// OpenAPI: planted_at (date format — YYYY-MM-DD, required)
  final DateTime plantedAt;

  /// OpenAPI: harvested_at (date format | null, required in envelope but nullable)
  final DateTime? harvestedAt;

  /// Raw string for the CropPeriodStatus enum value.
  /// Known values: ACTIVE, ARCHIVED.
  final String status;

  /// OpenAPI: created_at (date-time, required)
  final DateTime createdAt;

  /// OpenAPI: updated_at (date-time, required)
  final DateTime updatedAt;

  static String _fallbackTurkish(String? cropType) => switch (cropType) {
    'WHEAT' || 'Wheat' => 'Buğday',
    'BARLEY' || 'Barley' => 'Arpa',
    'CORN' || 'Corn' => 'Mısır',
    'SUNFLOWER' || 'Sunflower' => 'Ayçiçeği',
    'TOMATO' || 'Tomato' => 'Domates',
    _ => cropType ?? '',
  };

  factory CropPeriodResponseDto.fromJson(Map<String, dynamic> json) {
    final rawCropName = (json['crop_name'] as String?)?.trim();
    final rawCropType = (json['crop_type'] as String?) ?? '';
    final resolvedCropName = (rawCropName != null && rawCropName.isNotEmpty)
        ? rawCropName
        : _fallbackTurkish(rawCropType);

    return CropPeriodResponseDto(
      id: json['id'] as String,
      farmId: json['farm_id'] as String,
      cropName: resolvedCropName,
      cropType: rawCropType.isNotEmpty ? rawCropType : resolvedCropName,
      variety: json['variety'] as String?,
      plantedAt: DateTime.parse(json['planted_at'] as String),
      harvestedAt: json['harvested_at'] == null
          ? null
          : DateTime.parse(json['harvested_at'] as String),
      status: (json['status'] as String?) ?? 'ACTIVE',
      createdAt: DateTime.parse(
        (json['created_at'] ?? json['created_at_utc']) as String,
      ),
      updatedAt: DateTime.parse(
        (json['updated_at'] ?? json['updated_at_utc']) as String,
      ),
    );
  }
}

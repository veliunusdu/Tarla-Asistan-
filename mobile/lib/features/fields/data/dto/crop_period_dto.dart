/// OpenAPI: CropPeriodResponse
///
/// Enum fields (crop_type, status) are kept as raw [String] so that unknown
/// server values never cause a runtime crash.  The caller is responsible for
/// validating or displaying them.
class CropPeriodResponseDto {
  const CropPeriodResponseDto({
    required this.id,
    required this.farmId,
    required this.cropType,
    required this.variety,
    required this.plantedAt,
    required this.harvestedAt,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  });

  /// UUID string — OpenAPI: id (uuid, required)
  final String id;

  /// UUID string — OpenAPI: farm_id (uuid, required)
  final String farmId;

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

  factory CropPeriodResponseDto.fromJson(Map<String, dynamic> json) {
    return CropPeriodResponseDto(
      id: json['id'] as String,
      farmId: json['farm_id'] as String,
      cropType: json['crop_type'] as String,
      variety: json['variety'] as String?,
      plantedAt: DateTime.parse(json['planted_at'] as String),
      harvestedAt: json['harvested_at'] == null
          ? null
          : DateTime.parse(json['harvested_at'] as String),
      status: json['status'] as String,
      createdAt: DateTime.parse(
        (json['created_at'] ?? json['created_at_utc']) as String,
      ),
      updatedAt: DateTime.parse(
        (json['updated_at'] ?? json['updated_at_utc']) as String,
      ),
    );
  }
}

import 'crop_period_dto.dart';

/// OpenAPI: FarmResponse
///
/// All fields listed as `required` in the OpenAPI envelope but many carry
/// a nullable value (anyOf [type, null]).  Those are modelled as nullable
/// Dart fields.
class FarmResponseDto {
  const FarmResponseDto({
    required this.id,
    required this.ownerId,
    required this.name,
    required this.latitude,
    required this.longitude,
    required this.sizeInHectares,
    required this.irrigationMethod,
    required this.soilType,
    required this.note,
    required this.archivedAt,
    required this.createdAt,
    required this.updatedAt,
    required this.currentCrop,
  });

  /// UUID string — OpenAPI: id
  final String id;

  /// UUID string — OpenAPI: owner_id
  final String ownerId;

  final String name;

  /// OpenAPI: latitude (number | null)
  final double? latitude;

  /// OpenAPI: longitude (number | null)
  final double? longitude;

  /// OpenAPI: size_in_hectares (number | null)
  final double? sizeInHectares;

  /// Raw IrrigationMethod enum string or null.
  /// Known values: DRIP, SPRINKLER, FLOOD, RAINFED, OTHER.
  final String? irrigationMethod;

  /// OpenAPI: soil_type (string | null)
  final String? soilType;

  /// OpenAPI: note (string | null)
  final String? note;

  /// OpenAPI: archived_at (date-time | null)
  final DateTime? archivedAt;

  /// OpenAPI: created_at (date-time)
  final DateTime createdAt;

  /// OpenAPI: updated_at (date-time)
  final DateTime updatedAt;

  /// OpenAPI: current_crop (CropPeriodResponse | null)
  final CropPeriodResponseDto? currentCrop;

  factory FarmResponseDto.fromJson(Map<String, dynamic> json) {
    final rawCrop = json['current_crop'] ?? json['current_crop_period'];
    final createdAt = DateTime.parse(
      (json['created_at'] ?? json['created_at_utc']) as String,
    );
    return FarmResponseDto(
      id: json['id'] as String,
      ownerId: json['owner_id'] as String,
      name: json['name'] as String,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      sizeInHectares: (json['size_in_hectares'] as num?)?.toDouble(),
      irrigationMethod: json['irrigation_method'] as String?,
      soilType: json['soil_type'] as String?,
      note: json['note'] as String?,
      archivedAt: json['archived_at'] == null
          ? null
          : DateTime.parse(json['archived_at'] as String),
      createdAt: createdAt,
      updatedAt: (json['updated_at'] ?? json['updated_at_utc']) == null
          ? createdAt
          : DateTime.parse(
              (json['updated_at'] ?? json['updated_at_utc']) as String,
            ),
      currentCrop: rawCrop == null
          ? null
          : CropPeriodResponseDto.fromJson(rawCrop as Map<String, dynamic>),
    );
  }
}

// ---------------------------------------------------------------------------

/// OpenAPI: FarmListResponse
class FarmListResponseDto {
  const FarmListResponseDto({
    required this.items,
    required this.total,
    required this.limit,
    required this.offset,
  });

  final List<FarmResponseDto> items;
  final int total;
  final int limit;
  final int offset;

  factory FarmListResponseDto.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'] as List<dynamic>;
    return FarmListResponseDto(
      items: rawItems
          .map((e) => FarmResponseDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      total: json['total'] as int,
      limit: json['limit'] as int,
      offset: json['offset'] as int,
    );
  }
}

// ---------------------------------------------------------------------------

/// OpenAPI: FarmMutationResponse
/// Returned by POST /farms (201) and PATCH /farms/{farm_id} (200).
class FarmMutationResponseDto {
  const FarmMutationResponseDto({required this.farm, required this.warnings});

  final FarmResponseDto farm;

  /// OpenAPI: warnings — optional list, absent when no warnings.
  final List<String> warnings;

  factory FarmMutationResponseDto.fromJson(Map<String, dynamic> json) {
    final rawWarnings = json['warnings'];
    return FarmMutationResponseDto(
      farm: FarmResponseDto.fromJson(json['farm'] as Map<String, dynamic>),
      warnings: rawWarnings == null
          ? const []
          : List<String>.from(rawWarnings as List),
    );
  }
}

// ---------------------------------------------------------------------------

/// OpenAPI: FarmCreate request body
///
/// Required fields: name, latitude, longitude, crop_type, planted_at.
/// Optional fields are null by default and omitted from [toJson] when null.
class FarmCreateRequestDto {
  const FarmCreateRequestDto({
    required this.name,
    required this.latitude,
    required this.longitude,
    required this.cropType,
    required this.plantedAt,
    this.sizeInHectares,
    this.irrigationMethod,
    this.soilType,
    this.note,
    this.variety,
  });

  final String name;
  final double? latitude;
  final double? longitude;

  /// Raw CropType enum value (e.g. 'WHEAT').
  final String cropType;

  /// ISO-8601 date string in YYYY-MM-DD format.
  final String plantedAt;

  final double? sizeInHectares;
  final String? irrigationMethod;
  final String? soilType;
  final String? note;
  final String? variety;

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'name': name,
      'latitude': latitude,
      'longitude': longitude,
      'crop_type': cropType,
      'planted_at': plantedAt,
      if (sizeInHectares != null) 'size_in_hectares': sizeInHectares,
      if (irrigationMethod != null) 'irrigation_method': irrigationMethod,
      if (soilType != null) 'soil_type': soilType,
      if (note != null) 'note': note,
      if (variety != null) 'variety': variety,
    };
  }
}

// ---------------------------------------------------------------------------

/// OpenAPI: FarmUpdate request body (PATCH — sparse update)
///
/// Only non-null fields are serialised.  Null means "do not change this
/// field", not "set this field to null on the server".
class FarmUpdateRequestDto {
  const FarmUpdateRequestDto({
    this.name,
    this.latitude,
    this.longitude,
    this.sizeInHectares,
    this.irrigationMethod,
    this.soilType,
    this.note,
  });

  final String? name;
  final double? latitude;
  final double? longitude;
  final double? sizeInHectares;
  final String? irrigationMethod;
  final String? soilType;
  final String? note;

  /// Returns a map containing only the fields explicitly provided (non-null).
  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      if (name != null) 'name': name,
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (sizeInHectares != null) 'size_in_hectares': sizeInHectares,
      if (irrigationMethod != null) 'irrigation_method': irrigationMethod,
      if (soilType != null) 'soil_type': soilType,
      if (note != null) 'note': note,
    };
  }
}

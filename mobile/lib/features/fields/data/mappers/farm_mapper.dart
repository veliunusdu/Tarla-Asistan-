import '../../../../models/tarla.dart';
import '../dto/farm_dto.dart';

/// Maps [FarmResponseDto] (API layer) to [Tarla] (SQLite / screen layer).
///
/// Since [Tarla] now has nullable fields for latitude, longitude, size,
/// cropType, and plantingDate, every valid [FarmResponseDto] (one that has a
/// non-empty id and name) can be represented without fabricating fake data.
/// Nullable API values are forwarded as nullable Tarla fields so that the UI
/// can display appropriate fallback labels.
class FarmMapper {
  FarmMapper._();

  /// Converts [dto] to a [Tarla].
  ///
  /// Nullable API fields (latitude, longitude, sizeInHectares, currentCrop)
  /// are mapped to the corresponding nullable Tarla fields.
  /// No fake coordinates, empty crop strings, or placeholder dates are generated.
  static Tarla fromDto(FarmResponseDto dto) {
    return Tarla(
      id: dto.id,
      name: dto.name,
      latitude: dto.latitude,
      longitude: dto.longitude,
      size: dto.sizeInHectares,
      cropType: dto.currentCrop?.cropName ?? dto.currentCrop?.cropType,
      plantingDate: dto.currentCrop?.plantedAt,
    );
  }

  /// Converts a list of DTOs to [Tarla] objects.
  ///
  /// Every valid DTO in the list produces a [Tarla] — no records are silently
  /// dropped due to missing optional fields.
  static List<Tarla> fromDtoList(List<FarmResponseDto> dtos) {
    return dtos.map(fromDto).toList();
  }
}

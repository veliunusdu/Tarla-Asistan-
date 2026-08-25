import '../../../../models/tarla.dart';
import '../dto/farm_dto.dart';

/// Maps [FarmResponseDto] (API layer) to [Tarla] (SQLite / screen layer).
///
/// ## Known mapping limitations
///
/// The [Tarla] model requires all fields to be non-null:
///   - [Tarla.latitude] and [Tarla.longitude] are `double` (non-nullable).
///   - [Tarla.size] is `double` (non-nullable).
///   - [Tarla.cropType] and [Tarla.plantingDate] are non-nullable.
///
/// [FarmResponseDto] allows several of those to be null:
///   - `latitude` and `longitude` may be null.
///   - `sizeInHectares` may be null.
///   - `currentCrop` (and therefore `cropType` and `plantedAt`) may be null.
///
/// [fromDto] returns `null` for any [FarmResponseDto] where the required
/// [Tarla] fields cannot be populated without fabricating data.  No fake
/// coordinates, no empty crop strings, and no today's date are generated.
///
/// At the time of writing, the [Tarla] model is not extended to handle these
/// nullable cases.  If the product requires showing farms without a crop
/// period or without coordinates, the [Tarla] model must first be updated
/// to make those fields nullable.
class FarmMapper {
  FarmMapper._();

  /// Converts [dto] to a [Tarla] or returns `null` when the DTO contains
  /// values that cannot be expressed by the current [Tarla] model.
  ///
  /// Null is returned (rather than throwing) so callers can filter out
  /// incomplete records without crashing.
  static Tarla? fromDto(FarmResponseDto dto) {
    final latitude = dto.latitude;
    final longitude = dto.longitude;
    final crop = dto.currentCrop;
    final cropType = crop?.cropType;
    final plantedAt = crop?.plantedAt;

    if (latitude == null ||
        longitude == null ||
        cropType == null ||
        plantedAt == null) {
      return null;
    }

    // sizeInHectares is nullable in API but Tarla.size is required double.
    // Return null rather than fabricating 0.0.
    final size = dto.sizeInHectares;
    if (size == null) return null;

    return Tarla(
      id: dto.id,
      name: dto.name,
      latitude: latitude,
      longitude: longitude,
      size: size,
      cropType: cropType,
      plantingDate: plantedAt,
    );
  }

  /// Converts a list of DTOs, silently dropping any entry that cannot be
  /// represented as [Tarla].  The caller can compare list lengths to detect
  /// dropped items if needed.
  static List<Tarla> fromDtoList(List<FarmResponseDto> dtos) {
    return dtos.map(fromDto).whereType<Tarla>().toList();
  }
}

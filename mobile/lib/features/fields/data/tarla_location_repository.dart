import '../../location/domain/tarla_location.dart';

abstract interface class TarlaLocationRepository {
  Future<void> updateTarlaLocation(String id, TarlaLocation location);
}

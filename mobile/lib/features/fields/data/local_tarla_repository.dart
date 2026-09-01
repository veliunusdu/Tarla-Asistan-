import '../../location/domain/tarla_location.dart';
import '../../../models/tarla.dart';
import '../../../services/database_helper.dart';
import 'tarla_location_repository.dart';
import 'tarla_repository.dart';

class LocalTarlaRepository
    implements TarlaRepository, TarlaLocationRepository {
  const LocalTarlaRepository();

  @override
  Future<List<Tarla>> getTarlalar() => DatabaseHelper.instance.getTarlalar();

  @override
  Future<void> addTarla(Tarla tarla) =>
      DatabaseHelper.instance.insertTarla(tarla);

  @override
  Future<void> archiveTarla(String id) => DatabaseHelper.instance.deleteTarla(id);

  @override
  Future<void> updateTarlaLocation(String id, TarlaLocation location) =>
      DatabaseHelper.instance.updateTarlaLocation(
        id,
        location.latitude,
        location.longitude,
      );
}

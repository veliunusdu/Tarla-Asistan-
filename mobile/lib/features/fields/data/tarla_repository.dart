import '../../../models/tarla.dart';
import 'tarla_read_repository.dart';

export 'tarla_location_repository.dart';

abstract interface class TarlaRepository implements TarlaReadRepository {
  @override
  Future<List<Tarla>> getTarlalar();
  Future<void> addTarla(Tarla tarla);
}

abstract interface class TarlaArchiveRepository {
  Future<void> archiveTarla(String id);
}

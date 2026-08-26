import '../../../models/tarla.dart';
import 'tarla_read_repository.dart';

abstract interface class TarlaRepository implements TarlaReadRepository {
  @override
  Future<List<Tarla>> getTarlalar();
  Future<void> addTarla(Tarla tarla);
}

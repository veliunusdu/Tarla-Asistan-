import '../../../models/tarla.dart';
import '../../../services/database_helper.dart';
import 'tarla_repository.dart';

class LocalTarlaRepository implements TarlaRepository {
  const LocalTarlaRepository();

  @override
  Future<List<Tarla>> getTarlalar() => DatabaseHelper.instance.getTarlalar();

  @override
  Future<void> addTarla(Tarla tarla) =>
      DatabaseHelper.instance.insertTarla(tarla);
}

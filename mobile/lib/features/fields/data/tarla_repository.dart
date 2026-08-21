import '../../../models/tarla.dart';

abstract interface class TarlaRepository {
  Future<List<Tarla>> getTarlalar();
  Future<void> addTarla(Tarla tarla);
}

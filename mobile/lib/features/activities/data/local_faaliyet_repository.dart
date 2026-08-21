import '../../../models/faaliyet.dart';
import '../../../services/database_helper.dart';
import 'faaliyet_repository.dart';

class LocalFaaliyetRepository implements FaaliyetRepository {
  const LocalFaaliyetRepository();

  @override
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) =>
      DatabaseHelper.instance.getFaaliyetler(tarlaId);

  @override
  Future<void> addFaaliyet(Faaliyet faaliyet) =>
      DatabaseHelper.instance.insertFaaliyet(faaliyet);

  @override
  Future<List<Faaliyet>> getTumFaaliyetler() =>
      DatabaseHelper.instance.getTumFaaliyetler();
}

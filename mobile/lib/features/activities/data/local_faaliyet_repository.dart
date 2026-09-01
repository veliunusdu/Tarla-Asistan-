import '../../../models/faaliyet.dart';
import '../../../services/database_helper.dart';
import 'faaliyet_repository.dart';

class LocalFaaliyetRepository
    implements
        FaaliyetRepository,
        FaaliyetDeleteRepository,
        PlanliGorevRepository,
        PlanliGorevCompletionRepository {
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

  @override
  Future<void> deleteFaaliyet(String id) async {
    await DatabaseHelper.instance.deleteFaaliyet(id);
  }

  @override
  Future<void> addPlanliGorev(Faaliyet gorev) => addFaaliyet(gorev);

  @override
  Future<List<Faaliyet>> getPlanliGorevler() async => [];

  @override
  Future<void> completePlanliGorev(String id, {String? note}) async {
    await DatabaseHelper.instance.completePlanliGorev(id);
  }
}

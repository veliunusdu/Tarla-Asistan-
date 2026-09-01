import '../../../models/faaliyet.dart';

abstract interface class FaaliyetRepository {
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId);
  Future<void> addFaaliyet(Faaliyet faaliyet);
  Future<List<Faaliyet>> getTumFaaliyetler();
}

abstract interface class FaaliyetDeleteRepository {
  Future<void> deleteFaaliyet(String id);
}

abstract interface class PlanliGorevRepository {
  Future<void> addPlanliGorev(Faaliyet gorev);
  Future<List<Faaliyet>> getPlanliGorevler();
}

abstract interface class PlanliGorevCompletionRepository {
  Future<void> completePlanliGorev(String id, {String? note});
}

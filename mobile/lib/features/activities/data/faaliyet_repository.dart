import '../../../models/faaliyet.dart';

abstract interface class FaaliyetRepository {
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId);
  Future<void> addFaaliyet(Faaliyet faaliyet);
  Future<List<Faaliyet>> getTumFaaliyetler();
}

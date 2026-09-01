import '../../../models/faaliyet.dart';

abstract interface class FaaliyetRepository {
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId);
  Future<void> addFaaliyet(Faaliyet faaliyet);
  Future<List<Faaliyet>> getTumFaaliyetler();

  /// Belirtilen ID'li faaliyeti hem yerel veritabanından hem de
  /// canlı backend'den siler. Backend isteği başarısız olursa
  /// yerel silme de geri alınmaz — hata üste taşınır.
  Future<void> deleteFaaliyet(String id);

  /// Belirtilen ID'li planlı faaliyeti tamamlandı olarak işaretler.
  /// Canlı repository backend PATCH isteği gönderir;
  /// yerel repository SQLite kaydını günceller.
  Future<void> markAsCompleted(String id);
}

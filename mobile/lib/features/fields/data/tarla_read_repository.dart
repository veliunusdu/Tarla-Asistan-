import '../../../models/tarla.dart';

/// Minimal read-only farm interface for read-only farm consumers.
///
/// [TarlaRepository] extends this interface, so [LocalTarlaRepository]
/// satisfies it without any changes.  [BackendTarlaReadRepository] provides
/// a backend-based implementation for DI without touching the write path.
abstract interface class TarlaReadRepository {
  Future<List<Tarla>> getTarlalar();
}

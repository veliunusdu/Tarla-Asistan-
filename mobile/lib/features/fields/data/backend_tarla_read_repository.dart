import '../../../models/tarla.dart';
import 'farm_remote_repository.dart';
import 'mappers/farm_mapper.dart';
import 'tarla_read_repository.dart';

/// Backend-backed read-only implementation of [TarlaReadRepository].
///
/// Fetches all active farms from [FarmRemoteRepository] using transparent
/// pagination, maps each [FarmResponseDto] to a [Tarla] via [FarmMapper],
/// and returns the full list in backend order.
///
/// **Pagination algorithm**:
/// - Page size: 50 (the backend maximum used here).
/// - Stops when the page is empty, the page has fewer items than requested
///   (last page signal), or the running offset reaches the backend-reported
///   total.
/// - Duplicate IDs across pages are silently deduplicated; first-occurrence
///   order is preserved.
/// - Archived farms are excluded (`includeArchived=false`).
///
/// **Not implemented**: local caching.  Backend records are never written to
/// SQLite in this step; that is a separate, later task.
class BackendTarlaReadRepository implements TarlaReadRepository {
  const BackendTarlaReadRepository({required FarmRemoteRepository this._remote});

  final FarmRemoteRepository _remote;

  static const _pageSize = 50;

  @override
  Future<List<Tarla>> getTarlalar() async {
    final result = <Tarla>[];
    final seen = <String>{};
    var offset = 0;

    while (true) {
      final page = await _remote.getFarms(
        includeArchived: false,
        limit: _pageSize,
        offset: offset,
      );

      // Empty page → all records fetched (or backend returned nothing).
      if (page.items.isEmpty) break;

      for (final dto in page.items) {
        // Dedup: same ID can appear on different pages due to data races.
        if (seen.add(dto.id)) {
          result.add(FarmMapper.fromDto(dto));
        }
      }

      final nextOffset = offset + page.items.length;

      // Reached the total reported by the backend.
      if (nextOffset >= page.total) break;

      // Received fewer items than requested → this was the last page.
      if (page.items.length < _pageSize) break;

      offset = nextOffset;
    }

    return result;
  }
}

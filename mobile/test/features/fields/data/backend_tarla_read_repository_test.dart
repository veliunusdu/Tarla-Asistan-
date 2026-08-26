import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/fields/data/backend_tarla_read_repository.dart';
import 'package:mobile/features/fields/data/dto/crop_period_dto.dart';
import 'package:mobile/features/fields/data/dto/farm_dto.dart';
import 'package:mobile/features/fields/data/farm_remote_repository.dart';
import 'package:mobile/services/api_client.dart';

// ---------------------------------------------------------------------------
// Fake remote repository
// ---------------------------------------------------------------------------

/// Controls which pages are returned by [getFarms] based on [offset].
class FakeFarmRemoteRepository implements FarmRemoteRepository {
  FakeFarmRemoteRepository(this._handler);

  /// Called with (includeArchived, limit, offset) each time [getFarms] fires.
  final FarmListResponseDto Function({
    required bool includeArchived,
    required int limit,
    required int offset,
  })
  _handler;

  final List<Map<String, dynamic>> calls = [];

  @override
  Future<FarmListResponseDto> getFarms({
    bool includeArchived = false,
    int limit = 50,
    int offset = 0,
  }) async {
    calls.add({
      'includeArchived': includeArchived,
      'limit': limit,
      'offset': offset,
    });
    return _handler(
      includeArchived: includeArchived,
      limit: limit,
      offset: offset,
    );
  }

  @override
  Future<FarmResponseDto> getFarm(String farmId) => throw UnimplementedError();

  @override
  Future<FarmMutationResponseDto> createFarm(FarmCreateRequestDto request) =>
      throw UnimplementedError();

  @override
  Future<FarmMutationResponseDto> updateFarm(
    String farmId,
    FarmUpdateRequestDto request,
  ) => throw UnimplementedError();

  @override
  Future<void> archiveFarm(String farmId) => throw UnimplementedError();
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

FarmResponseDto _makeDto({
  String id = 'farm-1',
  String name = 'Test Tarla',
  double? latitude,
  double? longitude,
  double? sizeInHectares,
  CropPeriodResponseDto? currentCrop,
}) => FarmResponseDto(
  id: id,
  ownerId: 'owner-1',
  name: name,
  latitude: latitude,
  longitude: longitude,
  sizeInHectares: sizeInHectares,
  irrigationMethod: null,
  soilType: null,
  note: null,
  archivedAt: null,
  createdAt: DateTime(2026),
  updatedAt: DateTime(2026),
  currentCrop: currentCrop,
);

FarmListResponseDto _page(
  List<FarmResponseDto> items, {
  required int total,
  int limit = 50,
  int offset = 0,
}) => FarmListResponseDto(
  items: items,
  total: total,
  limit: limit,
  offset: offset,
);

// ---------------------------------------------------------------------------
// Tests 1–10
// ---------------------------------------------------------------------------

void main() {
  // Test 1 — tek sayfalı backend response Tarla listesine map edilir.
  test('1. tek sayfalı response doğru Tarla listesine map edilir', () async {
    final farm1 = _makeDto(id: 'f1', name: 'Kuzey', sizeInHectares: 10.0);
    final farm2 = _makeDto(
      id: 'f2',
      name: 'Güney',
      latitude: 38.5,
      longitude: 35.0,
    );

    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) =>
          _page([farm1, farm2], total: 2, offset: offset),
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();

    expect(tarlalar, hasLength(2));
    expect(tarlalar[0].id, 'f1');
    expect(tarlalar[0].name, 'Kuzey');
    expect(tarlalar[0].size, 10.0);
    expect(tarlalar[1].id, 'f2');
  });

  // Test 2 — nullable alanlı farm kaybolmaz.
  test('2. nullable alanlı farm kaybolmaz', () async {
    final nullableDto = _makeDto(
      id: 'null-farm',
      name: 'Sadece İsim',
      latitude: null,
      longitude: null,
      sizeInHectares: null,
      currentCrop: null,
    );

    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) =>
          _page([nullableDto], total: 1, offset: offset),
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();

    expect(tarlalar, hasLength(1));
    expect(tarlalar.first.id, 'null-farm');
    expect(tarlalar.first.latitude, isNull);
    expect(tarlalar.first.size, isNull);
    expect(tarlalar.first.cropType, isNull);
  });

  // Test 3 — backend sırası korunur.
  test('3. backend sırası korunur', () async {
    final dtos = [_makeDto(id: 'a'), _makeDto(id: 'b'), _makeDto(id: 'c')];

    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) =>
          _page(dtos, total: 3, offset: offset),
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();

    expect(tarlalar.map((t) => t.id).toList(), ['a', 'b', 'c']);
  });

  // Test 4 — 50'den fazla kayıtta sonraki sayfa istenir.
  test('4. 50\'den fazla kayıtta sonraki sayfa istenir', () async {
    final page1 = List.generate(50, (i) => _makeDto(id: 'p1-$i', name: 'F$i'));
    final page2 = List.generate(5, (i) => _makeDto(id: 'p2-$i', name: 'G$i'));

    final remote = FakeFarmRemoteRepository(({
      required includeArchived,
      required limit,
      required offset,
    }) {
      if (offset == 0) return _page(page1, total: 55, offset: 0);
      return _page(page2, total: 55, offset: 50);
    });
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();

    expect(tarlalar, hasLength(55));
    expect(remote.calls, hasLength(2));
  });

  // Test 5 — offset doğru ilerler.
  test('5. offset doğru ilerler', () async {
    final page1 = List.generate(50, (i) => _makeDto(id: 'x-$i'));
    final page2 = [_makeDto(id: 'x-50')];

    final remote = FakeFarmRemoteRepository(({
      required includeArchived,
      required limit,
      required offset,
    }) {
      if (offset == 0) return _page(page1, total: 51);
      return _page(page2, total: 51, offset: 50);
    });
    final repo = BackendTarlaReadRepository(remote: remote);

    await repo.getTarlalar();

    expect(remote.calls[0]['offset'], 0);
    expect(remote.calls[1]['offset'], 50);
  });

  // Test 6 — includeArchived=false gönderilir.
  test('6. includeArchived=false her sayfada gönderilir', () async {
    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) =>
          _page([_makeDto()], total: 1, offset: offset),
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    await repo.getTarlalar();

    for (final call in remote.calls) {
      expect(call['includeArchived'], isFalse);
    }
  });

  // Test 7 — aynı ID farklı sayfalarda yalnızca bir kez döner.
  test('7. aynı ID farklı sayfalarda yalnızca bir kez döner', () async {
    final farm = _makeDto(id: 'dup-id', name: 'Tekrarlayan');
    final page1 = [farm, _makeDto(id: 'other-1')];
    final page2 = [farm, _makeDto(id: 'other-2')]; // farm appears again

    final remote = FakeFarmRemoteRepository(({
      required includeArchived,
      required limit,
      required offset,
    }) {
      if (offset == 0) return _page(page1, total: 100);
      if (offset == 2) return _page(page2, total: 100, offset: 2);
      return _page([], total: 100, offset: offset);
    });
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();
    final ids = tarlalar.map((t) => t.id).toList();

    expect(ids.where((id) => id == 'dup-id'), hasLength(1));
  });

  // Test 8 — boş sayfa sonsuz döngü oluşturmaz.
  test('8. boş sayfa sonsuz döngü oluşturmaz', () async {
    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) => _page(
        [],
        total: 5,
        offset: offset,
      ), // total says 5 but items is empty
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    final tarlalar = await repo.getTarlalar();

    expect(tarlalar, isEmpty);
    expect(remote.calls, hasLength(1)); // Only one call was made
  });

  // Test 9 — tutarsız total/limit yanıtı sonsuz döngü oluşturmaz.
  test(
    '9. tutarsız total: az item gelen son sayfa sonsuz döngü oluşturmaz',
    () async {
      // Backend says total=100 but returns only 3 items (< limit=50) → last page signal
      final items = List.generate(3, (i) => _makeDto(id: 'inc-$i'));

      final remote = FakeFarmRemoteRepository(
        ({required includeArchived, required limit, required offset}) =>
            _page(items, total: 100, offset: offset),
      );
      final repo = BackendTarlaReadRepository(remote: remote);

      final tarlalar = await repo.getTarlalar();

      expect(tarlalar, hasLength(3));
      expect(
        remote.calls,
        hasLength(1),
      ); // Stopped after first (incomplete) page
    },
  );

  // Test 10 — ApiException korunur.
  test('10. ApiException repository katmanından bozulmadan geçer', () async {
    final remote = FakeFarmRemoteRepository(
      ({required includeArchived, required limit, required offset}) =>
          throw const ApiException(
            'Sunucu hatası',
            statusCode: 500,
            retryable: true,
          ),
    );
    final repo = BackendTarlaReadRepository(remote: remote);

    await expectLater(
      repo.getTarlalar(),
      throwsA(
        isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 500)
            .having((e) => e.retryable, 'retryable', isTrue),
      ),
    );
  });
}

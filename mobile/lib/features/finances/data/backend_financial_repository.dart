import '../../../services/api_client.dart';
import '../domain/finance_models.dart';
import 'finance_repository.dart';
import 'crop_period_source.dart';

class BackendFinancialRepository implements FinancialRepository {
  const BackendFinancialRepository({required ApiClient apiClient})
    : _api = apiClient;
  final ApiClient _api;
  FinanceCropPeriodSource get periodSource =>
      BackendFinanceCropPeriodSource(apiClient: _api);
  String _period(String farmId, String periodId, String suffix) =>
      '/farms/$farmId/production-periods/$periodId/$suffix';

  @override
  Future<FinancialSummaryDto> getFinancialSummary(
    String farmId,
    String cropPeriodId,
  ) async => FinancialSummaryDto.fromJson(
    await _api.getJson(_period(farmId, cropPeriodId, 'financial-summary')),
  );
  @override
  Future<List<ExpenseDto>> listExpenses(
    String farmId,
    String cropPeriodId,
  ) async => (await _api.getJsonList(_period(farmId, cropPeriodId, 'expenses')))
      .whereType<Map>()
      .map((e) => ExpenseDto.fromJson(Map<String, dynamic>.from(e)))
      .toList();
  @override
  Future<ExpenseDto> createExpense(
    String farmId,
    String cropPeriodId, {
    required ExpenseCategory category,
    required double amount,
    required DateTime occurredAtUtc,
    String? note,
    String? clientOperationId,
  }) async => ExpenseDto.fromJson(
    await _api.postJson(_period(farmId, cropPeriodId, 'expenses'), {
      'category': expenseCategoryValue(category),
      'amount': amount,
      'occurred_at_utc': occurredAtUtc.toUtc().toIso8601String(),
      if (note != null) 'note': note,
      if (clientOperationId != null) 'client_operation_id': clientOperationId,
    }),
  );
  @override
  Future<ExpenseDto> updateExpense(
    String id, {
    required ExpenseCategory category,
    required double amount,
    required DateTime occurredAtUtc,
    String? note,
  }) async => ExpenseDto.fromJson(
    await _api.patchJson('/expenses/$id', {
      'category': expenseCategoryValue(category),
      'amount': amount,
      'occurred_at_utc': occurredAtUtc.toUtc().toIso8601String(),
      if (note != null) 'note': note,
    }),
  );
  @override
  Future<void> archiveExpense(String id) => _api.delete('/expenses/$id');
  @override
  Future<List<CropSaleDto>> listSales(
    String farmId,
    String cropPeriodId,
  ) async => (await _api.getJsonList(_period(farmId, cropPeriodId, 'sales')))
      .whereType<Map>()
      .map((e) => CropSaleDto.fromJson(Map<String, dynamic>.from(e)))
      .toList();
  @override
  Future<CropSaleDto> createSale(
    String farmId,
    String cropPeriodId, {
    required double harvestQuantity,
    required String unit,
    required double unitPrice,
    required DateOnly soldAt,
    String? buyerOrMarketNote,
    String? clientOperationId,
  }) async => CropSaleDto.fromJson(
    await _api.postJson(_period(farmId, cropPeriodId, 'sales'), {
      'harvest_quantity': harvestQuantity,
      'unit': unit,
      'unit_price': unitPrice,
      'sold_at': soldAt.iso,
      if (buyerOrMarketNote != null) 'buyer_or_market_note': buyerOrMarketNote,
      if (clientOperationId != null) 'client_operation_id': clientOperationId,
    }),
  );
  @override
  Future<CropSaleDto> updateSale(
    String id, {
    required double harvestQuantity,
    required String unit,
    required double unitPrice,
    required DateOnly soldAt,
    String? buyerOrMarketNote,
  }) async => CropSaleDto.fromJson(
    await _api.patchJson('/sales/$id', {
      'harvest_quantity': harvestQuantity,
      'unit': unit,
      'unit_price': unitPrice,
      'sold_at': soldAt.iso,
      if (buyerOrMarketNote != null) 'buyer_or_market_note': buyerOrMarketNote,
    }),
  );
  @override
  Future<void> archiveSale(String id) => _api.delete('/sales/$id');
}

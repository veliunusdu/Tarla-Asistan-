import '../domain/finance_models.dart';

abstract interface class FinancialRepository {
  Future<FinancialSummaryDto> getFinancialSummary(
    String farmId,
    String cropPeriodId,
  );
  Future<List<ExpenseDto>> listExpenses(String farmId, String cropPeriodId);
  Future<ExpenseDto> createExpense(
    String farmId,
    String cropPeriodId, {
    required ExpenseCategory category,
    required double amount,
    required DateTime occurredAtUtc,
    String? note,
    String? clientOperationId,
  });
  Future<ExpenseDto> updateExpense(
    String id, {
    required ExpenseCategory category,
    required double amount,
    required DateTime occurredAtUtc,
    String? note,
  });
  Future<void> archiveExpense(String id);
  Future<List<CropSaleDto>> listSales(String farmId, String cropPeriodId);
  Future<CropSaleDto> createSale(
    String farmId,
    String cropPeriodId, {
    required double harvestQuantity,
    required String unit,
    required double unitPrice,
    required DateOnly soldAt,
    String? buyerOrMarketNote,
    String? clientOperationId,
  });
  Future<CropSaleDto> updateSale(
    String id, {
    required double harvestQuantity,
    required String unit,
    required double unitPrice,
    required DateOnly soldAt,
    String? buyerOrMarketNote,
  });
  Future<void> archiveSale(String id);
}

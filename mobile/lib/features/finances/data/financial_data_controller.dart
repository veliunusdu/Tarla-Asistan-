import 'package:flutter/foundation.dart';
import '../domain/finance_models.dart';
import 'finance_repository.dart';

class FinancialDataController extends ChangeNotifier {
  FinancialDataController(this.repository);
  final FinancialRepository repository;
  bool isLoading = false;
  String? error;
  FinancialSummaryDto? summary;
  List<ExpenseDto> expenses = const [];
  List<CropSaleDto> sales = const [];

  Future<void> load(String farmId, String periodId) async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      final result = await Future.wait([
        repository.getFinancialSummary(farmId, periodId),
        repository.listExpenses(farmId, periodId),
        repository.listSales(farmId, periodId),
      ]);
      summary = result[0] as FinancialSummaryDto;
      expenses = result[1] as List<ExpenseDto>;
      sales = result[2] as List<CropSaleDto>;
    } catch (e) {
      error = e.toString();
    }
    isLoading = false;
    notifyListeners();
  }

  Future<void> refresh(String farmId, String periodId) =>
      load(farmId, periodId);
}

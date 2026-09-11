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
  int _loadRequestId = 0;

  Future<void> load(String farmId, String periodId) async {
    final requestId = ++_loadRequestId;
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      final result = await Future.wait([
        repository.getFinancialSummary(farmId, periodId),
        repository.listExpenses(farmId, periodId),
        repository.listSales(farmId, periodId),
      ]);
      if (requestId != _loadRequestId) return;
      summary = result[0] as FinancialSummaryDto;
      expenses = result[1] as List<ExpenseDto>;
      sales = result[2] as List<CropSaleDto>;
    } catch (e) {
      if (requestId != _loadRequestId) return;
      error = e.toString();
    } finally {
      if (requestId == _loadRequestId) {
        isLoading = false;
        notifyListeners();
      }
    }
  }

  Future<void> refresh(String farmId, String periodId) =>
      load(farmId, periodId);
}

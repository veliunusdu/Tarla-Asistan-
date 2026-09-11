import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/finances/data/finance_repository.dart';
import 'package:mobile/features/finances/data/financial_data_controller.dart';
import 'package:mobile/features/finances/domain/finance_models.dart';

void main() {
  test(
    'late response from a previous period does not replace current data',
    () async {
      final repository = _DelayedFinancialRepository();
      final controller = FinancialDataController(repository);
      addTearDown(controller.dispose);

      final firstLoad = controller.load('farm-1', 'period-1');
      await controller.load('farm-1', 'period-2');
      expect(controller.summary?.cropPeriodId, 'period-2');

      repository.firstSummary.complete(_summary('period-1'));
      await firstLoad;

      expect(controller.summary?.cropPeriodId, 'period-2');
      expect(controller.isLoading, isFalse);
    },
  );
}

FinancialSummaryDto _summary(String periodId) => FinancialSummaryDto.fromJson({
  'farm_id': 'farm-1',
  'farm_name': 'Kuzey Tarlası',
  'crop_period_id': periodId,
  'crop_name': periodId == 'period-1' ? 'Buğday' : 'Nohut',
  'season_year': 2026,
  'planted_at': '2026-03-01',
  'total_expense': 0,
  'total_revenue': 0,
  'registered_difference': 0,
  'has_expense_records': false,
  'has_revenue_records': false,
  'expense_breakdown': <Object>[],
  'recent_sales': <Object>[],
  'warnings': <Object>[],
  'area_in_decares': 10,
  'total_quantity_sold': 0,
  'quantity_unit': null,
  'is_area_defined': true,
});

class _DelayedFinancialRepository implements FinancialRepository {
  final firstSummary = Completer<FinancialSummaryDto>();

  @override
  Future<FinancialSummaryDto> getFinancialSummary(
    String farmId,
    String cropPeriodId,
  ) => cropPeriodId == 'period-1'
      ? firstSummary.future
      : Future.value(_summary(cropPeriodId));

  @override
  Future<List<ExpenseDto>> listExpenses(
    String farmId,
    String cropPeriodId,
  ) async => const [];

  @override
  Future<List<CropSaleDto>> listSales(
    String farmId,
    String cropPeriodId,
  ) async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

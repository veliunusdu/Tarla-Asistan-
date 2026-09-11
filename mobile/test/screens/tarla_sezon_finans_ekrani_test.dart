import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/finances/data/crop_period_source.dart';
import 'package:mobile/features/finances/data/finance_repository.dart';
import 'package:mobile/features/finances/domain/finance_models.dart';
import 'package:mobile/screens/tarla_sezon_finans_ekrani.dart';

void main() {
  testWidgets('new sale is saved to the period selected on screen', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1000, 1600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final repository = _RecordingFinancialRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: TarlaSezonFinansEkrani(
          farmId: 'farm-1',
          cropPeriodId: 'period-1',
          farmName: 'Kuzey Tarlası',
          repository: repository,
          periodSource: const _TwoPeriodSource(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final selector = tester.widget<DropdownButton<String>>(
      find.byType(DropdownButton<String>),
    );
    selector.onChanged?.call('period-2');
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Satış Ekle'));
    await tester.tap(find.text('Satış Ekle'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byWidgetPredicate(
        (widget) =>
            widget is TextField && widget.decoration?.labelText == 'Miktar',
      ),
      '100',
    );
    await tester.enterText(
      find.byWidgetPredicate(
        (widget) =>
            widget is TextField &&
            widget.decoration?.labelText == 'Birim fiyat (TL)',
      ),
      '12,50',
    );
    await tester.tap(find.text('Kaydet'));
    await tester.pumpAndSettle();

    expect(repository.createdSalePeriodId, 'period-2');
  });
}

class _TwoPeriodSource implements FinanceCropPeriodSource {
  const _TwoPeriodSource();

  @override
  Future<List<FinanceCropPeriod>> listCropPeriods(String farmId) async => [
    FinanceCropPeriod(
      id: 'period-1',
      cropName: 'Buğday',
      plantedAt: DateTime(2025, 3, 1),
      status: 'COMPLETED',
    ),
    FinanceCropPeriod(
      id: 'period-2',
      cropName: 'Nohut',
      plantedAt: DateTime(2026, 3, 1),
      status: 'ACTIVE',
    ),
  ];
}

class _RecordingFinancialRepository implements FinancialRepository {
  String? createdSalePeriodId;

  @override
  Future<FinancialSummaryDto> getFinancialSummary(
    String farmId,
    String cropPeriodId,
  ) async => FinancialSummaryDto.fromJson({
    'farm_id': farmId,
    'farm_name': 'Kuzey Tarlası',
    'crop_period_id': cropPeriodId,
    'crop_name': cropPeriodId == 'period-1' ? 'Buğday' : 'Nohut',
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
  Future<CropSaleDto> createSale(
    String farmId,
    String cropPeriodId, {
    required double harvestQuantity,
    required String unit,
    required double unitPrice,
    required DateOnly soldAt,
    String? buyerOrMarketNote,
    String? clientOperationId,
  }) async {
    createdSalePeriodId = cropPeriodId;
    return CropSaleDto(
      id: 'sale-1',
      farmId: farmId,
      cropPeriodId: cropPeriodId,
      harvestQuantity: harvestQuantity,
      unit: unit,
      unitPrice: unitPrice,
      totalAmount: harvestQuantity * unitPrice,
      soldAt: soldAt,
      createdAtUtc: DateTime.utc(2026, 9, 9),
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

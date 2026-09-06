enum ExpenseCategory {
  seed,
  fertilizer,
  pesticide,
  fuel,
  irrigation,
  labor,
  other,
  unknown,
}

ExpenseCategory parseExpenseCategory(Object? value) {
  switch (value?.toString().toUpperCase()) {
    case 'SEED':
      return ExpenseCategory.seed;
    case 'FERTILIZER':
      return ExpenseCategory.fertilizer;
    case 'PESTICIDE':
      return ExpenseCategory.pesticide;
    case 'FUEL':
      return ExpenseCategory.fuel;
    case 'IRRIGATION':
      return ExpenseCategory.irrigation;
    case 'LABOR':
      return ExpenseCategory.labor;
    case 'OTHER':
      return ExpenseCategory.other;
    default:
      return ExpenseCategory.unknown;
  }
}

String expenseCategoryValue(ExpenseCategory category) => switch (category) {
  ExpenseCategory.seed => 'SEED',
  ExpenseCategory.fertilizer => 'FERTILIZER',
  ExpenseCategory.pesticide => 'PESTICIDE',
  ExpenseCategory.fuel => 'FUEL',
  ExpenseCategory.irrigation => 'IRRIGATION',
  ExpenseCategory.labor => 'LABOR',
  ExpenseCategory.other || ExpenseCategory.unknown => 'OTHER',
};

double _double(Object? value) => (value as num?)?.toDouble() ?? 0;
DateTime _dateTime(Object? value) => DateTime.parse(value.toString()).toUtc();
DateOnly _dateOnly(Object? value) => DateOnly.parse(value.toString());

class DateOnly {
  const DateOnly(this.year, this.month, this.day);
  final int year;
  final int month;
  final int day;
  factory DateOnly.parse(String value) {
    final parts = value.split('-');
    return DateOnly(
      int.parse(parts[0]),
      int.parse(parts[1]),
      int.parse(parts[2]),
    );
  }
  String get iso =>
      '${year.toString().padLeft(4, '0')}-${month.toString().padLeft(2, '0')}-${day.toString().padLeft(2, '0')}';
}

class ExpenseDto {
  const ExpenseDto({
    required this.id,
    required this.farmId,
    required this.cropPeriodId,
    required this.category,
    required this.amount,
    required this.occurredAtUtc,
    this.note,
    required this.isActivityLinked,
    this.activityId,
    required this.createdAtUtc,
  });
  final String id, farmId, cropPeriodId;
  final ExpenseCategory category;
  final double amount;
  final DateTime occurredAtUtc, createdAtUtc;
  final String? note;
  final bool isActivityLinked;
  final String? activityId;
  factory ExpenseDto.fromJson(Map<String, dynamic> json) => ExpenseDto(
    id: json['id'].toString(),
    farmId: json['farm_id'].toString(),
    cropPeriodId: json['crop_period_id'].toString(),
    category: parseExpenseCategory(json['category']),
    amount: _double(json['amount']),
    occurredAtUtc: _dateTime(json['occurred_at_utc']),
    note: json['note']?.toString(),
    isActivityLinked: json['is_activity_linked'] == true,
    activityId: json['activity_id']?.toString(),
    createdAtUtc: _dateTime(json['created_at_utc']),
  );
}

class CropSaleDto {
  const CropSaleDto({
    required this.id,
    required this.farmId,
    required this.cropPeriodId,
    required this.harvestQuantity,
    required this.unit,
    required this.unitPrice,
    required this.totalAmount,
    required this.soldAt,
    this.buyerOrMarketNote,
    required this.createdAtUtc,
  });
  final String id, farmId, cropPeriodId, unit;
  final double harvestQuantity, unitPrice, totalAmount;
  final DateOnly soldAt;
  final String? buyerOrMarketNote;
  final DateTime createdAtUtc;
  factory CropSaleDto.fromJson(Map<String, dynamic> json) => CropSaleDto(
    id: json['id'].toString(),
    farmId: json['farm_id'].toString(),
    cropPeriodId: json['crop_period_id'].toString(),
    harvestQuantity: _double(json['harvest_quantity']),
    unit: json['unit'].toString(),
    unitPrice: _double(json['unit_price']),
    totalAmount: _double(json['total_amount']),
    soldAt: _dateOnly(json['sold_at']),
    buyerOrMarketNote: json['buyer_or_market_note']?.toString(),
    createdAtUtc: _dateTime(json['created_at_utc']),
  );
}

class ExpenseBreakdownDto {
  const ExpenseBreakdownDto(this.category, this.amount, this.percentage);
  final ExpenseCategory category;
  final double amount, percentage;
  factory ExpenseBreakdownDto.fromJson(Map<String, dynamic> json) =>
      ExpenseBreakdownDto(
        parseExpenseCategory(json['category']),
        _double(json['amount']),
        _double(json['percentage']),
      );
}

class CompletenessStatusDto {
  const CompletenessStatusDto({
    required this.hasExpenses,
    required this.hasSales,
    required this.isAreaDefined,
  });
  final bool hasExpenses, hasSales, isAreaDefined;
}

class FinancialSummaryDto {
  const FinancialSummaryDto({
    required this.farmId,
    required this.farmName,
    required this.cropPeriodId,
    required this.cropName,
    this.variety,
    required this.seasonYear,
    required this.plantedAt,
    this.harvestedAt,
    this.areaInDecares,
    required this.totalExpense,
    required this.totalRevenue,
    required this.registeredDifference,
    this.expensePerDecare,
    this.totalQuantitySold,
    this.quantityUnit,
    required this.hasExpenseRecords,
    required this.hasRevenueRecords,
    required this.expenseBreakdown,
    required this.recentSales,
    required this.warnings,
    required this.completenessStatus,
  });
  final String farmId, farmName, cropPeriodId, cropName;
  final String? variety, quantityUnit;
  final int seasonYear;
  final DateOnly plantedAt;
  final DateOnly? harvestedAt;
  final double? areaInDecares, expensePerDecare, totalQuantitySold;
  final double totalExpense, totalRevenue, registeredDifference;
  final bool hasExpenseRecords, hasRevenueRecords;
  final List<ExpenseBreakdownDto> expenseBreakdown;
  final List<CropSaleDto> recentSales;
  final List<String> warnings;
  final CompletenessStatusDto completenessStatus;
  factory FinancialSummaryDto.fromJson(Map<String, dynamic> json) =>
      FinancialSummaryDto(
        farmId: json['farm_id'].toString(),
        farmName: json['farm_name'].toString(),
        cropPeriodId: json['crop_period_id'].toString(),
        cropName: json['crop_name'].toString(),
        variety: json['variety']?.toString(),
        seasonYear: (json['season_year'] as num).toInt(),
        plantedAt: _dateOnly(json['planted_at']),
        harvestedAt: json['harvested_at'] == null
            ? null
            : _dateOnly(json['harvested_at']),
        areaInDecares: json['area_in_decares'] == null
            ? null
            : _double(json['area_in_decares']),
        totalExpense: _double(json['total_expense']),
        totalRevenue: _double(json['total_revenue']),
        registeredDifference: _double(json['registered_difference']),
        expensePerDecare: json['expense_per_decare'] == null
            ? null
            : _double(json['expense_per_decare']),
        totalQuantitySold: json['total_quantity_sold'] == null
            ? null
            : _double(json['total_quantity_sold']),
        quantityUnit: json['quantity_unit']?.toString(),
        hasExpenseRecords: json['has_expense_records'] == true,
        hasRevenueRecords: json['has_revenue_records'] == true,
        expenseBreakdown: (json['expense_breakdown'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (e) => ExpenseBreakdownDto.fromJson(Map<String, dynamic>.from(e)),
            )
            .toList(),
        recentSales: (json['recent_sales'] as List? ?? const [])
            .whereType<Map>()
            .map((e) => CropSaleDto.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
        warnings: (json['warnings'] as List? ?? const [])
            .map((e) => e.toString())
            .toList(),
        completenessStatus: CompletenessStatusDto(
          hasExpenses: json['has_expense_records'] == true,
          hasSales: json['has_revenue_records'] == true,
          isAreaDefined: json['is_area_defined'] == true,
        ),
      );
}

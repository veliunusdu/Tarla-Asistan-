import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/features/finances/data/backend_financial_repository.dart';
import 'package:mobile/features/finances/domain/finance_models.dart';
import 'package:mobile/services/api_client.dart';

void main() {
  test('parses integer and decimal money and nullable mixed-unit totals', () {
    final summary = FinancialSummaryDto.fromJson({
      'farm_id': 'f', 'farm_name': 'Tarla', 'crop_period_id': 'p', 'crop_name': 'Buğday', 'season_year': 2026,
      'planted_at': '2026-01-01', 'total_expense': 100, 'total_revenue': 100.5, 'registered_difference': .5,
      'has_expense_records': true, 'has_revenue_records': true, 'expense_breakdown': [], 'recent_sales': [], 'warnings': [],
      'area_in_decares': null, 'total_quantity_sold': null, 'quantity_unit': null, 'is_area_defined': false,
    });
    expect(summary.totalExpense, 100.0);
    expect(summary.totalRevenue, 100.5);
    expect(summary.totalQuantitySold, isNull);
    expect(summary.plantedAt.iso, '2026-01-01');
  });

  test('sale create sends no total amount and serializes DateOnly', () async {
    late http.Request captured;
    final client = ApiClient(httpClient: MockClient((request) async {
      captured = request;
      return http.Response(jsonEncode({'id':'s','farm_id':'f','crop_period_id':'p','harvest_quantity':100,'unit':'kg','unit_price':8.75,'total_amount':875,'sold_at':'2026-07-20','created_at_utc':'2026-07-20T00:00:00Z'}), 201);
    }), idTokenProvider: () async => 'token');
    final repo = BackendFinancialRepository(apiClient: client);
    await repo.createSale('f', 'p', harvestQuantity: 100, unit: 'kg', unitPrice: 8.75, soldAt: const DateOnly(2026, 7, 20));
    final body = jsonDecode(captured.body) as Map<String, dynamic>;
    expect(body['sold_at'], '2026-07-20');
    expect(body.containsKey('total_amount'), isFalse);
  });

  test('archive accepts empty 204 response', () async {
    final client = ApiClient(httpClient: MockClient((_) async => http.Response('', 204)), idTokenProvider: () async => 'token');
    await BackendFinancialRepository(apiClient: client).archiveExpense('e');
  });

  test('api errors use existing ApiException mapping', () async {
    final client = ApiClient(httpClient: MockClient((_) async => http.Response('{"detail":"not found"}', 404)), idTokenProvider: () async => 'token');
    expect(() => BackendFinancialRepository(apiClient: client).listExpenses('f', 'p'), throwsA(isA<ApiException>()));
  });
}

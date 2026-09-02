import 'farm_summary_model.dart';

abstract interface class FarmSummaryRepository {
  Future<FarmSummaryResponse> getFarmSummary({int upcomingLimit = 5});
}

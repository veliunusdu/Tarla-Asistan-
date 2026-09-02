import '../../../models/faaliyet.dart';
import '../../../models/tarla.dart';
import '../../activities/data/backend_faaliyet_repository.dart';
import 'backend_tarla_repository.dart';
import 'dto/farm_dto.dart';

class FarmWorkSummary {
  const FarmWorkSummary({
    required this.tarla,
    this.nextTask,
    this.lastActivity,
  });

  final Tarla tarla;
  final Faaliyet? nextTask;
  final Faaliyet? lastActivity;
}

class FarmSummaryResponse {
  const FarmSummaryResponse({
    required this.farms,
    required this.upcomingTasks,
  });

  final List<FarmWorkSummary> farms;
  final List<Faaliyet> upcomingTasks;

  factory FarmSummaryResponse.fromJson(Map<String, dynamic> json) {
    final rawFarms = (json['farms'] as List<dynamic>?) ?? [];
    final parsedFarms = rawFarms.map((item) {
      final map = item as Map<String, dynamic>;
      final farmMap = (map['farm'] as Map<String, dynamic>?) ?? {};
      final farmDto = FarmResponseDto.fromJson(farmMap);
      final tarla = BackendTarlaRepository.fromDto(farmDto);

      Faaliyet? nextTask;
      final rawNextTask =
          (map['next_task'] ?? map['nextTask']) as Map<String, dynamic>?;
      if (rawNextTask != null) {
        nextTask = BackendFaaliyetRepository.fromTaskJson(
          rawNextTask,
          fallbackTarlaId: tarla.id,
        );
      }

      Faaliyet? lastActivity;
      final rawLastActivity =
          (map['last_activity'] ?? map['lastActivity'])
              as Map<String, dynamic>?;
      if (rawLastActivity != null) {
        lastActivity = BackendFaaliyetRepository.fromBackendJson(
          rawLastActivity,
          fallbackTarlaId: tarla.id,
        );
      }

      return FarmWorkSummary(
        tarla: tarla,
        nextTask: nextTask,
        lastActivity: lastActivity,
      );
    }).toList();

    final rawUpcoming =
        (json['upcoming_tasks'] ?? json['upcomingTasks']) as List<dynamic>? ??
        [];
    final parsedUpcoming = rawUpcoming.map((item) {
      final map = item as Map<String, dynamic>;
      return BackendFaaliyetRepository.fromTaskJson(
        map,
        fallbackTarlaId: map['farm_id']?.toString() ?? '',
      );
    }).toList();

    return FarmSummaryResponse(
      farms: parsedFarms,
      upcomingTasks: parsedUpcoming,
    );
  }
}

using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Application.Features.Tasks.DTOs;

namespace TarlaAsistani.Application.Features.Farms.DTOs;

public record FarmWorkSummaryDto(
    FarmDto Farm,
    TaskDto? NextTask,
    ActivityDto? LastActivity
);

public record FarmSummaryResponse(
    List<FarmWorkSummaryDto> Farms,
    List<TaskDto> UpcomingTasks
);

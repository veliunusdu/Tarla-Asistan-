using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Farms.Queries;

public class GetFarmSummaryQueryHandler : IRequestHandler<GetFarmSummaryQuery, FarmSummaryResponse>
{
    private readonly IApplicationDbContext _db;

    public GetFarmSummaryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FarmSummaryResponse> Handle(GetFarmSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch user's active farms (1 query)
        var farmQuery = _db.Farms.AsNoTracking();

        if (request.Role == UserRole.Farmer)
        {
            farmQuery = farmQuery.Where(f => f.OwnerId == request.UserId);
        }

        farmQuery = farmQuery.Where(f => f.ArchivedAt == null);

        var farms = await farmQuery
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new FarmDto(
                f.Id,
                f.OwnerId,
                f.Name,
                f.Latitude,
                f.Longitude,
                f.SizeInHectares,
                f.IrrigationMethod,
                f.SoilType,
                f.Note,
                f.ArchivedAt,
                f.CreatedAtUtc,
                f.UpdatedAtUtc,
                f.CropPeriods
                    .Where(cp => cp.Status == CropPeriodStatus.Active)
                    .Select(cp => new CropPeriodDto(
                        cp.Id,
                        cp.FarmId,
                        cp.CropName,
                        cp.CropType,
                        cp.Variety,
                        cp.PlantedAt,
                        cp.HarvestedAt,
                        cp.Status,
                        cp.CreatedAtUtc,
                        cp.UpdatedAtUtc
                    )).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);

        if (farms.Count == 0)
        {
            return new FarmSummaryResponse(new List<FarmWorkSummaryDto>(), new List<TaskDto>());
        }

        var farmIds = farms.Select(f => f.Id).ToList();

        // 2. Fetch global upcoming tasks bounded by UpcomingLimit (Query 2)
        var upcomingTasks = await _db.FarmTasks
            .AsNoTracking()
            .Where(t => farmIds.Contains(t.FarmId) &&
                        t.Status != TaskStatus.Completed &&
                        t.Status != TaskStatus.Cancelled &&
                        t.Status != TaskStatus.NotApplied)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .Take(request.UpcomingLimit)
            .Select(t => TaskDto.FromEntity(t))
            .ToListAsync(cancellationToken);

        // 3. Fetch earliest open task per farm (Query 3)
        var nextTasks = await _db.FarmTasks
            .AsNoTracking()
            .Where(t => farmIds.Contains(t.FarmId) &&
                        t.Status != TaskStatus.Completed &&
                        t.Status != TaskStatus.Cancelled &&
                        t.Status != TaskStatus.NotApplied)
            .GroupBy(t => t.FarmId)
            .Select(g => g
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.CreatedAtUtc)
                .ThenBy(t => t.Id)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        var nextTaskByFarm = nextTasks
            .Where(t => t != null)
            .ToDictionary(t => t!.FarmId, t => TaskDto.FromEntity(t!));

        // 4. Fetch latest confirmed non-archived activity per farm (Query 4)
        var latestActivities = await _db.Activities
            .AsNoTracking()
            .Where(a => farmIds.Contains(a.FarmId) &&
                        a.ArchivedAtUtc == null &&
                        a.Status == ActivityStatus.Confirmed)
            .GroupBy(a => a.FarmId)
            .Select(g => g
                .OrderByDescending(a => a.OccurredAtUtc)
                .ThenByDescending(a => a.CreatedAtUtc)
                .ThenByDescending(a => a.Id)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        var lastActivityByFarm = latestActivities
            .Where(a => a != null)
            .ToDictionary(a => a!.FarmId, a => ActivityDto.FromEntity(a!));

        // 5. Assemble summaries per farm
        var summaries = farms.Select(farm => new FarmWorkSummaryDto(
            farm,
            nextTaskByFarm.GetValueOrDefault(farm.Id),
            lastActivityByFarm.GetValueOrDefault(farm.Id)
        )).ToList();

        return new FarmSummaryResponse(summaries, upcomingTasks);
    }
}

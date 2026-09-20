using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Queries;

public record ListExpertTasksQuery(
    Guid ExpertId,
    UserRole Role,
    string? Scope = "mine",
    TaskStatus? Status = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    bool? Overdue = null,
    Guid? FarmId = null,
    Guid? CaseId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<ExpertTaskListDto>;

public record ExpertTaskListDto(
    IReadOnlyList<TaskDto> Items,
    int Total,
    int Page,
    int PageSize
);

public class ListExpertTasksQueryHandler : IRequestHandler<ListExpertTasksQuery, ExpertTaskListDto>
{
    private static readonly TaskStatus[] TerminalStatuses = [TaskStatus.Completed, TaskStatus.NotApplied, TaskStatus.Cancelled];
    private readonly IApplicationDbContext _db;

    public ListExpertTasksQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ExpertTaskListDto> Handle(ListExpertTasksQuery request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist && request.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Yalnızca ziraatçi veya yönetici uzman görev kutusunu sorgulayabilir.");
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var query = _db.FarmTasks
            .Include(t => t.Farm)
            .Where(t => t.Farm.ArchivedAt == null);

        // Scope filter: "mine" vs others
        if (request.Role == UserRole.Agronomist)
        {
            // Default is "mine": tasks created by this expert or linked to a case assigned to this expert
            query = query.Where(t =>
                t.CreatedById == request.ExpertId ||
                (t.CaseId != null && _db.SupportCases.Any(c => c.Id == t.CaseId && c.AssignedExpertId == request.ExpertId)));
        }

        // Filters
        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        if (request.DueFrom.HasValue)
        {
            query = query.Where(t => t.DueDate >= request.DueFrom.Value);
        }

        if (request.DueTo.HasValue)
        {
            query = query.Where(t => t.DueDate <= request.DueTo.Value);
        }

        if (request.Overdue == true)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(t => t.DueDate < today && !TerminalStatuses.Contains(t.Status));
        }

        if (request.FarmId.HasValue)
        {
            query = query.Where(t => t.FarmId == request.FarmId.Value);
        }

        if (request.CaseId.HasValue)
        {
            query = query.Where(t => t.CaseId == request.CaseId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        // Stable sorting: DueDate ASC, CreatedAtUtc DESC, Id ASC
        var tasks = await query
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = tasks.Select(TaskDto.FromEntity).ToList();

        return new ExpertTaskListDto(items, total, page, pageSize);
    }
}

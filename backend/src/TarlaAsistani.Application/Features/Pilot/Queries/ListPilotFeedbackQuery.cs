using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Pilot.Queries;

public record ListPilotFeedbackQuery(
    Guid UserId,
    UserRole Role,
    FeedbackType? FeedbackType = null,
    FeedbackStatus? Status = null,
    int Limit = 50,
    int Offset = 0
) : IRequest<PilotFeedbackListDto>;

public class ListPilotFeedbackQueryHandler : IRequestHandler<ListPilotFeedbackQuery, PilotFeedbackListDto>
{
    private readonly IApplicationDbContext _db;

    public ListPilotFeedbackQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PilotFeedbackListDto> Handle(ListPilotFeedbackQuery request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist)
        {
            throw new UnauthorizedAccessException("Yalnızca uzman geri bildirimleri listeleyebilir.");
        }

        var query = _db.PilotFeedbacks
            .Include(pf => pf.CreatedBy)
                .ThenInclude(u => u.Profile)
            .AsQueryable();

        if (request.FeedbackType.HasValue)
        {
            query = query.Where(pf => pf.FeedbackType == request.FeedbackType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(pf => pf.Status == request.Status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var limit = Math.Clamp(request.Limit, 1, 100);
        var offset = Math.Max(request.Offset, 0);

        var items = await query
            .OrderByDescending(pf => pf.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .Select(pf => PilotFeedbackDto.FromEntity(pf))
            .ToListAsync(cancellationToken);

        return new PilotFeedbackListDto(items, total, limit, offset);
    }
}

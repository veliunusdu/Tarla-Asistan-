using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Pilot.Commands;

public record UpdatePilotFeedbackStatusCommand(
    Guid FeedbackId,
    Guid ReviewerId,
    UserRole Role,
    FeedbackStatus Status
) : IRequest<PilotFeedbackDto?>;

public class UpdatePilotFeedbackStatusCommandHandler : IRequestHandler<UpdatePilotFeedbackStatusCommand, PilotFeedbackDto?>
{
    private readonly IApplicationDbContext _db;

    public UpdatePilotFeedbackStatusCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PilotFeedbackDto?> Handle(UpdatePilotFeedbackStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist)
        {
            throw new UnauthorizedAccessException("Yalnızca uzman geri bildirim durumunu güncelleyebilir.");
        }

        var feedback = await _db.PilotFeedbacks
            .Include(pf => pf.CreatedBy)
                .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(pf => pf.Id == request.FeedbackId, cancellationToken);

        if (feedback == null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        feedback.Status = request.Status;
        feedback.ReviewedById = request.ReviewerId;
        feedback.ReviewedAtUtc = now;
        feedback.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return PilotFeedbackDto.FromEntity(feedback);
    }
}

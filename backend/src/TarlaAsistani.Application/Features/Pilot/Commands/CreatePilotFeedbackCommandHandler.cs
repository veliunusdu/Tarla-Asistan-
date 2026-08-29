using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Pilot.Commands;

public class CreatePilotFeedbackCommandHandler : IRequestHandler<CreatePilotFeedbackCommand, PilotFeedbackDto>
{
    private readonly IApplicationDbContext _db;

    public CreatePilotFeedbackCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PilotFeedbackDto> Handle(CreatePilotFeedbackCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify User existence
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == request.CreatedById, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        // 2. Validate related task if provided
        if (request.RelatedTaskId.HasValue)
        {
            var taskQuery = _db.FarmTasks
                .Include(t => t.Farm)
                .Where(t => t.Id == request.RelatedTaskId.Value && t.Farm.ArchivedAt == null);

            if (request.Role == UserRole.Farmer)
            {
                taskQuery = taskQuery.Where(t => t.Farm.OwnerId == request.CreatedById);
            }

            var task = await taskQuery.FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Görev bulunamadı.");

            if (request.FeedbackType == FeedbackType.FalseAlert && task.Source != TaskSource.Weather)
            {
                throw new ArgumentException("Yanlış uyarı kaydı yalnızca hava uyarısı görevi için açılabilir.");
            }
        }

        // 3. Validate related case if provided
        if (request.RelatedCaseId.HasValue)
        {
            var caseQuery = _db.SupportCases
                .Include(sc => sc.Farm)
                .Where(sc => sc.Id == request.RelatedCaseId.Value && sc.Farm.ArchivedAt == null);

            if (request.Role == UserRole.Farmer)
            {
                caseQuery = caseQuery.Where(sc => sc.Farm.OwnerId == request.CreatedById);
            }

            var caseExists = await caseQuery.AnyAsync(cancellationToken);
            if (!caseExists)
            {
                throw new KeyNotFoundException("Vaka bulunamadı.");
            }
        }

        // 4. Create PilotFeedback
        var now = DateTime.UtcNow;
        var feedback = new PilotFeedback
        {
            CreatedById = user.Id,
            FeedbackType = request.FeedbackType,
            Status = FeedbackStatus.Open,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            RelatedTaskId = request.RelatedTaskId,
            RelatedCaseId = request.RelatedCaseId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PilotFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(cancellationToken);

        feedback.CreatedBy = user;
        return PilotFeedbackDto.FromEntity(feedback);
    }
}

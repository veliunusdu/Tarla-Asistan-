using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class UpdateCaseStatusCommandHandler : IRequestHandler<UpdateCaseStatusCommand, CaseDetailDto?>
{
    private static readonly Dictionary<CaseStatus, HashSet<CaseStatus>> ExpertTransitions = new()
    {
        [CaseStatus.Open] = [CaseStatus.InReview, CaseStatus.WaitingFarmer, CaseStatus.Answered, CaseStatus.Closed],
        [CaseStatus.InReview] = [CaseStatus.WaitingFarmer, CaseStatus.Answered, CaseStatus.Closed],
        [CaseStatus.WaitingFarmer] = [CaseStatus.InReview, CaseStatus.Answered, CaseStatus.Closed],
        [CaseStatus.Answered] = [CaseStatus.InReview, CaseStatus.Closed],
        [CaseStatus.Closed] = [CaseStatus.InReview]
    };

    private readonly IApplicationDbContext _db;

    public UpdateCaseStatusCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseDetailDto?> Handle(UpdateCaseStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist)
        {
            throw new UnauthorizedAccessException("Yalnızca uzman vaka durumunu güncelleyebilir.");
        }

        var supportCase = await _db.SupportCases
            .Include(sc => sc.Farm)
            .Include(sc => sc.MediaLinks)
                .ThenInclude(ml => ml.Media)
            .Include(sc => sc.Messages)
                .ThenInclude(m => m.MediaLinks)
                    .ThenInclude(ml => ml.Media)
            .FirstOrDefaultAsync(sc => sc.Id == request.CaseId && sc.Farm.ArchivedAt == null, cancellationToken);

        if (supportCase == null)
        {
            return null;
        }

        if (request.Status != supportCase.Status)
        {
            if (!ExpertTransitions.TryGetValue(supportCase.Status, out var allowed) || !allowed.Contains(request.Status))
            {
                throw new InvalidOperationException("Geçersiz vaka durum geçişi.");
            }
        }

        var now = DateTime.UtcNow;

        if (request.AssignToMe)
        {
            TarlaAsistani.Application.Features.Cases.Services.CaseClaimWorkflow.ApplyClaim(
                supportCase, request.UserId, _db, "Claimed via status update");
        }
        else
        {
            if (!supportCase.AssignedExpertId.HasValue)
            {
                throw new InvalidOperationException("Vaka durumu güncellenmeden önce üstlenilmelidir.");
            }

            if (supportCase.AssignedExpertId.Value != request.UserId)
            {
                throw new UnauthorizedAccessException("Yalnızca vakayı üstlenen uzman durumunu güncelleyebilir.");
            }
        }

        supportCase.Status = request.Status;
        if (request.Priority.HasValue)
        {
            supportCase.Priority = request.Priority.Value;
        }

        supportCase.ClosedAtUtc = request.Status == CaseStatus.Closed ? now : null;
        supportCase.UpdatedAtUtc = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await TarlaAsistani.Application.Features.Cases.Services.CaseClaimWorkflow.HandleConcurrencyConflictAsync(_db, request.CaseId, request.UserId, cancellationToken);
            return CaseDetailDto.FromEntity(fresh);
        }

        return CaseDetailDto.FromEntity(supportCase);
    }
}

using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.Cases.Services;

public static class CaseClaimWorkflow
{
    public static void ApplyClaim(SupportCase supportCase, Guid expertId, IApplicationDbContext db, string reason)
    {
        if (supportCase.Status == CaseStatus.Closed)
        {
            throw new ConflictException("CASE_CLOSED", "Kapalı vakalar üstlenilemez.");
        }

        // Conflict: already claimed by another expert
        if (supportCase.AssignedExpertId.HasValue && supportCase.AssignedExpertId.Value != expertId)
        {
            throw new ConflictException("CASE_ALREADY_ASSIGNED", "Bu vaka başka bir uzman tarafından üstlenilmiştir.");
        }

        // Idempotent: already claimed by this expert
        if (supportCase.AssignedExpertId.HasValue && supportCase.AssignedExpertId.Value == expertId)
        {
            return;
        }

        var previousExpert = supportCase.AssignedExpertId;
        supportCase.AssignedExpertId = expertId;
        if (supportCase.Status == CaseStatus.Open)
        {
            supportCase.Status = CaseStatus.InReview;
        }
        supportCase.UpdatedAtUtc = DateTime.UtcNow;

        var assignmentEvent = new CaseAssignmentEvent
        {
            Id = Guid.NewGuid(),
            CaseId = supportCase.Id,
            PreviousExpertId = previousExpert,
            NewExpertId = expertId,
            ChangedByUserId = expertId,
            ChangeType = CaseAssignmentChangeType.Claim,
            Reason = reason,
            ChangedAtUtc = DateTime.UtcNow
        };
        db.CaseAssignmentEvents.Add(assignmentEvent);
    }

    public static async Task<SupportCase> HandleConcurrencyConflictAsync(
        IApplicationDbContext db,
        Guid caseId,
        Guid expertId,
        CancellationToken ct)
    {
        var currentCase = await db.SupportCases
            .AsNoTracking()
            .Include(c => c.Farm)
            .Include(c => c.MediaLinks).ThenInclude(ml => ml.Media)
            .Include(c => c.Messages).ThenInclude(m => m.MediaLinks).ThenInclude(ml => ml.Media)
            .Include(c => c.Context)
            .FirstOrDefaultAsync(c => c.Id == caseId, ct);

        if (currentCase == null)
        {
            throw new KeyNotFoundException("Vaka bulunamadı.");
        }

        if (currentCase.Status == CaseStatus.Closed)
        {
            throw new ConflictException("CASE_CLOSED", "Kapalı vakalar üstlenilemez.");
        }

        if (currentCase.AssignedExpertId == expertId)
        {
            return currentCase;
        }

        throw new ConflictException("CASE_ALREADY_ASSIGNED", "Bu vaka başka bir uzman tarafından üstlenilmiştir.");
    }
}

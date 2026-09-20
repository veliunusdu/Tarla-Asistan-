using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record FirebaseLinkApprovalDto(
    Guid Id,
    Guid UserId,
    string FirebaseUid,
    string ApprovedBy,
    DateTime ApprovedAtUtc,
    DateTime ExpiresAtUtc,
    Guid? ApprovedByUserId = null
);

public record ApproveFirebaseLinkCommand(
    Guid UserId,
    string FirebaseUid,
    Guid ApprovedByUserId
) : IRequest<FirebaseLinkApprovalDto>;

public class ApproveFirebaseLinkCommandValidator : AbstractValidator<ApproveFirebaseLinkCommand>
{
    public ApproveFirebaseLinkCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kullanıcı kimliği boş olamaz.");
        RuleFor(x => x.FirebaseUid).NotEmpty().MaximumLength(128).WithMessage("Geçersiz Firebase UID.");
        RuleFor(x => x.ApprovedByUserId).NotEmpty().WithMessage("Operatör kullanıcı kimliği gereklidir.");
    }
}

public class ApproveFirebaseLinkCommandHandler : IRequestHandler<ApproveFirebaseLinkCommand, FirebaseLinkApprovalDto>
{
    private readonly IApplicationDbContext _db;

    public ApproveFirebaseLinkCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FirebaseLinkApprovalDto> Handle(ApproveFirebaseLinkCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // 1. Verify user exists and is an active Agronomist without existing FirebaseUid
        var user = await _db.Users
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var isAgronomist = await _db.UserRoleAssignments
            .AnyAsync(ra => ra.UserId == user.Id && ra.Role == UserRole.Agronomist && ra.RevokedAtUtc == null, cancellationToken)
            || user.RoleAssignments.Any(ra => ra.Role == UserRole.Agronomist && ra.RevokedAtUtc == null);

        if (!isAgronomist)
        {
            throw new InvalidOperationException("Yalnızca uzman / ziraat mühendisi hesapları onay gerektirir.");
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            throw new InvalidOperationException("Kullanıcı hesabı aktif değil.");
        }

        if (!string.IsNullOrEmpty(user.FirebaseUid))
        {
            throw new InvalidOperationException("Bu kullanıcıya ait bir Firebase hesabı zaten bağlı.");
        }

        // 2. Verify target FirebaseUid is not already linked to someone else
        var existingLinkedUser = await _db.Users
            .AnyAsync(u => u.FirebaseUid == request.FirebaseUid, cancellationToken);

        if (existingLinkedUser)
        {
            throw new InvalidOperationException("Bu Firebase kimliği zaten başka bir kullanıcıya bağlı.");
        }

        // 2.1 Invalidate any expired approvals for this user or UID so they do not block unique indexes
        var expiredApprovals = await _db.FirebaseLinkApprovals
            .Where(a => (a.UserId == request.UserId || a.FirebaseUid == request.FirebaseUid) &&
                        a.ConsumedAtUtc == null &&
                        a.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var exp in expiredApprovals)
        {
            exp.ConsumedAtUtc = now;
        }

        // 3. Check for existing active approval (Idempotency: return existing if same user and UID)
        var existingSameApproval = await _db.FirebaseLinkApprovals
            .FirstOrDefaultAsync(a => a.UserId == request.UserId &&
                                      a.FirebaseUid == request.FirebaseUid &&
                                      a.ConsumedAtUtc == null &&
                                      a.ExpiresAtUtc > now, cancellationToken);

        if (existingSameApproval != null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new FirebaseLinkApprovalDto(
                existingSameApproval.Id,
                existingSameApproval.UserId,
                existingSameApproval.FirebaseUid,
                existingSameApproval.ApprovedBy,
                existingSameApproval.ApprovedAtUtc,
                existingSameApproval.ExpiresAtUtc,
                existingSameApproval.ApprovedByUserId
            );
        }

        // Verify no conflicting active approval exists
        var conflictApproval = await _db.FirebaseLinkApprovals
            .AnyAsync(a => (a.UserId == request.UserId || a.FirebaseUid == request.FirebaseUid) &&
                           a.ConsumedAtUtc == null &&
                           a.ExpiresAtUtc > now, cancellationToken);

        if (conflictApproval)
        {
            throw new InvalidOperationException("Bu kullanıcı veya Firebase kimliği için halihazırda geçerli bir onay bulunmaktadır.");
        }

        // 4. Resolve operator name server-side from admin user
        var adminUser = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == request.ApprovedByUserId, cancellationToken);

        var operatorName = adminUser?.Profile?.FullName ?? adminUser?.PhoneNumber ?? request.ApprovedByUserId.ToString();

        // 5. Create new approval token with 24 hours TTL
        var approval = new FirebaseLinkApproval
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            FirebaseUid = request.FirebaseUid.Trim(),
            ApprovedBy = operatorName,
            ApprovedByUserId = request.ApprovedByUserId,
            ApprovedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24),
            ConsumedAtUtc = null,
            CreatedAtUtc = now
        };

        _db.FirebaseLinkApprovals.Add(approval);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existing = await _db.FirebaseLinkApprovals
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == request.UserId &&
                                          a.FirebaseUid == request.FirebaseUid &&
                                          a.ConsumedAtUtc == null &&
                                          a.ExpiresAtUtc > now, cancellationToken);
            if (existing != null)
            {
                return new FirebaseLinkApprovalDto(
                    existing.Id,
                    existing.UserId,
                    existing.FirebaseUid,
                    existing.ApprovedBy,
                    existing.ApprovedAtUtc,
                    existing.ExpiresAtUtc,
                    existing.ApprovedByUserId
                );
            }
            throw;
        }

        return new FirebaseLinkApprovalDto(
            approval.Id,
            approval.UserId,
            approval.FirebaseUid,
            approval.ApprovedBy,
            approval.ApprovedAtUtc,
            approval.ExpiresAtUtc,
            approval.ApprovedByUserId
        );
    }
}

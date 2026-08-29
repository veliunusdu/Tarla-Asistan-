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
    DateTime ExpiresAtUtc
);

public record ApproveFirebaseLinkCommand(
    Guid UserId,
    string FirebaseUid,
    string OperatorName
) : IRequest<FirebaseLinkApprovalDto>;

public class ApproveFirebaseLinkCommandValidator : AbstractValidator<ApproveFirebaseLinkCommand>
{
    public ApproveFirebaseLinkCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kullanıcı kimliği boş olamaz.");
        RuleFor(x => x.FirebaseUid).NotEmpty().MaximumLength(128).WithMessage("Geçersiz Firebase UID.");
        RuleFor(x => x.OperatorName).NotEmpty().MaximumLength(120).WithMessage("Operatör adı gereklidir.");
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
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        if (user.Role != UserRole.Agronomist)
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

        // 3. Verify no active (unconsumed & non-expired) approval already exists
        var existingActiveApproval = await _db.FirebaseLinkApprovals
            .AnyAsync(a => (a.UserId == request.UserId || a.FirebaseUid == request.FirebaseUid) &&
                           a.ConsumedAtUtc == null &&
                           a.ExpiresAtUtc > now, cancellationToken);

        if (existingActiveApproval)
        {
            throw new InvalidOperationException("Bu kullanıcı veya Firebase kimliği için halihazırda geçerli bir onay bulunmaktadır.");
        }

        // 4. Create new approval token with 24 hours TTL
        var approval = new FirebaseLinkApproval
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            FirebaseUid = request.FirebaseUid.Trim(),
            ApprovedBy = request.OperatorName.Trim(),
            ApprovedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24),
            ConsumedAtUtc = null,
            CreatedAtUtc = now
        };

        _db.FirebaseLinkApprovals.Add(approval);
        await _db.SaveChangesAsync(cancellationToken);

        return new FirebaseLinkApprovalDto(
            approval.Id,
            approval.UserId,
            approval.FirebaseUid,
            approval.ApprovedBy,
            approval.ApprovedAtUtc,
            approval.ExpiresAtUtc
        );
    }
}

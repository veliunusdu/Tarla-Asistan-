using System.Collections.Concurrent;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Common.Utilities;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Users.Commands;

public record RequestAccountDeletionCommand(Guid UserId, string Confirmation) : IRequest<AccountDeletionResponseDto>;

public class RequestAccountDeletionCommandValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Confirmation)
            .Equal("HESABIMI SIL")
            .WithMessage("Hesap silme onay metni eşleşmiyor.");
    }
}

public class RequestAccountDeletionCommandHandler : IRequestHandler<RequestAccountDeletionCommand, AccountDeletionResponseDto>
{
    private static readonly KeyedLockManager<Guid> UserLocks = new();
    private readonly IApplicationDbContext _db;

    public RequestAccountDeletionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AccountDeletionResponseDto> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        using (await UserLocks.AcquireAsync(request.UserId, cancellationToken))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        user.AccountStatus = AccountStatus.DeletionPending;
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke all active refresh tokens for the user
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        // Idempotency: Check if an active deletion job already exists
        var existingJob = await _db.AccountDeletionJobs
            .FirstOrDefaultAsync(j => j.UserId == user.Id &&
                                     (j.Status == AccountDeletionStatus.Pending ||
                                      j.Status == AccountDeletionStatus.Processing ||
                                      j.Status == AccountDeletionStatus.RetryRequired),
                                 cancellationToken);

        if (existingJob != null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new AccountDeletionResponseDto(existingJob.Id, existingJob.Status.ToString().ToUpperInvariant());
        }

        var job = new AccountDeletionJob
        {
            UserId = user.Id,
            FirebaseUidSnapshot = user.FirebaseUid,
            Status = AccountDeletionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.AccountDeletionJobs.Add(job);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrentJob = await _db.AccountDeletionJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.UserId == user.Id &&
                                         (j.Status == AccountDeletionStatus.Pending ||
                                          j.Status == AccountDeletionStatus.Processing ||
                                          j.Status == AccountDeletionStatus.RetryRequired),
                                     cancellationToken);
            if (concurrentJob != null)
            {
                return new AccountDeletionResponseDto(concurrentJob.Id, concurrentJob.Status.ToString().ToUpperInvariant());
            }
            throw;
        }

            return new AccountDeletionResponseDto(job.Id, job.Status.ToString().ToUpperInvariant());
        }
    }
}

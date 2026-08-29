using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
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
    private readonly IApplicationDbContext _db;

    public RequestAccountDeletionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AccountDeletionResponseDto> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        user.AccountStatus = AccountStatus.DeletionPending;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var job = new AccountDeletionJob
        {
            UserId = user.Id,
            FirebaseUidSnapshot = user.FirebaseUid,
            Status = AccountDeletionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.AccountDeletionJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return new AccountDeletionResponseDto(job.Id, job.Status.ToString().ToUpperInvariant());
    }
}

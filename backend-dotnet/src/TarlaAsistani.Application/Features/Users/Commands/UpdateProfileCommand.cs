using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Users.Commands;

public record UpdateProfileCommand(
    Guid UserId,
    string FullName,
    string Province,
    string District,
    bool TermsAccepted,
    bool NotificationsEnabled
) : IRequest<UserDto?>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Province).NotEmpty().MinimumLength(2).MaximumLength(80);
        RuleFor(x => x.District).NotEmpty().MinimumLength(2).MaximumLength(80);
        RuleFor(x => x.TermsAccepted).Equal(true).WithMessage("Kullanım koşulları kabul edilmelidir.");
    }
}

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserDto?>
{
    private readonly IApplicationDbContext _db;

    public UpdateProfileCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserDto?> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null) return null;

        var now = DateTime.UtcNow;
        if (user.Profile == null)
        {
            user.Profile = new Profile
            {
                UserId = user.Id,
                FullName = request.FullName.Trim(),
                Province = request.Province.Trim(),
                District = request.District.Trim(),
                TermsAccepted = request.TermsAccepted,
                NotificationsEnabled = request.NotificationsEnabled,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.Profiles.Add(user.Profile);
        }
        else
        {
            user.Profile.FullName = request.FullName.Trim();
            user.Profile.Province = request.Province.Trim();
            user.Profile.District = request.District.Trim();
            user.Profile.TermsAccepted = request.TermsAccepted;
            user.Profile.NotificationsEnabled = request.NotificationsEnabled;
            user.Profile.UpdatedAtUtc = now;
        }

        user.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return UserDto.FromEntity(user);
    }
}

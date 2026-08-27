using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record FirebaseLoginCommand(
    string IdToken,
    UserRole? Role = null
) : IRequest<TokenResponseDto>;

public class FirebaseLoginCommandValidator : AbstractValidator<FirebaseLoginCommand>
{
    public FirebaseLoginCommandValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Firebase kimlik belirteci boş olamaz.");
    }
}

public class FirebaseLoginCommandHandler : IRequestHandler<FirebaseLoginCommand, TokenResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFirebaseAuthService _firebaseAuth;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _config;

    public FirebaseLoginCommandHandler(
        IApplicationDbContext db,
        IFirebaseAuthService firebaseAuth,
        IJwtService jwtService,
        IConfiguration config)
    {
        _db = db;
        _firebaseAuth = firebaseAuth;
        _jwtService = jwtService;
        _config = config;
    }

    public async Task<TokenResponseDto> Handle(FirebaseLoginCommand request, CancellationToken cancellationToken)
    {
        var tokenInfo = await _firebaseAuth.VerifyIdTokenAsync(request.IdToken, cancellationToken);
        if (tokenInfo == null || string.IsNullOrWhiteSpace(tokenInfo.Uid))
        {
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş Firebase kimlik doğrulama belirteci.");
        }

        var now = DateTime.UtcNow;

        // 1. Locate existing user by FirebaseUid or PhoneNumber
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.FirebaseUid == tokenInfo.Uid ||
                                     (!string.IsNullOrEmpty(tokenInfo.PhoneNumber) && u.PhoneNumber == tokenInfo.PhoneNumber),
                                 cancellationToken);

        if (user == null)
        {
            // Auto-register user: new self-registered Firebase users are always Farmer
            user = new User
            {
                PhoneNumber = tokenInfo.PhoneNumber ?? string.Empty,
                FirebaseUid = tokenInfo.Uid,
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            if (!string.IsNullOrWhiteSpace(tokenInfo.DisplayName))
            {
                user.Profile = new Profile
                {
                    UserId = user.Id,
                    FullName = tokenInfo.DisplayName.Trim(),
                    Province = string.Empty,
                    District = string.Empty,
                    TermsAccepted = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
            }

            _db.Users.Add(user);
        }
        else
        {
            if (user.AccountStatus == AccountStatus.Anonymized)
            {
                throw new InvalidOperationException("Bu hesap silinmiştir.");
            }

            // Link Firebase UID if previously unlinked
            if (string.IsNullOrEmpty(user.FirebaseUid))
            {
                // If user is Agronomist, must have an unconsumed and non-expired approval
                if (user.Role == UserRole.Agronomist)
                {
                    var approval = await _db.FirebaseLinkApprovals
                        .FirstOrDefaultAsync(a => a.UserId == user.Id &&
                                                  a.FirebaseUid == tokenInfo.Uid &&
                                                  a.ConsumedAtUtc == null &&
                                                  a.ExpiresAtUtc > now,
                                             cancellationToken);

                    if (approval == null)
                    {
                        throw new UnauthorizedAccessException("Ziraat mühendisi / uzman hesabı için yönetici onayı gereklidir.");
                    }

                    approval.ConsumedAtUtc = now;
                }

                user.FirebaseUid = tokenInfo.Uid;
            }

            user.AccountStatus = AccountStatus.Active;
            user.UpdatedAtUtc = now;
        }

        // 2. Generate Access & Refresh Tokens
        var accessToken = _jwtService.GenerateAccessToken(user);
        var rawRefreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken))).ToLowerInvariant();

        var expiryDays = _config.GetValue<int>("Auth:RefreshTokenExpiryDays", 30);
        var tokenRecord = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = now.AddDays(expiryDays),
            CreatedAtUtc = now
        };

        _db.RefreshTokens.Add(tokenRecord);
        await _db.SaveChangesAsync(cancellationToken);

        return new TokenResponseDto(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            TokenType: "bearer",
            ExpiresIn: 900,
            User: UserDto.FromEntity(user)
        );
    }
}

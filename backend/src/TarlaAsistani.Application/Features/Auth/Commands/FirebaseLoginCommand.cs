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
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record FirebaseLoginCommand(
    string IdToken,
    UserRole? ActiveRole = null,
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
        var requestedRole = request.ActiveRole ?? request.Role;

        // 1. Locate existing user by FirebaseUid or PhoneNumber
        var user = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(u => u.FirebaseUid == tokenInfo.Uid ||
                                     (!string.IsNullOrEmpty(tokenInfo.PhoneNumber) && u.PhoneNumber == tokenInfo.PhoneNumber),
                                 cancellationToken);

        if (user == null)
        {
            // Auto-register user: new self-registered Firebase users are always Farmer
            user = new User
            {
                PhoneNumber = GetPhoneNumber(tokenInfo),
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

            var initialAssignment = new UserRoleAssignment
            {
                UserId = user.Id,
                Role = UserRole.Farmer,
                GrantedAtUtc = now,
                GrantReason = "Self registration"
            };
            user.RoleAssignments.Add(initialAssignment);
            _db.UserRoleAssignments.Add(initialAssignment);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);

            // New registration cannot be granted privileged session
            if (requestedRole.HasValue && requestedRole.Value != UserRole.Farmer)
            {
                throw new ForbiddenException(requestedRole.Value == UserRole.Agronomist
                    ? "Bu hesap için ziraatçi yetkisi bulunmuyor."
                    : "Bu hesap için yönetici yetkisi bulunmuyor.");
            }
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

        // 2. Fetch active roles
        var activeRoles = await _db.UserRoleAssignments
            .Where(ura => ura.UserId == user.Id && ura.RevokedAtUtc == null)
            .Select(ura => ura.Role)
            .Distinct()
            .ToListAsync(cancellationToken);

        UserRole effectiveActiveRole;
        if (requestedRole.HasValue)
        {
            if (!activeRoles.Contains(requestedRole.Value))
            {
                throw new ForbiddenException(requestedRole.Value == UserRole.Agronomist
                    ? "Bu hesap için ziraatçi yetkisi bulunmuyor."
                    : (requestedRole.Value == UserRole.Admin
                        ? "Bu hesap için yönetici yetkisi bulunmuyor."
                        : "Bu hesap için belirtilen rol yetkisi bulunmuyor."));
            }
            effectiveActiveRole = requestedRole.Value;
        }
        else
        {
            if (activeRoles.Contains(UserRole.Farmer))
            {
                effectiveActiveRole = UserRole.Farmer;
            }
            else if (activeRoles.Count > 0)
            {
                effectiveActiveRole = activeRoles[0];
            }
            else
            {
                throw new ForbiddenException("Bu hesap için aktif bir rol ataması bulunmuyor.");
            }
        }

        // 3. Generate Access & Refresh Tokens with effective active role
        var accessToken = _jwtService.GenerateAccessToken(user, effectiveActiveRole);
        var rawRefreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken))).ToLowerInvariant();

        var expiryDays = _config.GetValue<int>("Auth:RefreshTokenExpiryDays", 30);
        var tokenRecord = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ActiveRole = effectiveActiveRole,
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
            User: UserDto.FromEntity(user, effectiveActiveRole, activeRoles)
        );
    }

    private static string GetPhoneNumber(FirebaseTokenInfo tokenInfo)
    {
        if (!string.IsNullOrWhiteSpace(tokenInfo.PhoneNumber))
        {
            return tokenInfo.PhoneNumber;
        }

        // The legacy schema requires a unique phone value. Email-only Firebase users
        // receive a stable internal identifier derived from their Firebase UID.
        var uidHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenInfo.Uid))).ToLowerInvariant();
        return $"firebase-{uidHash[..48]}";
    }
}

using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, TokenResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _config;

    public VerifyOtpCommandHandler(IApplicationDbContext db, IJwtService jwtService, IConfiguration config)
    {
        _db = db;
        _jwtService = jwtService;
        _config = config;
    }

    public async Task<TokenResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.OtpCode.Trim())));

        var activeOtp = await _db.OtpCodes
            .Where(o => o.PhoneNumber == phone && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeOtp == null || activeOtp.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Kod bulunamadı veya süresi doldu.");
        }

        if (activeOtp.AttemptCount >= 3 || activeOtp.CodeHash != codeHash)
        {
            activeOtp.AttemptCount++;
            await _db.SaveChangesAsync(cancellationToken);
            throw new ArgumentException("Kod geçersiz veya deneme sınırı aşıldı.");
        }

        activeOtp.IsUsed = true;

        // Find or create User
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phone, cancellationToken);

        if (user == null)
        {
            var now = DateTime.UtcNow;
            user = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = phone,
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active,
                IsVerified = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.Users.Add(user);

            var initialAssignment = new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Role = UserRole.Farmer,
                GrantedAtUtc = now,
                GrantReason = "OTP verification initial farmer role"
            };
            user.RoleAssignments.Add(initialAssignment);
            _db.UserRoleAssignments.Add(initialAssignment);

            await _db.SaveChangesAsync(cancellationToken);
        }

        // Issue session tokens based ONLY on active role assignments
        var activeRoles = await _db.UserRoleAssignments
            .Where(ura => ura.UserId == user.Id && ura.RevokedAtUtc == null)
            .Select(ura => ura.Role)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (activeRoles.Count == 0)
        {
            throw new ForbiddenException("Kullanıcının aktif bir rol ataması bulunmamaktadır.");
        }

        var effectiveActiveRole = activeRoles.Contains(UserRole.Farmer)
            ? UserRole.Farmer
            : activeRoles[0];

        var accessToken = _jwtService.GenerateAccessToken(user, effectiveActiveRole);
        var rawRefreshToken = _jwtService.GenerateRefreshToken();
        var refreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken))).ToLowerInvariant();
        var refreshDays = _config.GetValue("Auth:RefreshTokenExpireDays", 30);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = refreshHash,
            ActiveRole = effectiveActiveRole,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new TokenResponseDto(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            TokenType: "bearer",
            ExpiresIn: _config.GetValue("Auth:AccessTokenExpireMinutes", 15) * 60,
            User: UserDto.FromEntity(user, effectiveActiveRole, activeRoles)
        );
    }
}

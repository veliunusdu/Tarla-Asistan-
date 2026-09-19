using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<TokenResponseDto>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _config;

    public RefreshTokenCommandHandler(IApplicationDbContext db, IJwtService jwtService, IConfiguration config)
    {
        _db = db;
        _jwtService = jwtService;
        _config = config;
    }

    public async Task<TokenResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken))).ToLowerInvariant();

        var stored = await _db.RefreshTokens
            .Include(r => r.User)
                .ThenInclude(u => u.Profile)
            .Include(r => r.User)
                .ThenInclude(u => u.RoleAssignments)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (stored == null || stored.RevokedAtUtc != null || stored.ExpiresAtUtc <= DateTime.UtcNow)
        {
            if (stored != null && stored.RevokedAtUtc == null)
            {
                stored.RevokedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
            throw new UnauthorizedAccessException("Refresh oturumu geçersiz veya süresi doldu.");
        }

        // Verify active role is still granted and not revoked
        var isRoleActive = await _db.UserRoleAssignments
            .AnyAsync(ura => ura.UserId == stored.UserId && ura.Role == stored.ActiveRole && ura.RevokedAtUtc == null, cancellationToken);

        if (!isRoleActive)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Oturum rolü artık aktif değil veya yetki geri alındı.");
        }

        // Revoke old token and rotate in same family
        var now = DateTime.UtcNow;
        stored.RevokedAtUtc = now;

        var rawReplacement = _jwtService.GenerateRefreshToken();
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawReplacement))).ToLowerInvariant();
        var refreshDays = _config.GetValue("Auth:RefreshTokenExpireDays", 30);

        var replacement = new RefreshToken
        {
            UserId = stored.UserId,
            FamilyId = stored.FamilyId,
            TokenHash = newHash,
            ActiveRole = stored.ActiveRole,
            ExpiresAtUtc = now.AddDays(refreshDays),
            CreatedAtUtc = now
        };

        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(cancellationToken);

        var activeRoles = await _db.UserRoleAssignments
            .Where(ura => ura.UserId == stored.UserId && ura.RevokedAtUtc == null)
            .Select(ura => ura.Role)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (stored.User == null)
        {
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        }

        return new TokenResponseDto(
            AccessToken: _jwtService.GenerateAccessToken(stored.User, stored.ActiveRole),
            RefreshToken: rawReplacement,
            TokenType: "bearer",
            ExpiresIn: _config.GetValue("Auth:AccessTokenExpireMinutes", 15) * 60,
            User: UserDto.FromEntity(stored.User, stored.ActiveRole, activeRoles)
        );
    }
}

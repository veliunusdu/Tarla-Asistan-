using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<bool>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public LogoutCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAtUtc == null, cancellationToken);
        if (stored != null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }
}

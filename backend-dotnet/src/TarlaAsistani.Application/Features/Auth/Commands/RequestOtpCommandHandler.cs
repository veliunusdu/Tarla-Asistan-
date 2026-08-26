using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public class RequestOtpCommandHandler : IRequestHandler<RequestOtpCommand, RequestOtpResponseDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<RequestOtpCommandHandler> _logger;

    public RequestOtpCommandHandler(IApplicationDbContext db, IConfiguration config, ILogger<RequestOtpCommandHandler> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<RequestOtpResponseDto> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();
        var cooldownSeconds = _config.GetValue("Auth:OtpCooldownSeconds", 60);
        var ttlSeconds = _config.GetValue("Auth:OtpTtlSeconds", 180);
        var env = _config.GetValue("Environment", "Production") ?? "Production";
        var isLocal = env.Equals("local", StringComparison.OrdinalIgnoreCase);

        // Rate-limit check (cooldown)
        var recentOtp = await _db.OtpCodes
            .Where(o => o.PhoneNumber == phone && o.CreatedAtUtc >= DateTime.UtcNow.AddSeconds(-cooldownSeconds))
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentOtp != null)
        {
            throw new InvalidOperationException("Yeni kod istemeden önce kısa bir süre bekleyin.");
        }

        // Generate 6-digit random code
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var otp = new OtpCode
        {
            PhoneNumber = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ttlSeconds),
            IsUsed = false,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.OtpCodes.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        if (isLocal)
        {
            _logger.LogInformation("Development OTP for {PhoneNumber}: {Code}", phone, code);
        }

        return new RequestOtpResponseDto(
            Message: "Doğrulama kodu gönderildi.",
            ExpiresIn: ttlSeconds,
            DebugOtp: isLocal ? code : null
        );
    }
}

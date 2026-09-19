using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(User user, UserRole activeRole)
    {
        var effectiveRole = activeRole;
        var secret = _config["Auth:JwtSecret"] 
                  ?? _config["Jwt:Secret"] 
                  ?? _config["JWT_SECRET"]
                  ?? Environment.GetEnvironmentVariable("JWT_SECRET")
                  ?? throw new InvalidOperationException("Auth:JwtSecret is required to generate JWTs.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = _config.GetValue<int>("Auth:AccessTokenExpiryMinutes", 
                            _config.GetValue<int>("Auth:AccessTokenExpireMinutes", 15));
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, effectiveRole.ToString().ToUpperInvariant()),
            new Claim("role", effectiveRole.ToString().ToUpperInvariant()),
            new Claim("active_role", effectiveRole.ToString().ToUpperInvariant()),
            new Claim("phone", user.PhoneNumber)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "TarlaAsistani",
            audience: _config["Jwt:Audience"] ?? "TarlaAsistaniApp",
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }
}

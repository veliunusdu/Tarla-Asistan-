using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(User user)
    {
        var secret = _config["Auth:JwtSecret"] ?? _config["Jwt:Secret"] ?? "super_secret_jwt_key_at_least_32_characters_long_for_hmac_sha256_production!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = _config.GetValue<int>("Auth:AccessTokenExpiryMinutes", 
                            _config.GetValue<int>("Auth:AccessTokenExpireMinutes", 15));
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()),
            new Claim("role", user.Role.ToString().ToLowerInvariant()),
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

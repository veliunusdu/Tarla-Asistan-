namespace TarlaAsistani.Application.Common.Interfaces;

using TarlaAsistani.Domain.Entities;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

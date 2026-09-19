namespace TarlaAsistani.Application.Common.Interfaces;

using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

public interface IJwtService
{
    string GenerateAccessToken(User user, UserRole activeRole);
    string GenerateRefreshToken();
}

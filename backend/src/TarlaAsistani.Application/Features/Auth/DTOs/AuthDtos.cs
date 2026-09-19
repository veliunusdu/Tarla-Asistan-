using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Auth.DTOs;

public record UserDto(
    Guid Id,
    string PhoneNumber,
    string? FirebaseUid,
    string? FullName,
    string? Province,
    string? District,
    UserRole Role,
    bool TermsAccepted,
    bool NotificationsEnabled,
    bool ProfileComplete,
    UserRole ActiveRole,
    IReadOnlyList<UserRole> Roles
)
{
    public static UserDto FromEntity(User u, UserRole? activeRole = null, IEnumerable<UserRole>? roles = null)
    {
        var resolvedRoles = roles?.Distinct().ToList()
            ?? u.RoleAssignments
                .Where(a => a.RevokedAtUtc == null)
                .Select(a => a.Role)
                .Distinct()
                .ToList();

        var effectiveActiveRole = (activeRole.HasValue && resolvedRoles.Contains(activeRole.Value))
            ? activeRole.Value
            : (resolvedRoles.Contains(u.Role)
                ? u.Role
                : (resolvedRoles.Contains(UserRole.Farmer)
                    ? UserRole.Farmer
                    : (resolvedRoles.Count > 0 ? resolvedRoles[0] : UserRole.Farmer)));

        return new UserDto(
            u.Id,
            u.PhoneNumber,
            u.FirebaseUid,
            u.Profile?.FullName,
            u.Profile?.Province,
            u.Profile?.District,
            Role: effectiveActiveRole,
            TermsAccepted: u.Profile?.TermsAccepted ?? false,
            NotificationsEnabled: u.Profile?.NotificationsEnabled ?? true,
            ProfileComplete: !string.IsNullOrWhiteSpace(u.Profile?.FullName) && (u.Profile?.TermsAccepted ?? false),
            ActiveRole: effectiveActiveRole,
            Roles: resolvedRoles
        );
    }
}

public record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserDto User
);

public record RequestOtpResponseDto(
    string Message,
    int ExpiresIn,
    string? DebugOtp
);

public record AccountDeletionResponseDto(
    Guid RequestId,
    string Status
);

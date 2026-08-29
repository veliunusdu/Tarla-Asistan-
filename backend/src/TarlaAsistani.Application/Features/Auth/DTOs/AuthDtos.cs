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
    bool ProfileComplete
)
{
    public static UserDto FromEntity(User u) => new(
        u.Id,
        u.PhoneNumber,
        u.FirebaseUid,
        u.Profile?.FullName,
        u.Profile?.Province,
        u.Profile?.District,
        u.Role,
        u.Profile?.TermsAccepted ?? false,
        u.Profile?.NotificationsEnabled ?? true,
        ProfileComplete: !string.IsNullOrWhiteSpace(u.Profile?.FullName) && (u.Profile?.TermsAccepted ?? false)
    );
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

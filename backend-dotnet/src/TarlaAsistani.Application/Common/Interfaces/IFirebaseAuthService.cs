namespace TarlaAsistani.Application.Common.Interfaces;

public record FirebaseTokenInfo(
    string Uid,
    string? PhoneNumber,
    string? Email,
    string? DisplayName
);

public interface IFirebaseAuthService
{
    Task<FirebaseTokenInfo?> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

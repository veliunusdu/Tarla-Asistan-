using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Infrastructure.Services;

public class FirebaseAuthService : IFirebaseAuthService
{
    private readonly ILogger<FirebaseAuthService> _logger;
    private readonly IHostEnvironment? _environment;

    public FirebaseAuthService(ILogger<FirebaseAuthService> logger, IHostEnvironment? environment = null)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task<FirebaseTokenInfo?> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        // Development token simulation must never be accepted outside local/test environments.
        if (AllowsDevelopmentTokenSimulation() &&
            (idToken.StartsWith("dev_", StringComparison.OrdinalIgnoreCase) ||
             idToken.StartsWith("mock_", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("Using development Firebase token simulation for {IdToken}", idToken);
            return new FirebaseTokenInfo(
                Uid: idToken,
                PhoneNumber: "+905550000000",
                Email: $"{idToken}@example.com",
                DisplayName: "Dev User"
            );
        }

        // 2. If FirebaseApp is initialized, verify with Firebase Admin SDK
        if (FirebaseApp.DefaultInstance != null)
        {
            try
            {
                var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);
                
                string? phone = null;
                if (decodedToken.Claims.TryGetValue("phone_number", out var phoneObj) && phoneObj is string phoneStr)
                {
                    phone = phoneStr;
                }

                string? email = null;
                if (decodedToken.Claims.TryGetValue("email", out var emailObj) && emailObj is string emailStr)
                {
                    email = emailStr;
                }

                string? name = null;
                if (decodedToken.Claims.TryGetValue("name", out var nameObj) && nameObj is string nameStr)
                {
                    name = nameStr;
                }

                return new FirebaseTokenInfo(
                    Uid: decodedToken.Uid,
                    PhoneNumber: phone,
                    Email: email,
                    DisplayName: name
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed for token {TokenSnippet}", idToken[..Math.Min(10, idToken.Length)]);
                return null;
            }
        }

        _logger.LogWarning("FirebaseApp is not configured, and token is not a dev token.");
        return null;
    }

    private bool AllowsDevelopmentTokenSimulation()
    {
        var environmentName = _environment?.EnvironmentName
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RevokeUserSessionsAsync(string firebaseUid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid)) return;

        if (FirebaseApp.DefaultInstance != null)
        {
            try
            {
                await FirebaseAuth.DefaultInstance.RevokeRefreshTokensAsync(firebaseUid, cancellationToken);
                _logger.LogInformation("Revoked Firebase sessions for UID {Uid}", firebaseUid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke Firebase sessions for UID {Uid}", firebaseUid);
            }
        }
    }

    public async Task DeleteUserAsync(string firebaseUid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid)) return;

        if (FirebaseApp.DefaultInstance != null)
        {
            try
            {
                await FirebaseAuth.DefaultInstance.DeleteUserAsync(firebaseUid, cancellationToken);
                _logger.LogInformation("Deleted Firebase user with UID {Uid}", firebaseUid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Firebase user with UID {Uid}", firebaseUid);
            }
        }
    }
}

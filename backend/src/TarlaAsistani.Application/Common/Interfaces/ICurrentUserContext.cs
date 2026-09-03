using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Common.Interfaces;

/// <summary>
/// Provides trusted server-side identity and authorization information for the authenticated user.
/// AI tools and application services use this contract to avoid trusting model-supplied authentication fields.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets the unique identifier of the authenticated user, or null if unauthenticated.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets the role of the authenticated user, or null if unauthenticated.
    /// </summary>
    UserRole? Role { get; }

    /// <summary>
    /// Gets a value indicating whether an authenticated user is present.
    /// </summary>
    bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
}

/// <summary>
/// Default fallback implementation of <see cref="ICurrentUserContext"/> representing an unauthenticated context.
/// </summary>
public sealed class NullCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId => null;
    public UserRole? Role => null;
}

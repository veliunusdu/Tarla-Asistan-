using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Common;

public static class CurrentUserExtensions
{
    public static Guid? GetUserId(this HttpContext context)
    {
        // 1. Try ClaimsPrincipal (from JWT Bearer token)
        var claimId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? context.User.FindFirst("sub")?.Value;

        if (Guid.TryParse(claimId, out var idFromClaim))
        {
            return idFromClaim;
        }

        if (context.IsProduction())
        {
            return null;
        }

        // 2. Fallback: X-User-Id header (for pilot & testing)
        if (context.Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
            Guid.TryParse(headerVal.ToString(), out var idFromHeader))
        {
            return idFromHeader;
        }

        // 3. Fallback: Query parameter "userId"
        if (context.Request.Query.TryGetValue("userId", out var queryVal) &&
            Guid.TryParse(queryVal.ToString(), out var idFromQuery))
        {
            return idFromQuery;
        }

        return null;
    }

    public static UserRole? GetUserRole(this HttpContext context)
    {
        // 1. Try ClaimsPrincipal (from JWT Bearer token)
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                     ?? context.User.FindFirst("role")?.Value;

        if (!string.IsNullOrWhiteSpace(roleClaim) &&
            Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var roleFromClaim))
        {
            return roleFromClaim;
        }

        if (context.IsProduction())
        {
            return null;
        }

        // 2. Fallback: X-User-Role header
        if (context.Request.Headers.TryGetValue("X-User-Role", out var headerVal) &&
            Enum.TryParse<UserRole>(headerVal.ToString(), ignoreCase: true, out var roleFromHeader))
        {
            return roleFromHeader;
        }

        // 3. Fallback: Query parameter "role"
        if (context.Request.Query.TryGetValue("role", out var queryVal) &&
            Enum.TryParse<UserRole>(queryVal.ToString(), ignoreCase: true, out var roleFromQuery))
        {
            return roleFromQuery;
        }

        return null;
    }

    public static Guid ResolveUserId(this HttpContext context, Guid? explicitUserId = null, Guid? headerUserId = null)
    {
        if (context.IsProduction())
        {
            return context.GetUserId() ?? Guid.Empty;
        }

        if (explicitUserId.HasValue && explicitUserId.Value != Guid.Empty)
        {
            return explicitUserId.Value;
        }

        if (headerUserId.HasValue && headerUserId.Value != Guid.Empty)
        {
            return headerUserId.Value;
        }

        return context.GetUserId() ?? Guid.Empty;
    }

    public static UserRole ResolveUserRole(this HttpContext context, UserRole? explicitRole = null, string? headerRole = null, UserRole defaultRole = UserRole.Farmer)
    {
        if (context.IsProduction())
        {
            return context.GetUserRole() ?? UserRole.Farmer;
        }

        if (explicitRole.HasValue)
        {
            return explicitRole.Value;
        }

        if (!string.IsNullOrWhiteSpace(headerRole) &&
            Enum.TryParse<UserRole>(headerRole, ignoreCase: true, out var parsedHeaderRole))
        {
            return parsedHeaderRole;
        }

        return context.GetUserRole() ?? defaultRole;
    }

    private static bool IsProduction(this HttpContext context) =>
        context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();
}

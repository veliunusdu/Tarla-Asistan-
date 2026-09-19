using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Common;

public class ActiveRoleAssignmentRequirement : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; }

    public ActiveRoleAssignmentRequirement(UserRole? requiredRole = null)
    {
        RequiredRole = requiredRole;
    }
}

public class ActiveRoleRequirement : ActiveRoleAssignmentRequirement
{
    public ActiveRoleRequirement(UserRole requiredRole) : base(requiredRole)
    {
    }
}

public class ActiveRoleAuthorizationHandler : AuthorizationHandler<ActiveRoleAssignmentRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    public ActiveRoleAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveRoleAssignmentRequirement requirement)
    {
        HttpContext? httpContext = context.Resource as HttpContext
            ?? (context.Resource is EndpointFilterInvocationContext efc ? efc.HttpContext : null);

        var isProduction = httpContext?.IsProduction() ?? false;

        // 1. Resolve User ID
        Guid userId = Guid.Empty;
        var claimId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User.FindFirst("sub")?.Value;

        if (Guid.TryParse(claimId, out var parsedId))
        {
            userId = parsedId;
        }
        else if (!isProduction && httpContext != null)
        {
            userId = httpContext.ResolveUserId();
        }

        if (userId == Guid.Empty)
        {
            return;
        }

        // 2. Resolve Active Role
        UserRole? activeRole = null;
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                     ?? context.User.FindFirst("active_role")?.Value
                     ?? context.User.FindFirst("role")?.Value;

        if (!string.IsNullOrWhiteSpace(roleClaim) &&
            Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var parsedRole))
        {
            activeRole = parsedRole;
        }
        else if (!isProduction && httpContext != null)
        {
            activeRole = httpContext.GetUserRole();
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // In non-production, if activeRole is still null (e.g. tests passing only X-User-Id):
        if (!activeRole.HasValue && !isProduction)
        {
            if (requirement.RequiredRole.HasValue)
            {
                activeRole = requirement.RequiredRole.Value;
            }
            else
            {
                var activeRoles = await db.UserRoleAssignments
                    .Where(ura => ura.UserId == userId && ura.RevokedAtUtc == null)
                    .Select(ura => ura.Role)
                    .ToListAsync();
                if (activeRoles.Count > 0)
                {
                    activeRole = activeRoles.Contains(UserRole.Farmer) ? UserRole.Farmer : activeRoles[0];
                }
            }
        }

        if (!activeRole.HasValue)
        {
            return;
        }

        // 3. Verify specific required role if specified
        if (requirement.RequiredRole.HasValue && activeRole.Value != requirement.RequiredRole.Value)
        {
            return;
        }

        // 4. Validate against database user_role_assignments (active assignment required)
        var isRoleAssigned = await db.UserRoleAssignments.AnyAsync(ura =>
            ura.UserId == userId &&
            ura.Role == activeRole.Value &&
            ura.RevokedAtUtc == null);

        if (isRoleAssigned)
        {
            context.Succeed(requirement);
        }
    }
}

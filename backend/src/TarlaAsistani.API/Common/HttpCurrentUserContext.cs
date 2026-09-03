using Microsoft.AspNetCore.Http;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Common;

/// <summary>
/// HTTP-backed implementation of <see cref="ICurrentUserContext"/> utilizing <see cref="IHttpContextAccessor"/>
/// and existing <see cref="CurrentUserExtensions"/> resolution rules.
/// </summary>
public class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var resolved = httpContext.ResolveUserId();
            return resolved == Guid.Empty ? null : resolved;
        }
    }

    public UserRole? Role
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            return httpContext.GetUserRole();
        }
    }
}

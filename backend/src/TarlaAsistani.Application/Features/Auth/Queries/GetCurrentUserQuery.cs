using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Auth.Queries;

public record GetCurrentUserQuery(Guid UserId, UserRole? ActiveRole = null) : IRequest<UserDto?>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto?>
{
    private readonly IApplicationDbContext _db;

    public GetCurrentUserQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null) return null;

        var activeRoles = user.RoleAssignments
            .Where(ura => ura.RevokedAtUtc == null)
            .Select(ura => ura.Role)
            .Distinct()
            .ToList();

        var effectiveActiveRole = (request.ActiveRole.HasValue && activeRoles.Contains(request.ActiveRole.Value))
            ? request.ActiveRole.Value
            : (activeRoles.Contains(user.Role)
                ? user.Role
                : (activeRoles.Contains(UserRole.Farmer)
                    ? UserRole.Farmer
                    : (activeRoles.Count > 0 ? activeRoles[0] : UserRole.Farmer)));

        return UserDto.FromEntity(user, effectiveActiveRole, activeRoles);
    }
}

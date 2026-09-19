using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.API.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin");
        group.RequireAuthorization("AdminContext");

        // 1. GET /api/v1/admin/users - Search and list users
        group.MapGet("/users", async (
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IApplicationDbContext db) =>
        {
            var p = page.GetValueOrDefault(1) <= 0 ? 1 : page.GetValueOrDefault(1);
            var ps = pageSize.GetValueOrDefault(20) <= 0 ? 20 : (pageSize.GetValueOrDefault(20) > 100 ? 100 : pageSize.GetValueOrDefault(20));

            var query = db.Users
                .Include(u => u.Profile)
                .Include(u => u.RoleAssignments)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var isGuid = Guid.TryParse(s, out var searchGuid);
                query = query.Where(u =>
                    (isGuid && u.Id == searchGuid) ||
                    u.PhoneNumber.Contains(s) ||
                    (u.FirebaseUid != null && u.FirebaseUid.Contains(s)) ||
                    (u.Profile != null && u.Profile.FullName != null && u.Profile.FullName.ToLower().Contains(s.ToLower())));
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAtUtc)
                .Skip((p - 1) * ps)
                .Take(ps)
                .ToListAsync();

            var items = users.Select(u =>
            {
                var roles = u.RoleAssignments
                    .Where(ra => ra.RevokedAtUtc == null)
                    .Select(ra => ra.Role)
                    .Distinct()
                    .ToList();

                if (!roles.Contains(UserRole.Farmer) && u.AccountStatus == AccountStatus.Active)
                {
                    roles.Add(UserRole.Farmer);
                }

                return new AdminUserListItemDto(
                    Id: u.Id,
                    PhoneNumber: u.PhoneNumber,
                    FirebaseUid: u.FirebaseUid,
                    FullName: u.Profile?.FullName,
                    Roles: roles,
                    AccountStatus: u.AccountStatus,
                    CreatedAtUtc: u.CreatedAtUtc
                );
            }).ToList();

            return Results.Ok(new AdminUserListResponseDto(items, total, p, ps));
        })
        .WithName("AdminListUsers")
        .Produces<AdminUserListResponseDto>(StatusCodes.Status200OK);

        // 2. GET /api/v1/admin/users/{id} - User details with active assignments
        group.MapGet("/users/{id:guid}", async (
            Guid id,
            IApplicationDbContext db) =>
        {
            var user = await db.Users
                .Include(u => u.Profile)
                .Include(u => u.RoleAssignments)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
            }

            var roles = user.RoleAssignments
                .Where(ra => ra.RevokedAtUtc == null)
                .Select(ra => ra.Role)
                .Distinct()
                .ToList();

            if (!roles.Contains(UserRole.Farmer) && user.AccountStatus == AccountStatus.Active)
            {
                roles.Add(UserRole.Farmer);
            }

            var activeAssignments = user.RoleAssignments
                .Where(ra => ra.RevokedAtUtc == null)
                .Select(ra => new AdminRoleAssignmentDto(
                    Id: ra.Id,
                    Role: ra.Role,
                    GrantedAtUtc: ra.GrantedAtUtc,
                    GrantedByUserId: ra.GrantedByUserId,
                    GrantReason: ra.GrantReason
                ))
                .ToList();

            var dto = new AdminUserDetailDto(
                Id: user.Id,
                PhoneNumber: user.PhoneNumber,
                FirebaseUid: user.FirebaseUid,
                FullName: user.Profile?.FullName,
                Province: user.Profile?.Province,
                District: user.Profile?.District,
                AccountStatus: user.AccountStatus,
                CreatedAtUtc: user.CreatedAtUtc,
                Roles: roles,
                ActiveAssignments: activeAssignments
            );

            return Results.Ok(dto);
        })
        .WithName("AdminGetUserById")
        .Produces<AdminUserDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. POST /api/v1/admin/users/{id}/role-assignments - Assign role (only AGRONOMIST allowed via API, idempotent)
        group.MapPost("/users/{id:guid}/role-assignments", async (
            Guid id,
            HttpContext httpContext,
            AdminAssignRoleRequest req,
            IApplicationDbContext db) =>
        {
            // Security constraint: ADMIN cannot be assigned via web/API!
            if (req.Role == UserRole.Admin)
            {
                return Results.BadRequest(new { detail = "ADMIN rolü API üzerinden atanamaz; yalnızca güvenli CLI ile yönetilebilir." });
            }

            if (req.Role != UserRole.Agronomist)
            {
                return Results.BadRequest(new { detail = "Yalnızca AGRONOMIST rolü panel üzerinden atanabilir." });
            }

            var user = await db.Users
                .Include(u => u.RoleAssignments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
            }

            if (user.AccountStatus == AccountStatus.Anonymized)
            {
                return Results.BadRequest(new { detail = "Silinmiş veya anonimleştirilmiş hesaba rol atanamaz." });
            }

            var reason = req.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 500)
            {
                return Results.BadRequest(new { detail = "Rol atama gerekçesi (reason) zorunludur ve 3 ile 500 karakter arasında olmalıdır." });
            }

            var adminUserId = httpContext.ResolveUserId();

            // Idempotent: check if active assignment already exists
            var existingAssignment = user.RoleAssignments
                .FirstOrDefault(ra => ra.Role == req.Role && ra.RevokedAtUtc == null);

            if (existingAssignment != null)
            {
                return Results.Ok(new AdminRoleAssignmentDto(
                    Id: existingAssignment.Id,
                    Role: existingAssignment.Role,
                    GrantedAtUtc: existingAssignment.GrantedAtUtc,
                    GrantedByUserId: existingAssignment.GrantedByUserId,
                    GrantReason: existingAssignment.GrantReason
                ));
            }

            var assignment = new UserRoleAssignment
            {
                UserId = user.Id,
                Role = req.Role,
                GrantedAtUtc = DateTime.UtcNow,
                GrantedByUserId = adminUserId == Guid.Empty ? null : adminUserId,
                GrantReason = reason
            };

            db.UserRoleAssignments.Add(assignment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/admin/users/{id}/role-assignments", new AdminRoleAssignmentDto(
                Id: assignment.Id,
                Role: assignment.Role,
                GrantedAtUtc: assignment.GrantedAtUtc,
                GrantedByUserId: assignment.GrantedByUserId,
                GrantReason: assignment.GrantReason
            ));
        })
        .WithName("AdminAssignRole")
        .Produces<AdminRoleAssignmentDto>(StatusCodes.Status200OK)
        .Produces<AdminRoleAssignmentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // 4. POST /api/v1/admin/users/{id}/role-assignments/{role}/revoke - Revoke role (idempotent, revokes role's refresh tokens)
        group.MapPost("/users/{id:guid}/role-assignments/{role}/revoke", async (
            Guid id,
            string role,
            HttpContext httpContext,
            AdminRevokeRoleRequest req,
            IApplicationDbContext db) =>
        {
            var reason = req.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 500)
            {
                return Results.BadRequest(new { detail = "Rol iptal gerekçesi (reason) zorunludur ve 3 ile 500 karakter arasında olmalıdır." });
            }

            if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
            {
                return Results.BadRequest(new { detail = "Geçersiz rol adı." });
            }

            if (parsedRole == UserRole.Admin)
            {
                return Results.BadRequest(new { detail = "ADMIN rolü API üzerinden yönetilemez." });
            }

            if (parsedRole == UserRole.Farmer)
            {
                return Results.BadRequest(new { detail = "FARMER temel rolü geri alınamaz." });
            }

            var user = await db.Users
                .Include(u => u.RoleAssignments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
            }

            var adminUserId = httpContext.ResolveUserId();
            var activeAssignment = user.RoleAssignments
                .FirstOrDefault(ra => ra.Role == parsedRole && ra.RevokedAtUtc == null);

            if (activeAssignment != null)
            {
                var now = DateTime.UtcNow;
                activeAssignment.RevokedAtUtc = now;
                activeAssignment.RevokedByUserId = adminUserId == Guid.Empty ? null : adminUserId;
                activeAssignment.RevokeReason = reason;

                // Revoke refresh tokens bound to this role
                var tokensToRevoke = await db.RefreshTokens
                    .Where(rt => rt.UserId == id && rt.ActiveRole == parsedRole && rt.RevokedAtUtc == null)
                    .ToListAsync();

                foreach (var token in tokensToRevoke)
                {
                    token.RevokedAtUtc = now;
                }

                await db.SaveChangesAsync();
            }

            return Results.Ok(new { message = $"{parsedRole} rolü başarıyla geri alındı." });
        })
        .WithName("AdminRevokeRole")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // 5. GET /api/v1/admin/users/{id}/role-history - Get role assignment history
        group.MapGet("/users/{id:guid}/role-history", async (
            Guid id,
            IApplicationDbContext db) =>
        {
            var exists = await db.Users.AnyAsync(u => u.Id == id);
            if (!exists)
            {
                return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
            }

            var history = await db.UserRoleAssignments
                .Where(ra => ra.UserId == id)
                .OrderByDescending(ra => ra.GrantedAtUtc)
                .Select(ra => new AdminRoleHistoryItemDto(
                    ra.Id,
                    ra.Role,
                    ra.GrantedAtUtc,
                    ra.GrantedByUserId,
                    ra.GrantReason,
                    ra.RevokedAtUtc,
                    ra.RevokedByUserId,
                    ra.RevokeReason
                ))
                .ToListAsync();

            return Results.Ok(history);
        })
        .WithName("AdminGetRoleHistory")
        .Produces<List<AdminRoleHistoryItemDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

// DTOs for Admin Endpoints
public record AdminUserListItemDto(
    Guid Id,
    string PhoneNumber,
    string? FirebaseUid,
    string? FullName,
    IReadOnlyList<UserRole> Roles,
    AccountStatus AccountStatus,
    DateTime CreatedAtUtc
);

public record AdminUserListResponseDto(
    IReadOnlyList<AdminUserListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record AdminRoleAssignmentDto(
    Guid Id,
    UserRole Role,
    DateTime GrantedAtUtc,
    Guid? GrantedByUserId,
    string? GrantReason
);

public record AdminUserDetailDto(
    Guid Id,
    string PhoneNumber,
    string? FirebaseUid,
    string? FullName,
    string? Province,
    string? District,
    AccountStatus AccountStatus,
    DateTime CreatedAtUtc,
    IReadOnlyList<UserRole> Roles,
    IReadOnlyList<AdminRoleAssignmentDto> ActiveAssignments
);

public record AdminAssignRoleRequest(
    UserRole Role,
    string? Reason = null
);

public record AdminRevokeRoleRequest(
    string? Reason = null
);

public record AdminRoleHistoryItemDto(
    Guid Id,
    UserRole Role,
    DateTime GrantedAtUtc,
    Guid? GrantedByUserId,
    string? GrantReason,
    DateTime? RevokedAtUtc,
    Guid? RevokedByUserId,
    string? RevokeReason
);

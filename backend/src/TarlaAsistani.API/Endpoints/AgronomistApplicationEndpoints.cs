using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Common.Utilities;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class AgronomistApplicationEndpoints
{
    private static readonly KeyedLockManager<Guid> ApplicationLocks = new();

    public static IEndpointRouteBuilder MapAgronomistApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. User application endpoints
        var userGroup = app.MapGroup("/api/v1/agronomist-applications").WithTags("Agronomist Applications");
        userGroup.RequireAuthorization("ActiveRoleAssignment");

        // POST /api/v1/agronomist-applications - Apply to become an agronomist
        userGroup.MapPost("", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            SubmitAgronomistApplicationRequest req,
            IApplicationDbContext db) =>
        {
            var resolvedUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (resolvedUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (req.Note != null && req.Note.Length > 2000)
            {
                return Results.BadRequest(new { detail = "Başvuru notu en fazla 2000 karakter olabilir." });
            }

            using (await ApplicationLocks.AcquireAsync(resolvedUserId))
            {
                // Check if already an active agronomist
                var isAlreadyAgronomist = await db.UserRoleAssignments
                    .AnyAsync(ra => ra.UserId == resolvedUserId && ra.Role == UserRole.Agronomist && ra.RevokedAtUtc == null);

                if (isAlreadyAgronomist)
                {
                    return Results.BadRequest(new { detail = "Kullanıcı zaten aktif bir ziraatçi rolüne sahiptir." });
                }

                // Check for existing pending application (idempotency)
                var pendingApp = await db.AgronomistApplications
                    .Include(a => a.User)
                        .ThenInclude(u => u.Profile)
                    .FirstOrDefaultAsync(a => a.UserId == resolvedUserId && a.Status == AgronomistApplicationStatus.Pending);

                if (pendingApp != null)
                {
                    return Results.Ok(ToDto(pendingApp));
                }

                var now = DateTime.UtcNow;
                var newApp = new AgronomistApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = resolvedUserId,
                    Status = AgronomistApplicationStatus.Pending,
                    ApplicantNote = req.Note?.Trim(),
                    SubmittedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                db.AgronomistApplications.Add(newApp);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    var existing = await db.AgronomistApplications
                        .Include(a => a.User)
                            .ThenInclude(u => u.Profile)
                        .FirstOrDefaultAsync(a => a.UserId == resolvedUserId && a.Status == AgronomistApplicationStatus.Pending);
                    if (existing != null)
                    {
                        return Results.Ok(ToDto(existing));
                    }
                    throw;
                }

                var created = await db.AgronomistApplications
                    .Include(a => a.User)
                        .ThenInclude(u => u.Profile)
                    .FirstAsync(a => a.Id == newApp.Id);

                return Results.Created($"/api/v1/agronomist-applications/{created.Id}", ToDto(created));
            }
        })
        .WithName("SubmitAgronomistApplication")
        .Produces<AgronomistApplicationDto>(StatusCodes.Status201Created)
        .Produces<AgronomistApplicationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/v1/agronomist-applications/me - Get my latest application
        userGroup.MapGet("/me", async (
            HttpContext httpContext,
            IApplicationDbContext db) =>
        {
            var userId = httpContext.ResolveUserId();
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var latestApp = await db.AgronomistApplications
                .Include(a => a.User)
                    .ThenInclude(u => u.Profile)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.SubmittedAtUtc)
                .FirstOrDefaultAsync();

            if (latestApp == null)
            {
                return Results.NotFound(new { detail = "Ziraatçi başvurusu bulunamadı." });
            }

            return Results.Ok(ToDto(latestApp));
        })
        .WithName("GetMyAgronomistApplication")
        .Produces<AgronomistApplicationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 2. Admin application review endpoints
        var adminGroup = app.MapGroup("/api/v1/admin/agronomist-applications").WithTags("Admin Agronomist Applications");
        adminGroup.RequireAuthorization("AdminContext");

        // GET /api/v1/admin/agronomist-applications - List applications
        adminGroup.MapGet("", async (
            [FromQuery] AgronomistApplicationStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IApplicationDbContext db) =>
        {
            var p = page.GetValueOrDefault(1) <= 0 ? 1 : page.GetValueOrDefault(1);
            var ps = pageSize.GetValueOrDefault(20) <= 0 ? 20 : (pageSize.GetValueOrDefault(20) > 100 ? 100 : pageSize.GetValueOrDefault(20));

            var query = db.AgronomistApplications
                .Include(a => a.User)
                    .ThenInclude(u => u.Profile)
                .AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.SubmittedAtUtc)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(a => ToDto(a))
                .ToListAsync();

            return Results.Ok(new AgronomistApplicationListResponseDto(items, total, p, ps));
        })
        .WithName("AdminListAgronomistApplications")
        .Produces<AgronomistApplicationListResponseDto>(StatusCodes.Status200OK);

        // POST /api/v1/admin/agronomist-applications/{id}/approve - Approve application and grant agronomist role
        adminGroup.MapPost("/{id:guid}/approve", async (
            Guid id,
            HttpContext httpContext,
            ReviewAgronomistApplicationRequest req,
            IApplicationDbContext db) =>
        {
            if (req.Reason != null && req.Reason.Length > 1000)
            {
                return Results.BadRequest(new { detail = "İnceleme gerekçesi en fazla 1000 karakter olabilir." });
            }

            var adminUserId = httpContext.ResolveUserId();
            var application = await db.AgronomistApplications
                .Include(a => a.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                return Results.NotFound(new { detail = "Başvuru bulunamadı." });
            }

            if (application.Status == AgronomistApplicationStatus.Rejected)
            {
                return Results.Conflict(new { detail = "Reddedilmiş başvuru onaylanamaz." });
            }

            var now = DateTime.UtcNow;

            if (application.Status != AgronomistApplicationStatus.Approved)
            {
                application.Status = AgronomistApplicationStatus.Approved;
                application.ReviewedAtUtc = now;
                application.ReviewedByUserId = adminUserId;
                application.ReviewReason = req.Reason?.Trim();
                application.UpdatedAtUtc = now;

                // Grant Agronomist role assignment if not already granted
                var existingAssignment = await db.UserRoleAssignments
                    .FirstOrDefaultAsync(ra => ra.UserId == application.UserId && ra.Role == UserRole.Agronomist && ra.RevokedAtUtc == null);

                if (existingAssignment == null)
                {
                    db.UserRoleAssignments.Add(new UserRoleAssignment
                    {
                        Id = Guid.NewGuid(),
                        UserId = application.UserId,
                        Role = UserRole.Agronomist,
                        GrantedAtUtc = now,
                        GrantedByUserId = adminUserId,
                        GrantReason = req.Reason ?? "Agronomist application approved"
                    });
                }

                await db.SaveChangesAsync();
            }

            return Results.Ok(ToDto(application));
        })
        .WithName("AdminApproveAgronomistApplication")
        .Produces<AgronomistApplicationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/agronomist-applications/{id}/reject - Reject application
        adminGroup.MapPost("/{id:guid}/reject", async (
            Guid id,
            HttpContext httpContext,
            ReviewAgronomistApplicationRequest req,
            IApplicationDbContext db) =>
        {
            if (req.Reason != null && req.Reason.Length > 1000)
            {
                return Results.BadRequest(new { detail = "İnceleme gerekçesi en fazla 1000 karakter olabilir." });
            }

            var adminUserId = httpContext.ResolveUserId();
            var application = await db.AgronomistApplications
                .Include(a => a.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                return Results.NotFound(new { detail = "Başvuru bulunamadı." });
            }

            if (application.Status == AgronomistApplicationStatus.Approved)
            {
                return Results.Conflict(new { detail = "Onaylanmış başvuru reddedilemez." });
            }

            var now = DateTime.UtcNow;

            if (application.Status != AgronomistApplicationStatus.Rejected)
            {
                application.Status = AgronomistApplicationStatus.Rejected;
                application.ReviewedAtUtc = now;
                application.ReviewedByUserId = adminUserId;
                application.ReviewReason = req.Reason?.Trim();
                application.UpdatedAtUtc = now;

                await db.SaveChangesAsync();
            }

            return Results.Ok(ToDto(application));
        })
        .WithName("AdminRejectAgronomistApplication")
        .Produces<AgronomistApplicationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static AgronomistApplicationDto ToDto(AgronomistApplication a) => new(
        Id: a.Id,
        UserId: a.UserId,
        PhoneNumber: a.User?.PhoneNumber ?? string.Empty,
        FullName: a.User?.Profile?.FullName,
        Status: a.Status,
        ApplicantNote: a.ApplicantNote,
        SubmittedAtUtc: a.SubmittedAtUtc,
        ReviewedAtUtc: a.ReviewedAtUtc,
        ReviewedByUserId: a.ReviewedByUserId,
        ReviewReason: a.ReviewReason
    );
}

public record SubmitAgronomistApplicationRequest(
    string? Note = null
);

public record ReviewAgronomistApplicationRequest(
    string? Reason = null
);

public record AgronomistApplicationDto(
    Guid Id,
    Guid UserId,
    string PhoneNumber,
    string? FullName,
    AgronomistApplicationStatus Status,
    string? ApplicantNote,
    DateTime SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    Guid? ReviewedByUserId,
    string? ReviewReason
);

public record AgronomistApplicationListResponseDto(
    IReadOnlyList<AgronomistApplicationDto> Items,
    int Total,
    int Page,
    int PageSize
);

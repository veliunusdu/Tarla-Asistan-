using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Activities.Commands;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Application.Features.Activities.Queries;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.API.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var farmActivities = app.MapGroup("/api/v1/farms/{farmId:guid}")
                                .WithTags("Activities & Journal");

        var activities = app.MapGroup("/api/v1/activities")
                            .WithTags("Activities");

        // 1. POST /api/v1/farms/{farmId}/activities - Create activity or voice draft
        farmActivities.MapPost("/activities", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] UserRole? role,
            CreateActivityApiRequest req,
            IMediator mediator,
            IValidator<CreateActivityCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var userRole = httpContext.ResolveUserRole(role, headerRole);

            var command = new CreateActivityCommand(
                FarmId: farmId,
                CreatedById: userId,
                ActivityName: req.ActivityName ?? string.Empty,
                Description: req.Description ?? string.Empty,
                OccurredAt: req.OccurredAt ?? DateTime.UtcNow,
                ActivityType: req.ActivityType,
                CropPeriodId: req.CropPeriodId,
                InputMethod: req.InputMethod ?? ActivitySource.Manual,
                DurationMinutes: req.DurationMinutes,
                Amount: req.Amount,
                Unit: req.Unit,
                PhotoUrl: req.PhotoUrl,
                VoiceUrl: req.VoiceUrl,
                VoiceTranscript: req.VoiceTranscript,
                PerformedBy: req.PerformedBy,
                Cost: req.Cost,
                ClientOperationId: req.ClientOperationId,
                CreatedByRole: userRole
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/activities/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CreateActivity")
        .Produces<ActivityDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // 2. GET /api/v1/farms/{farmId}/activities - List farm activities
        farmActivities.MapGet("/activities", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] bool? includeDrafts,
            [FromQuery] bool? includeArchived,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            try
            {
                var query = new ListActivitiesQuery(
                    FarmId: farmId,
                    UserId: queryUserId,
                    Role: userRole,
                    IncludeDrafts: includeDrafts ?? true,
                    IncludeArchived: includeArchived ?? false,
                    Limit: limit ?? 50,
                    Offset: offset ?? 0
                );

                var result = await mediator.Send(query);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
        })
        .WithName("ListActivities")
        .Produces<ActivityListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. GET /api/v1/farms/{farmId}/journal - Get chronological farm journal
        farmActivities.MapGet("/journal", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var query = new GetFarmJournalQuery(
                FarmId: farmId,
                UserId: queryUserId,
                Role: userRole,
                Limit: limit ?? 50,
                Offset: offset ?? 0
            );

            var result = await mediator.Send(query);
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Tarla bulunamadı." });
        })
        .WithName("GetFarmJournal")
        .Produces<FarmJournalResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 4. GET /api/v1/activities/{id} - Get activity details
        activities.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new GetActivityByIdQuery(id, queryUserId, userRole));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
        })
        .WithName("GetActivityById")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 5. PATCH /api/v1/activities/{id} - Update activity
        activities.MapPatch("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdateActivityApiRequest req,
            IMediator mediator,
            IValidator<UpdateActivityCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new UpdateActivityCommand(
                ActivityId: id,
                UserId: userId,
                ActivityName: req.ActivityName,
                ActivityType: req.ActivityType,
                Description: req.Description,
                OccurredAt: req.OccurredAt,
                CropPeriodId: req.CropPeriodId,
                DurationMinutes: req.DurationMinutes,
                Amount: req.Amount,
                Unit: req.Unit,
                PhotoUrl: req.PhotoUrl,
                VoiceUrl: req.VoiceUrl,
                VoiceTranscript: req.VoiceTranscript,
                PerformedBy: req.PerformedBy,
                Cost: req.Cost
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
        })
        .WithName("UpdateActivity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem();

        // 6. DELETE /api/v1/activities/{id} - Soft-archive activity
        activities.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var commandUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (commandUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await mediator.Send(new ArchiveActivityCommand(id, commandUserId));
            return result ? Results.NoContent() : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
        })
        .WithName("ArchiveActivity")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 7. POST /api/v1/activities/{id}/confirm - Confirm activity
        activities.MapPost("/{id:guid}/confirm", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromBody] ConfirmActivityApiRequest? req,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(req?.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await mediator.Send(new ConfirmActivityCommand(id, userId));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
        })
        .WithName("ConfirmActivity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 8. POST /api/v1/activities/{id}/restore - Restore archived activity
        activities.MapPost("/{id:guid}/restore", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromBody] RestoreActivityApiRequest? req,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(req?.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await mediator.Send(new RestoreActivityCommand(id, userId));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
        })
        .WithName("RestoreActivity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 9. GET /api/v1/activities/{id}/revisions - List activity change revisions
        activities.MapGet("/{id:guid}/revisions", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var revisions = await mediator.Send(new ListActivityRevisionsQuery(id, queryUserId, userRole));
            return revisions is not null ? Results.Ok(revisions) : Results.NotFound(new { detail = "Faaliyet bulunamadı." });
        })
        .WithName("ListActivityRevisions")
        .Produces<List<ActivityRevisionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public record CreateActivityApiRequest(
    Guid? UserId = null,
    ActivityType? ActivityType = null,
    string? Description = null,
    DateTime? OccurredAt = null,
    Guid? CropPeriodId = null,
    ActivitySource? InputMethod = null,
    int? DurationMinutes = null,
    float? Amount = null,
    string? Unit = null,
    string? PhotoUrl = null,
    string? VoiceUrl = null,
    string? VoiceTranscript = null,
    string? PerformedBy = null,
    float? Cost = null,
    Guid? ClientOperationId = null,
    string? ActivityName = null
);

public record UpdateActivityApiRequest(
    Guid? UserId = null,
    ActivityType? ActivityType = null,
    string? Description = null,
    DateTime? OccurredAt = null,
    Guid? CropPeriodId = null,
    int? DurationMinutes = null,
    float? Amount = null,
    string? Unit = null,
    string? PhotoUrl = null,
    string? VoiceUrl = null,
    string? VoiceTranscript = null,
    string? PerformedBy = null,
    float? Cost = null,
    string? ActivityName = null
);

public record ConfirmActivityApiRequest(Guid? UserId = null);
public record RestoreActivityApiRequest(Guid? UserId = null);

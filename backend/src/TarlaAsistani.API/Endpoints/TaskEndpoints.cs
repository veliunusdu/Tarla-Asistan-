using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.API.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var farmTasks = app.MapGroup("/api/v1/farms/{farmId:guid}/tasks")
                           .WithTags("Daily Tasks");

        var tasks = app.MapGroup("/api/v1/tasks")
                       .WithTags("Daily Tasks");

        // 1. POST /api/v1/farms/{farmId}/tasks - Create expert task (AGRONOMIST only)
        farmTasks.MapPost("", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateExpertTaskApiRequest req,
            IMediator mediator,
            IValidator<CreateExpertTaskCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new CreateExpertTaskCommand(
                FarmId: farmId,
                CreatedById: userId,
                Title: req.Title,
                Description: req.Description,
                Reason: req.Reason,
                Priority: req.Priority ?? TaskPriority.High,
                Confidence: req.Confidence ?? TaskConfidence.High,
                DueDate: req.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                CropPeriodId: req.CropPeriodId
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/tasks/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { detail = ex.Message });
            }
        })
        .WithName("CreateExpertTask")
        .Produces<TaskDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // 2. GET /api/v1/farms/{farmId}/tasks - List daily tasks
        farmTasks.MapGet("", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] DateOnly? date,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await mediator.Send(new ListDailyTasksQuery(farmId, queryUserId, userRole, targetDate));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Tarla bulunamadı." });
        })
        .WithName("ListDailyTasks")
        .Produces<DailyTaskListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. GET /api/v1/tasks/{id} - Get task details
        tasks.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new GetTaskByIdQuery(id, queryUserId, userRole));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Görev bulunamadı." });
        })
        .WithName("GetTaskById")
        .Produces<TaskDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 4. PATCH /api/v1/tasks/{id}/status - Update task status
        tasks.MapPatch("/{id:guid}/status", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdateTaskStatusApiRequest req,
            IMediator mediator,
            IValidator<UpdateTaskStatusCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            var userRole = httpContext.ResolveUserRole(req.Role, defaultRole: UserRole.Farmer);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new UpdateTaskStatusCommand(
                TaskId: id,
                UserId: userId,
                Role: userRole,
                Status: req.Status,
                NotAppliedReason: req.NotAppliedReason,
                Note: req.Note,
                PhotoUrl: req.PhotoUrl
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Görev bulunamadı." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { detail = ex.Message });
            }
        })
        .WithName("UpdateTaskStatus")
        .Produces<TaskDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        // 5. POST /api/v1/tasks/{id}/complete - Complete task and record in journal
        tasks.MapPost("/{id:guid}/complete", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CompleteTaskApiRequest req,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            var userRole = httpContext.ResolveUserRole(req.Role, defaultRole: UserRole.Farmer);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            try
            {
                var result = await mediator.Send(new CompleteTaskCommand(id, userId, userRole, req.Note, req.PhotoUrl));
                return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Görev bulunamadı." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { detail = ex.Message });
            }
        })
        .WithName("CompleteTask")
        .Produces<TaskDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}

public record CreateExpertTaskApiRequest(
    Guid? UserId,
    string Title,
    string Description,
    string Reason,
    TaskPriority? Priority,
    TaskConfidence? Confidence,
    DateOnly? DueDate,
    Guid? CropPeriodId
);

public record UpdateTaskStatusApiRequest(
    Guid? UserId,
    UserRole? Role,
    TaskStatus Status,
    string? NotAppliedReason,
    string? Note,
    string? PhotoUrl
);

public record CompleteTaskApiRequest(
    Guid? UserId,
    UserRole? Role,
    string? Note,
    string? PhotoUrl
);

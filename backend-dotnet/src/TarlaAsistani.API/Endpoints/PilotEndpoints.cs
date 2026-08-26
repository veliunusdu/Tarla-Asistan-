using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.Application.Features.Pilot.Commands;
using TarlaAsistani.Application.Features.Pilot.DTOs;
using TarlaAsistani.Application.Features.Pilot.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class PilotEndpoints
{
    public static IEndpointRouteBuilder MapPilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pilot").WithTags("Pilot Metrics & Feedback");

        // 1. POST /api/v1/pilot/feedback - Submit feedback or false alert
        group.MapPost("/feedback", async (
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreatePilotFeedbackApiRequest req,
            IMediator mediator,
            IValidator<CreatePilotFeedbackCommand> validator) =>
        {
            var userId = headerUserId ?? req.UserId;
            var userRole = req.Role ?? UserRole.Farmer;

            var command = new CreatePilotFeedbackCommand(
                CreatedById: userId,
                Role: userRole,
                FeedbackType: req.FeedbackType,
                Comment: req.Comment,
                Rating: req.Rating,
                RelatedTaskId: req.RelatedTaskId,
                RelatedCaseId: req.RelatedCaseId
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/pilot/feedback/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
        })
        .WithName("CreatePilotFeedback")
        .Produces<PilotFeedbackDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // 2. GET /api/v1/pilot/feedback - List feedback (Agronomist only)
        group.MapGet("/feedback", async (
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] FeedbackType? feedbackType,
            [FromQuery] FeedbackStatus? status,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator) =>
        {
            var queryUserId = headerUserId ?? userId ?? Guid.Empty;
            var userRole = role ?? UserRole.Agronomist;

            try
            {
                var result = await mediator.Send(new ListPilotFeedbackQuery(
                    UserId: queryUserId,
                    Role: userRole,
                    FeedbackType: feedbackType,
                    Status: status,
                    Limit: limit ?? 50,
                    Offset: offset ?? 0
                ));
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ListPilotFeedback")
        .Produces<PilotFeedbackListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden);

        // 3. PATCH /api/v1/pilot/feedback/{id} - Review feedback (Agronomist only)
        group.MapPatch("/feedback/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdatePilotFeedbackStatusApiRequest req,
            IMediator mediator) =>
        {
            var reviewerId = headerUserId ?? req.UserId;
            var userRole = req.Role ?? UserRole.Agronomist;

            try
            {
                var result = await mediator.Send(new UpdatePilotFeedbackStatusCommand(id, reviewerId, userRole, req.Status));
                return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Geri bildirim bulunamadı." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("UpdatePilotFeedbackStatus")
        .Produces<PilotFeedbackDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 4. GET /api/v1/pilot/metrics - Pilot success & quality metrics (Agronomist only)
        group.MapGet("/metrics", async (
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] int? windowDays,
            IMediator mediator) =>
        {
            var queryUserId = headerUserId ?? userId ?? Guid.Empty;
            var userRole = role ?? UserRole.Agronomist;

            try
            {
                var result = await mediator.Send(new GetPilotMetricsQuery(queryUserId, userRole, windowDays ?? 7));
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("GetPilotMetrics")
        .Produces<PilotMetricsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}

public record CreatePilotFeedbackApiRequest(
    Guid UserId,
    UserRole? Role,
    FeedbackType FeedbackType,
    string Comment,
    int? Rating,
    Guid? RelatedTaskId,
    Guid? RelatedCaseId
);

public record UpdatePilotFeedbackStatusApiRequest(
    Guid UserId,
    UserRole? Role,
    FeedbackStatus Status
);

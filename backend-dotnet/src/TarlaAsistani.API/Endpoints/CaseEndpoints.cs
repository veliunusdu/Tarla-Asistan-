using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Cases.Commands;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Application.Features.Cases.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cases").WithTags("Support Cases");

        // 1. POST /api/v1/cases - Create support case
        group.MapPost("/", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateCaseApiRequest req,
            IMediator mediator,
            IValidator<CreateCaseCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new CreateCaseCommand(
                FarmId: req.FarmId,
                CreatedById: userId,
                Category: req.Category,
                Title: req.Title,
                Description: req.Description,
                MediaIds: req.MediaIds,
                ClientOperationId: req.ClientOperationId
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/cases/{result.Id}", result);
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
        .WithName("CreateCase")
        .Produces<CaseDetailDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // 2. GET /api/v1/cases - List role-filtered cases
        group.MapGet("/", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] CaseStatus? status,
            [FromQuery] CasePriority? priority,
            [FromQuery] Guid? farmId,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new ListCasesQuery(
                UserId: queryUserId,
                Role: userRole,
                Status: status,
                Priority: priority,
                FarmId: farmId,
                Limit: limit ?? 50,
                Offset: offset ?? 0
            ));

            return Results.Ok(result);
        })
        .WithName("ListCases")
        .Produces<CaseListDto>(StatusCodes.Status200OK);

        // 3. GET /api/v1/cases/{id} - Get case details
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new GetCaseByIdQuery(id, queryUserId, userRole));
            return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Vaka bulunamadı." });
        })
        .WithName("GetCaseById")
        .Produces<CaseDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 4. PATCH /api/v1/cases/{id}/status - Update case status & priority (Agronomist only)
        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdateCaseStatusApiRequest req,
            IMediator mediator,
            IValidator<UpdateCaseStatusCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            var userRole = httpContext.ResolveUserRole(req.Role, defaultRole: UserRole.Agronomist);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new UpdateCaseStatusCommand(
                CaseId: id,
                UserId: userId,
                Role: userRole,
                Status: req.Status,
                Priority: req.Priority,
                AssignToMe: req.AssignToMe ?? false
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Vaka bulunamadı." });
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
        .WithName("UpdateCaseStatus")
        .Produces<CaseDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        // 5. POST /api/v1/cases/{id}/messages - Add message to case
        group.MapPost("/{id:guid}/messages", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateCaseMessageApiRequest req,
            IMediator mediator,
            IValidator<CreateCaseMessageCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            var userRole = httpContext.ResolveUserRole(req.Role, defaultRole: UserRole.Farmer);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new CreateCaseMessageCommand(
                CaseId: id,
                SenderId: userId,
                Role: userRole,
                MessageType: req.MessageType,
                Body: req.Body,
                MediaIds: req.MediaIds,
                ClientOperationId: req.ClientOperationId
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/cases/{id}/messages/{result.Id}", result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
        })
        .WithName("CreateCaseMessage")
        .Produces<CaseMessageDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem();

        // 6. POST /api/v1/cases/{id}/expert-response - Respond as expert and optionally close
        group.MapPost("/{id:guid}/expert-response", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateExpertResponseApiRequest req,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            var userRole = httpContext.ResolveUserRole(req.Role, defaultRole: UserRole.Agronomist);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new CreateExpertResponseCommand(
                CaseId: id,
                ExpertId: userId,
                Role: userRole,
                Body: req.Body,
                CloseCase: req.CloseCase ?? false,
                MediaIds: req.MediaIds,
                ClientOperationId: req.ClientOperationId
            );

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/cases/{id}", result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
        })
        .WithName("CreateExpertResponse")
        .Produces<CaseDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

public record CreateCaseApiRequest(
    Guid? UserId,
    Guid FarmId,
    CaseCategory Category,
    string Title,
    string Description,
    List<Guid>? MediaIds,
    Guid? ClientOperationId
);

public record UpdateCaseStatusApiRequest(
    Guid? UserId,
    UserRole? Role,
    CaseStatus Status,
    CasePriority? Priority,
    bool? AssignToMe
);

public record CreateCaseMessageApiRequest(
    Guid? UserId,
    UserRole? Role,
    CaseMessageType MessageType,
    string Body,
    List<Guid>? MediaIds,
    Guid? ClientOperationId
);

public record CreateExpertResponseApiRequest(
    Guid? UserId,
    UserRole? Role,
    string Body,
    bool? CloseCase,
    List<Guid>? MediaIds,
    Guid? ClientOperationId
);

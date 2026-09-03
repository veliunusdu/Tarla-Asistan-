using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.AI.Commands;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Queries;

namespace TarlaAsistani.API.Endpoints;

public static class ProactiveAdvisoryEndpoints
{
    public static IEndpointRouteBuilder MapProactiveAdvisoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/advisories").WithTags("Proactive AI Advisories");

        // 1. GET /api/v1/ai/advisories — List active advisories for user or specific farm
        group.MapGet("/", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery(Name = "farm_id")] Guid? farmId,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var query = new GetProactiveAdvisoriesQuery(userId, farmId);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetProactiveAdvisories")
        .Produces<IReadOnlyList<ProactiveAdvisoryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. POST /api/v1/ai/advisories/{id}/apply — Apply recommendation (e.g. postpone task)
        group.MapPost("/{id:guid}/apply", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new ApplyProactiveAdvisoryCommand(id, userId);
            var success = await mediator.Send(command);
            if (!success)
            {
                return Results.NotFound(new { detail = "Tavsiye bulunamadı veya yetkiniz yok." });
            }

            return Results.Ok(new { message = "Tavsiye uygulandı ve görev takvimi güncellendi." });
        })
        .WithName("ApplyProactiveAdvisory")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 3. POST /api/v1/ai/advisories/{id}/dismiss — Dismiss advisory
        group.MapPost("/{id:guid}/dismiss", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new DismissProactiveAdvisoryCommand(id, userId);
            var success = await mediator.Send(command);
            if (!success)
            {
                return Results.NotFound(new { detail = "Tavsiye bulunamadı veya yetkiniz yok." });
            }

            return Results.Ok(new { message = "Tavsiye kapatıldı." });
        })
        .WithName("DismissProactiveAdvisory")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 4. POST /api/v1/ai/advisories/evaluate/{farmId} — Trigger farm advisory evaluation on-demand
        group.MapPost("/evaluate/{farmId:guid}", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new EvaluateFarmAdvisoriesCommand(farmId);
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("EvaluateFarmAdvisories")
        .Produces<IReadOnlyList<ProactiveAdvisoryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

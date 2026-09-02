using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/farms").WithTags("Weather");

        // 1. GET /api/v1/farms/{farmId}/weather - Get farm weather forecast & risk analysis
        group.MapGet("/{farmId:guid}/weather", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (queryUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var resolvedRole = httpContext.ResolveUserRole(role, headerRole);
            var result = await mediator.Send(new GetFarmWeatherQuery(farmId, queryUserId, resolvedRole));
            return Results.Ok(result);
        })
        .WithName("GetFarmWeather")
        .Produces<FarmWeatherResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}

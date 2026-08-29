using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Queries;

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
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var result = await mediator.Send(new GetFarmWeatherQuery(farmId, queryUserId));
            return Results.Ok(result);
        })
        .WithName("GetFarmWeather")
        .Produces<FarmWeatherResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}

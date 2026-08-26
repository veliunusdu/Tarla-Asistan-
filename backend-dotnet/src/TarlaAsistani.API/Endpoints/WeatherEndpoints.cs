using MediatR;
using Microsoft.AspNetCore.Mvc;
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
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var queryUserId = headerUserId ?? userId ?? Guid.Empty;

            try
            {
                var result = await mediator.Send(new GetFarmWeatherQuery(farmId, queryUserId));
                return Results.Ok(result);
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
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("GetFarmWeather")
        .Produces<FarmWeatherResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}

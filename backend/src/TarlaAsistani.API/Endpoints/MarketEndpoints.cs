using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.Application.Features.Market.DTOs;
using TarlaAsistani.Application.Features.Market.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

/// <summary>
/// Çiftçiler ve ziraat mühendisleri için tarımsal piyasa verilerini (akaryakıt, gübre, mahsul, döviz) sunan uç noktalar.
/// </summary>
public static class MarketEndpoints
{
    public static IEndpointRouteBuilder MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/market")
                       .WithTags("Market");

        // GET /api/v1/market?category={category}
        group.MapGet("", async (
            [FromQuery] string? category,
            IMediator mediator,
            CancellationToken ct) =>
        {
            MarketCategory? parsedCategory = null;

            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!Enum.TryParse<MarketCategory>(category.Trim(), ignoreCase: true, out var catVal))
                {
                    return Results.Json(
                        new
                        {
                            detail = $"Geçersiz kategori: '{category}'. Desteklenen kategoriler: fuel, fertilizer, crop, fx."
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                parsedCategory = catVal;
            }

            var query = new GetMarketDataQuery(parsedCategory);
            var result = await mediator.Send(query, ct);

            return Results.Ok(result);
        })
        .WithName("GetMarketData")
        .WithTags("Market")
        .AllowAnonymous()
        .Produces<MarketDataResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}

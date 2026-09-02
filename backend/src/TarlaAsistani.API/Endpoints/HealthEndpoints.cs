using FirebaseAdmin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.API.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. GET /health - Comprehensive system health check
        app.MapGet("/health", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var dbOk = await db.Database.CanConnectAsync(ct);
            var result = new
            {
                status = dbOk ? "ok" : "degraded",
                database = dbOk ? "ok" : "unhealthy",
                firebase = FirebaseApp.DefaultInstance != null ? "ok" : "not_configured",
                timestamp = DateTime.UtcNow
            };

            return dbOk
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithTags("System")
        .WithName("SystemHealth")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        // 2. GET /health/live - Liveness probe (container/k8s)
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
        .WithTags("System")
        .WithName("SystemLiveness")
        .Produces(StatusCodes.Status200OK);

        // 3. GET /health/ready - Readiness probe (checks DB readiness)
        app.MapGet("/health/ready", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var dbOk = await db.Database.CanConnectAsync(ct);
            var result = new
            {
                status = dbOk ? "ok" : "degraded",
                database = dbOk ? "ok" : "unhealthy",
                firebase = FirebaseApp.DefaultInstance != null ? "ok" : "not_configured"
            };

            return dbOk
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithTags("System")
        .WithName("SystemReadiness")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}

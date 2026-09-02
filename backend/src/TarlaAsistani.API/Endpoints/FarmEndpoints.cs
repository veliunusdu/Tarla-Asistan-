using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Farms.Commands;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class FarmEndpoints
{
    public static IEndpointRouteBuilder MapFarmEndpoints(this IEndpointRouteBuilder app)
    {
        // Farms API Group
        var group = app.MapGroup("/api/v1/farms")
                       .WithTags("Farms");

        // 1. POST /api/v1/farms - Create a new farm
        group.MapPost("", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateFarmRequest req,
            IMediator mediator,
            IValidator<CreateFarmCommand> validator) =>
        {
            var ownerId = httpContext.ResolveUserId(req.OwnerId, headerUserId);
            if (ownerId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new CreateFarmCommand(
                ownerId, req.Name, req.Latitude, req.Longitude,
                req.SizeInHectares, req.IrrigationMethod,
                req.InitialCropType, req.InitialPlantedAt
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var farmId = await mediator.Send(command);
            return Results.Created($"/api/v1/farms/{farmId}", new { id = farmId });
        })
        .WithName("CreateFarm")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. GET /api/v1/farms - List active farms (tenant-isolated for farmers)
        group.MapGet("", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] bool? includeArchived,
            IMediator mediator) =>
        {
            var resolvedUserId = httpContext.ResolveUserId(userId, headerUserId);
            var resolvedRole = httpContext.ResolveUserRole(role, headerRole);

            var query = new GetFarmsQuery(resolvedUserId, resolvedRole, includeArchived ?? false);
            var farms = await mediator.Send(query);
            return Results.Ok(farms);
        })
        .WithName("GetFarms")
        .Produces<List<FarmDto>>(StatusCodes.Status200OK);

        // 2.1 GET /api/v1/farms/summary - Aggregate dashboard & overview summary
        group.MapGet("/summary", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            [FromQuery] int? upcomingLimit,
            IMediator mediator) =>
        {
            var resolvedUserId = httpContext.ResolveUserId(userId, headerUserId);
            var resolvedRole = httpContext.ResolveUserRole(role, headerRole);

            if (resolvedUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var clampedLimit = Math.Clamp(upcomingLimit ?? 5, 1, 20);
            var query = new GetFarmSummaryQuery(resolvedUserId, resolvedRole, clampedLimit);
            var summary = await mediator.Send(query);
            return Results.Ok(summary);
        })
        .WithName("GetFarmSummary")
        .Produces<FarmSummaryResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 3. GET /api/v1/farms/{id} - Get farm by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var resolvedUserId = httpContext.ResolveUserId(userId, headerUserId);
            var resolvedRole = httpContext.ResolveUserRole(role, headerRole);

            var query = new GetFarmByIdQuery(id, resolvedUserId, resolvedRole);
            var farm = await mediator.Send(query);
            return farm is not null ? Results.Ok(farm) : Results.NotFound();
        })
        .WithName("GetFarmById")
        .Produces<FarmDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 4. PATCH /api/v1/farms/{id} - Update farm
        group.MapPatch("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdateFarmRequest req,
            IMediator mediator,
            IValidator<UpdateFarmCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new UpdateFarmCommand(
                id,
                userId,
                req.Name,
                req.Latitude,
                req.Longitude,
                req.SizeInHectares,
                req.IrrigationMethod,
                req.SoilType,
                req.Note
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await mediator.Send(command);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("UpdateFarm")
        .Produces<FarmMutationResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        // 5. DELETE /api/v1/farms/{id} - Archive (Soft Delete) farm
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromHeader(Name = "X-User-Role")] string? headerRole,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var resolvedUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (resolvedUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var resolvedRole = httpContext.ResolveUserRole(role, headerRole);

            var command = new ArchiveFarmCommand(id, resolvedUserId, resolvedRole);
            var isArchived = await mediator.Send(command);
            return isArchived ? Results.NoContent() : Results.NotFound();
        })
        .WithName("ArchiveFarm")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

public record CreateFarmRequest(
    Guid? OwnerId,
    string Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    CropType InitialCropType,
    DateOnly InitialPlantedAt
);

public record UpdateFarmRequest(
    Guid? UserId,
    string? Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    string? SoilType,
    string? Note
);

using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Features.Farms.Commands;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class FarmEndpoints
{
    public static IEndpointRouteBuilder MapFarmEndpoints(this IEndpointRouteBuilder app)
    {
        // Health Check
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
           .WithTags("Health")
           .WithName("HealthCheck");

        // Farms API Group
        var group = app.MapGroup("/api/v1/farms")
                       .WithTags("Farms");

        // 1. POST /api/v1/farms - Create a new farm
        group.MapPost("/", async (CreateFarmRequest req, IMediator mediator, IValidator<CreateFarmCommand> validator) =>
        {
            var command = new CreateFarmCommand(
                req.OwnerId, req.Name, req.Latitude, req.Longitude,
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
        .ProducesValidationProblem();

        // 2. GET /api/v1/farms - List all active farms
        group.MapGet("/", async (IMediator mediator) =>
        {
            var query = new GetFarmsQuery();
            var farms = await mediator.Send(query);
            return Results.Ok(farms);
        })
        .WithName("GetFarms")
        .Produces<List<FarmDto>>(StatusCodes.Status200OK);

        // 3. GET /api/v1/farms/{id} - Get farm by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var farm = await mediator.Send(new GetFarmByIdQuery(id));
            return farm is not null ? Results.Ok(farm) : Results.NotFound();
        })
        .WithName("GetFarmById")
        .Produces<FarmDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 4. PATCH /api/v1/farms/{id} - Update farm
        group.MapPatch("/{id:guid}", async (Guid id, UpdateFarmRequest req, IMediator mediator, IValidator<UpdateFarmCommand> validator) =>
        {
            var command = new UpdateFarmCommand(
                id,
                req.UserId,
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
        .ProducesValidationProblem();

        // 5. DELETE /api/v1/farms/{id} - Archive (Soft Delete) farm
        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var command = new ArchiveFarmCommand(id);
            var isArchived = await mediator.Send(command);
            return isArchived ? Results.NoContent() : Results.NotFound();
        })
        .WithName("ArchiveFarm")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public record CreateFarmRequest(
    Guid OwnerId,
    string Name,
    double Latitude,
    double Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    CropType InitialCropType,
    DateOnly InitialPlantedAt
);

public record UpdateFarmRequest(
    Guid UserId,
    string? Name,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    IrrigationMethod? IrrigationMethod,
    string? SoilType,
    string? Note
);

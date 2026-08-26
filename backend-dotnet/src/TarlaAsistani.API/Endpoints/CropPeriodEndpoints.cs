using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.Application.Features.CropPeriods.Commands;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Application.Features.CropPeriods.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class CropPeriodEndpoints
{
    public static IEndpointRouteBuilder MapCropPeriodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/farms/{farmId:guid}/production-periods")
                       .WithTags("Crop Periods");

        // 1. GET /api/v1/farms/{farmId}/production-periods - List crop periods
        group.MapGet("/", async (
            Guid farmId,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = headerUserId ?? userId ?? Guid.Empty;
            var userRole = role ?? UserRole.Farmer;

            var result = await mediator.Send(new ListCropPeriodsQuery(farmId, queryUserId, userRole));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Tarla bulunamadı." });
        })
        .WithName("ListCropPeriods")
        .Produces<CropPeriodListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 2. POST /api/v1/farms/{farmId}/production-periods - Create new crop period
        group.MapPost("/", async (
            Guid farmId,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateCropPeriodApiRequest req,
            IMediator mediator,
            IValidator<CreateCropPeriodCommand> validator) =>
        {
            var userId = headerUserId ?? req.UserId;
            var command = new CreateCropPeriodCommand(
                FarmId: farmId,
                UserId: userId,
                CropType: req.CropType,
                Variety: req.Variety,
                PlantedAt: req.PlantedAt,
                CloseExisting: req.CloseExisting
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/farms/{farmId}/production-periods/{result.Id}", result);
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
        .WithName("CreateCropPeriod")
        .Produces<CropPeriodDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem();

        // 3. POST /api/v1/farms/{farmId}/production-periods/{periodId}/close - Close active crop period
        group.MapPost("/{periodId:guid}/close", async (
            Guid farmId,
            Guid periodId,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CloseCropPeriodApiRequest req,
            IMediator mediator,
            IValidator<CloseCropPeriodCommand> validator) =>
        {
            var userId = headerUserId ?? req.UserId;
            var command = new CloseCropPeriodCommand(
                FarmId: farmId,
                PeriodId: periodId,
                UserId: userId,
                HarvestedAt: req.HarvestedAt ?? DateOnly.FromDateTime(DateTime.UtcNow)
            );

            var validationResult = await validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Üretim dönemi bulunamadı." });
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
        .WithName("CloseCropPeriod")
        .Produces<CropPeriodDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem();

        return app;
    }
}

public record CreateCropPeriodApiRequest(
    Guid UserId,
    CropType CropType,
    string? Variety,
    DateOnly PlantedAt,
    bool CloseExisting = false
);

public record CloseCropPeriodApiRequest(
    Guid UserId,
    DateOnly? HarvestedAt
);

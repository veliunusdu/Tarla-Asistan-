using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
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
        group.MapGet("", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            var userRole = httpContext.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new ListCropPeriodsQuery(farmId, queryUserId, userRole));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { detail = "Tarla bulunamadı." });
        })
        .WithName("ListCropPeriods")
        .Produces<CropPeriodListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 2. POST /api/v1/farms/{farmId}/production-periods - Create new crop period
        group.MapPost("", async (
            Guid farmId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CreateCropPeriodApiRequest req,
            IMediator mediator,
            IValidator<CreateCropPeriodCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var cropName = !string.IsNullOrWhiteSpace(req.CropName)
                ? req.CropName.Trim()
                : (req.CropType.HasValue ? CropTypeHelper.ToTurkishName(req.CropType.Value) : string.Empty);

            var command = new CreateCropPeriodCommand(
                FarmId: farmId,
                UserId: userId,
                CropName: cropName,
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
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // 3. POST /api/v1/farms/{farmId}/production-periods/{periodId}/close - Close active crop period
        group.MapPost("/{periodId:guid}/close", async (
            Guid farmId,
            Guid periodId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            CloseCropPeriodApiRequest req,
            IMediator mediator,
            IValidator<CloseCropPeriodCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

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
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

[method: System.Text.Json.Serialization.JsonConstructor]
public record CreateCropPeriodApiRequest(
    Guid? UserId,
    string? CropName = null,
    CropType? CropType = null,
    string? Variety = null,
    DateOnly PlantedAt = default,
    bool CloseExisting = false
)
{
    public CreateCropPeriodApiRequest(
        Guid? userId,
        CropType cropType,
        string? variety,
        DateOnly plantedAt,
        bool closeExisting = false)
        : this(userId, CropTypeHelper.ToTurkishName(cropType), cropType, variety, plantedAt, closeExisting)
    {
    }
}

public record CloseCropPeriodApiRequest(
    Guid? UserId,
    DateOnly? HarvestedAt
);

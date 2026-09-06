using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Finance.Commands;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Application.Features.Finance.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class FinancialEndpoints
{
    public static IEndpointRouteBuilder MapFinancialEndpoints(this IEndpointRouteBuilder app)
    {
        var farmPeriods = app.MapGroup("/api/v1/farms/{farmId:guid}/production-periods/{periodId:guid}")
            .WithTags("Finance");
        var expenses = app.MapGroup("/api/v1/expenses").WithTags("Finance");
        var sales = app.MapGroup("/api/v1/sales").WithTags("Finance");

        farmPeriods.MapGet("/financial-summary", async (Guid farmId, Guid periodId, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, IMediator mediator) =>
            await SendRead<GetSeasonFinancialSummaryQuery, FinancialSummaryDto>(context, headerUserId, new GetSeasonFinancialSummaryQuery(farmId, periodId,
                context.ResolveUserId(null, headerUserId)), mediator))
            .WithName("GetFinancialSummary").Produces<FinancialSummaryDto>(200).Produces(404).Produces(401);

        farmPeriods.MapGet("/expenses", async (Guid farmId, Guid periodId, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, IMediator mediator) =>
            await SendRead<ListExpensesQuery, IReadOnlyList<ExpenseDto>>(context, headerUserId, new ListExpensesQuery(farmId, periodId,
                context.ResolveUserId(null, headerUserId)), mediator))
            .WithName("ListExpenses").Produces<IReadOnlyList<ExpenseDto>>(200).Produces(404).Produces(401);

        farmPeriods.MapPost("/expenses", async (Guid farmId, Guid periodId, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, CreateExpenseRequest request,
            IMediator mediator, IValidator<CreateExpenseCommand> validator) =>
            await CreateExpense(context, headerUserId, farmId, periodId, request, mediator, validator))
            .WithName("CreateExpense").Produces<ExpenseDto>(201).ProducesValidationProblem().Produces(404).Produces(401);

        expenses.MapPatch("/{id:guid}", async (Guid id, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, UpdateExpenseRequest request,
            IMediator mediator, IValidator<UpdateExpenseCommand> validator) =>
            await UpdateExpense(context, headerUserId, id, request, mediator, validator))
            .WithName("UpdateExpense").Produces<ExpenseDto>(200).ProducesValidationProblem().Produces(404).Produces(409);

        expenses.MapDelete("/{id:guid}", async (Guid id, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, IMediator mediator) =>
            await Archive(context, headerUserId, new ArchiveExpenseCommand(id,
                context.ResolveUserId(null, headerUserId)), mediator, "Gider bulunamadı."))
            .WithName("ArchiveExpense").Produces(204).Produces(404).Produces(409).Produces(401);

        farmPeriods.MapGet("/sales", async (Guid farmId, Guid periodId, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, IMediator mediator) =>
            await SendRead<ListCropSalesQuery, IReadOnlyList<CropSaleDto>>(context, headerUserId, new ListCropSalesQuery(farmId, periodId,
                context.ResolveUserId(null, headerUserId)), mediator))
            .WithName("ListCropSales").Produces<IReadOnlyList<CropSaleDto>>(200).Produces(404).Produces(401);

        farmPeriods.MapPost("/sales", async (Guid farmId, Guid periodId, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, CreateSaleRequest request,
            IMediator mediator, IValidator<CreateCropSaleCommand> validator) =>
            await CreateSale(context, headerUserId, farmId, periodId, request, mediator, validator))
            .WithName("CreateCropSale").Produces<CropSaleDto>(201).ProducesValidationProblem().Produces(404).Produces(401);

        sales.MapPatch("/{id:guid}", async (Guid id, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, UpdateSaleRequest request,
            IMediator mediator, IValidator<UpdateCropSaleCommand> validator) =>
            await UpdateSale(context, headerUserId, id, request, mediator, validator))
            .WithName("UpdateCropSale").Produces<CropSaleDto>(200).ProducesValidationProblem().Produces(404);

        sales.MapDelete("/{id:guid}", async (Guid id, HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId, IMediator mediator) =>
            await Archive(context, headerUserId, new ArchiveCropSaleCommand(id,
                context.ResolveUserId(null, headerUserId)), mediator, "Satış bulunamadı."))
            .WithName("ArchiveCropSale").Produces(204).Produces(404).Produces(401);

        return app;
    }

    private static async Task<IResult> SendRead<T, TResponse>(HttpContext context, Guid? headerUserId, T query, IMediator mediator)
        where T : IRequest<TResponse>
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        try { return Results.Ok(await mediator.Send(query)); }
        catch (KeyNotFoundException) { return Results.NotFound(new { detail = "Finansal kayıt bulunamadı." }); }
    }

    private static async Task<IResult> CreateExpense(HttpContext context, Guid? headerUserId, Guid farmId, Guid periodId,
        CreateExpenseRequest request, IMediator mediator, IValidator<CreateExpenseCommand> validator)
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        var command = new CreateExpenseCommand(farmId, periodId, userId, request.Category, request.Amount,
            request.OccurredAtUtc, request.Note, request.ClientOperationId);
        var validation = await validator.ValidateAsync(command);
        if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
        try { var result = await mediator.Send(command); return Results.Created($"/api/v1/expenses/{result.Id}", result); }
        catch (KeyNotFoundException) { return Results.NotFound(new { detail = "Finansal kayıt bulunamadı." }); }
    }

    private static async Task<IResult> UpdateExpense(HttpContext context, Guid? headerUserId, Guid id,
        UpdateExpenseRequest request, IMediator mediator, IValidator<UpdateExpenseCommand> validator)
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        var command = new UpdateExpenseCommand(id, userId, request.Category, request.Amount, request.OccurredAtUtc, request.Note);
        var validation = await validator.ValidateAsync(command);
        if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
        try { var result = await mediator.Send(command); return result is null ? Results.NotFound() : Results.Ok(result); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { detail = ex.Message }); }
    }

    private static async Task<IResult> CreateSale(HttpContext context, Guid? headerUserId, Guid farmId, Guid periodId,
        CreateSaleRequest request, IMediator mediator, IValidator<CreateCropSaleCommand> validator)
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        var command = new CreateCropSaleCommand(farmId, periodId, userId, request.HarvestQuantity, request.Unit,
            request.UnitPrice, request.SoldAt, request.BuyerOrMarketNote, request.ClientOperationId);
        var validation = await validator.ValidateAsync(command);
        if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
        try { var result = await mediator.Send(command); return Results.Created($"/api/v1/sales/{result.Id}", result); }
        catch (KeyNotFoundException) { return Results.NotFound(new { detail = "Finansal kayıt bulunamadı." }); }
    }

    private static async Task<IResult> UpdateSale(HttpContext context, Guid? headerUserId, Guid id,
        UpdateSaleRequest request, IMediator mediator, IValidator<UpdateCropSaleCommand> validator)
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        var command = new UpdateCropSaleCommand(id, userId, request.HarvestQuantity, request.Unit, request.UnitPrice,
            request.SoldAt, request.BuyerOrMarketNote);
        var validation = await validator.ValidateAsync(command);
        if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
        var result = await mediator.Send(command);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Archive<T>(HttpContext context, Guid? headerUserId, T command,
        IMediator mediator, string notFound) where T : IRequest<bool>
    {
        var userId = context.ResolveUserId(null, headerUserId);
        if (userId == Guid.Empty) return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: 401);
        return await mediator.Send(command) is true ? Results.NoContent() : Results.NotFound(new { detail = notFound });
    }
}

public record CreateExpenseRequest(ExpenseCategory Category, decimal Amount, DateTime OccurredAtUtc,
    string? Note = null, Guid? ClientOperationId = null);
public record UpdateExpenseRequest(ExpenseCategory Category, decimal Amount, DateTime OccurredAtUtc, string? Note = null);
public record CreateSaleRequest(decimal HarvestQuantity, string Unit, decimal UnitPrice, DateOnly SoldAt,
    string? BuyerOrMarketNote = null, Guid? ClientOperationId = null);
public record UpdateSaleRequest(decimal HarvestQuantity, string Unit, decimal UnitPrice, DateOnly SoldAt,
    string? BuyerOrMarketNote = null);

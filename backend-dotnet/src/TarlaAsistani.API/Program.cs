using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Features.Farms.Commands;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure (Database)
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add MediatR (Scans the Application assembly for Commands/Handlers)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateFarmCommand).Assembly));

// 3. Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- API ENDPOINTS ---

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// GET Farms
app.MapGet("/api/farms", async (ApplicationDbContext db) =>
{
    return await db.Farms
        .AsNoTracking()
        .Include(f => f.CropPeriods) // Include related data
        .OrderByDescending(f => f.CreatedAtUtc)
        .ToListAsync();
});

// POST Farm (The CQRS Command)
app.MapPost("/api/farms", async (CreateFarmRequest req, IMediator mediator) =>
{
    // Map HTTP Request to MediatR Command
    var command = new CreateFarmCommand(
        OwnerId: req.OwnerId, // Note: In real app, get this from Auth token
        Name: req.Name,
        Latitude: req.Latitude,
        Longitude: req.Longitude,
        SizeInHectares: req.SizeInHectares,
        IrrigationMethod: req.IrrigationMethod,
        InitialCropType: req.InitialCropType,
        InitialPlantedAt: req.InitialPlantedAt
    );

    // Send command to Handler
    var farmId = await mediator.Send(command);

    return Results.Created($"/api/farms/{farmId}", new { id = farmId });
});

app.Run();

// Request DTO (Data Transfer Object)
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
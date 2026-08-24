using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "tarla-asistani-dotnet"
    });
});

app.MapGet("/api/farms", async (ApplicationDbContext db) =>
{
    return await db.Farms
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .ToListAsync();
});

app.MapPost("/api/farms", async (CreateFarmRequest request, ApplicationDbContext db) =>
{
    var farm = new Farm
    {
        Name = request.Name,
        FarmerPhoneNumber = request.FarmerPhoneNumber
    };

    db.Farms.Add(farm);
    await db.SaveChangesAsync();

    return Results.Created($"/api/farms/{farm.Id}", farm);
});

app.Run();

public record CreateFarmRequest(string Name, string FarmerPhoneNumber);
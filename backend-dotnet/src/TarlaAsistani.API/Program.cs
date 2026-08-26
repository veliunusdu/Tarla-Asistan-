using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application;
using TarlaAsistani.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(); // Registers MediatR and Validators

// 2. Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. Map Modular Endpoints
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapMediaEndpoints();
app.MapNotificationEndpoints();
app.MapFarmEndpoints();
app.MapWeatherEndpoints();
app.MapActivityEndpoints();
app.MapCropPeriodEndpoints();
app.MapTaskEndpoints();
app.MapCaseEndpoints();
app.MapPilotEndpoints();
app.MapAIEndpoints();

app.Run();
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application;
using TarlaAsistani.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// 2. CORS Policy for Web & Mobile Clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 3. Register Application & Infrastructure Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 4. Swagger with Bearer & X-User-Id Security Definitions
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tarla Asistanı API",
        Version = "v1",
        Description = "Tarla Asistanı Backend - .NET 8 Clean Architecture"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("UserIdHeader", new OpenApiSecurityScheme
    {
        Description = "Pilot & Testing Header: X-User-Id containing User GUID",
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "UserIdHeader" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tarla Asistanı API v1");
    });
}

// 5. Map Modular Endpoints
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

public partial class Program { }
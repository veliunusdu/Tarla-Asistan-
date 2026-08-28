using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application;
using TarlaAsistani.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure JSON serialization (snake_case lower for keys, SNAKE_CASE UPPER for enums)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
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

// 4. Configure JWT Authentication & Authorization
var jwtSecret = builder.Configuration["Auth:JwtSecret"] 
             ?? builder.Configuration["Jwt:Secret"] 
             ?? builder.Configuration["JWT_SECRET"]
             ?? Environment.GetEnvironmentVariable("JWT_SECRET")
             ?? "super_secret_jwt_key_at_least_32_characters_long_for_hmac_sha256_production!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tarla Asistanı API",
        Version = "v1",
        Description = "Agricultural Decision Support System API - .NET 8 Re-implementation"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("UserIdHeader", new OpenApiSecurityScheme
    {
        Description = "Direct User ID header used in pilot & mobile modes (e.g. X-User-Id: <guid>)",
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

// 2.5 Register Global Exception Handler
builder.Services.AddExceptionHandler<TarlaAsistani.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<TarlaAsistani.API.Middleware.SecurityHeadersMiddleware>();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tarla Asistanı API v1");
    });
}

// 5. Map Modular Endpoints
app.MapHealthEndpoints();
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

// 6. Apply EF Core Migrations automatically on startup (in Production container or when AUTO_MIGRATE=true)
if (app.Environment.IsProduction() || Environment.GetEnvironmentVariable("AUTO_MIGRATE") == "true")
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<TarlaAsistani.Infrastructure.Persistence.ApplicationDbContext>();

    try
    {
        if (context.Database.IsRelational())
        {
            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

app.Run();

public partial class Program { }
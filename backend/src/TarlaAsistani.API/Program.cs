using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TarlaAsistani.API.Common;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application;
using TarlaAsistani.Domain.Enums;
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
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(section => section.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Cast<string>()
    .ToArray();

if ((builder.Environment.IsProduction() || builder.Environment.IsStaging()) && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins must be configured in Staging and Production.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .SetIsOriginAllowed(origin =>
                  {
                      if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                      {
                          return uri.Host == "localhost" || uri.Host == "127.0.0.1" || allowedOrigins.Contains(origin);
                      }
                      return false;
                  });
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// 2.2 Rate Limiting (User-based sliding window)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"detail\":\"Çok fazla istek gönderdiniz. Lütfen bir süre bekleyin.\"}", token);
    };

    options.AddPolicy("AiChatPerUser", httpContext =>
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? (httpContext.Request.Headers.TryGetValue("X-User-Id", out var hVal) ? hVal.ToString() : null)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 2,
            QueueLimit = 0
        });
    });
});

// 3. Register Application & Infrastructure Layers
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TarlaAsistani.Application.Common.Interfaces.ICurrentUserContext, TarlaAsistani.API.Common.HttpCurrentUserContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 4. Configure JWT Authentication & Authorization
var jwtSecret = builder.Configuration["Auth:JwtSecret"] 
             ?? builder.Configuration["Jwt:Secret"] 
             ?? builder.Configuration["JWT_SECRET"]
             ?? Environment.GetEnvironmentVariable("JWT_SECRET");

if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    {
        throw new InvalidOperationException("Auth:JwtSecret must be configured with at least 32 characters in Staging and Production.");
    }
}

jwtSecret ??= "development-only-jwt-secret-change-me-32-chars!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction() || builder.Environment.IsStaging();
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

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, TarlaAsistani.API.Common.ActiveRoleAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveRoleAssignment", policy => policy.Requirements.Add(new TarlaAsistani.API.Common.ActiveRoleAssignmentRequirement()));
    options.AddPolicy("FarmerContext", policy => policy.Requirements.Add(new TarlaAsistani.API.Common.ActiveRoleAssignmentRequirement(TarlaAsistani.Domain.Enums.UserRole.Farmer)));
    options.AddPolicy("AgronomistContext", policy => policy.Requirements.Add(new TarlaAsistani.API.Common.ActiveRoleAssignmentRequirement(TarlaAsistani.Domain.Enums.UserRole.Agronomist)));
    options.AddPolicy("AdminContext", policy => policy.Requirements.Add(new TarlaAsistani.API.Common.ActiveRoleAssignmentRequirement(TarlaAsistani.Domain.Enums.UserRole.Admin)));
});

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(type => type.FullName!.Replace('+', '.'));

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

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
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
app.UseCors("ConfiguredOrigins");
app.UseAuthentication();

if (app.Environment.EnvironmentName == "Testing")
{
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var userId = context.GetUserId();
            TarlaAsistani.Domain.Enums.UserRole? extractedRole = null;
            if (!userId.HasValue &&
                (context.Request.Method == HttpMethods.Post ||
                 context.Request.Method == HttpMethods.Patch ||
                 context.Request.Method == HttpMethods.Put) &&
                context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Request.EnableBuffering();
                using var reader = new System.IO.StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                var bodyText = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(bodyText);
                        if (doc.RootElement.TryGetProperty("user_id", out var uidProp) ||
                            doc.RootElement.TryGetProperty("userId", out uidProp) ||
                            doc.RootElement.TryGetProperty("owner_id", out uidProp) ||
                            doc.RootElement.TryGetProperty("ownerId", out uidProp))
                        {
                            if (uidProp.TryGetGuid(out var parsedGuid))
                            {
                                userId = parsedGuid;
                            }
                        }

                        if ((doc.RootElement.TryGetProperty("role", out var roleProp) ||
                             doc.RootElement.TryGetProperty("active_role", out roleProp) ||
                             doc.RootElement.TryGetProperty("activeRole", out roleProp)) &&
                            Enum.TryParse<TarlaAsistani.Domain.Enums.UserRole>(roleProp.GetString(), ignoreCase: true, out var parsedRole))
                        {
                            extractedRole = parsedRole;
                        }
                    }
                    catch
                    {
                        // Ignore malformed JSON in test middleware
                    }
                }
            }

            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                var role = extractedRole ?? context.GetUserRole();
                if (!role.HasValue)
                {
                    using var scope = context.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TarlaAsistani.Application.Common.Interfaces.IApplicationDbContext>();
                    role = db.UserRoleAssignments
                        .Where(ura => ura.UserId == userId.Value && ura.RevokedAtUtc == null)
                        .Select(ura => (TarlaAsistani.Domain.Enums.UserRole?)ura.Role)
                        .FirstOrDefault();
                }

                var claimsList = new List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, userId.Value.ToString())
                };

                if (role.HasValue)
                {
                    claimsList.Add(new(System.Security.Claims.ClaimTypes.Role, role.Value.ToString()));
                    claimsList.Add(new("active_role", role.Value.ToString()));
                }

                context.User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(claimsList, "TestHeaderAuth"));
            }
        }

        await next();
    });
}

app.UseRateLimiter();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        var isApiRequest = context.Request.Path.StartsWithSegments("/api/v1");
        var isPublicRequest = context.Request.Path.StartsWithSegments("/api/v1/auth/firebase") ||
                              context.Request.Path.StartsWithSegments("/api/v1/auth/refresh") ||
                              context.Request.Path.StartsWithSegments("/api/v1/auth/logout") ||
                              context.Request.Path.StartsWithSegments("/api/v1/market");

        if (isApiRequest && !isPublicRequest &&
            !HttpMethods.IsOptions(context.Request.Method) &&
            !(context.User.Identity?.IsAuthenticated ?? false))
        {
            await context.ChallengeAsync();
            return;
        }

        await next();
    });
}

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
app.MapProactiveAdvisoryEndpoints();
app.MapMarketEndpoints();
app.MapFinancialEndpoints();
app.MapAdminEndpoints();

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

// Initial Admin CLI command: dotnet run -- admin:create --firebase-uid <UID> [--operator <NAME>] [--reason <REASON>]
if (args.Length > 0 && (args[0] == "admin:create" || args.Contains("--create-admin")))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TarlaAsistani.Infrastructure.Persistence.ApplicationDbContext>();

    string? firebaseUid = null;
    string oper = "system-cli";
    string reason = "Initial system admin bootstrap";

    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--firebase-uid" || args[i] == "-u") && i + 1 < args.Length)
            firebaseUid = args[i + 1];
        else if ((args[i] == "--operator" || args[i] == "-o") && i + 1 < args.Length)
            oper = args[i + 1];
        else if ((args[i] == "--reason" || args[i] == "-r") && i + 1 < args.Length)
            reason = args[i + 1];
    }

    if (string.IsNullOrWhiteSpace(firebaseUid))
    {
        Console.Error.WriteLine("Hata: --firebase-uid belirtilmelidir.");
        return;
    }

    var user = await db.Users
        .Include(u => u.RoleAssignments)
        .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

    if (user == null)
    {
        Console.Error.WriteLine($"Hata: '{firebaseUid}' Firebase UID ile kullanıcı bulunamadı.");
        return;
    }

    var existingAdmin = user.RoleAssignments
        .FirstOrDefault(ra => ra.Role == TarlaAsistani.Domain.Enums.UserRole.Admin && ra.RevokedAtUtc == null);

    if (existingAdmin != null)
    {
        Console.WriteLine($"Kullanıcı ({user.Id}) zaten aktif ADMIN rolüne sahip. Değişiklik yapılmadı.");
        return;
    }

    user.RoleAssignments.Add(new TarlaAsistani.Domain.Entities.UserRoleAssignment
    {
        UserId = user.Id,
        Role = TarlaAsistani.Domain.Enums.UserRole.Admin,
        GrantedAtUtc = DateTime.UtcNow,
        GrantReason = $"{reason} (Operatör: {oper})"
    });

    await db.SaveChangesAsync();
    Console.WriteLine($"Başarılı: '{firebaseUid}' kullanıcısına ADMIN rolü atandı. (UserId: {user.Id}, Operatör: {oper})");
    return;
}

app.Run();

public partial class Program { }

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.Items["RequestId"]?.ToString() 
                   ?? httpContext.TraceIdentifier;

        _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}. Message: {Message}", traceId, exception.Message);

        var (statusCode, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            ArgumentException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, string.IsNullOrWhiteSpace(exception.Message) ? "Kimlik doğrulanmadı veya yetkisiz işlem." : exception.Message),
            _ => (StatusCodes.Status500InternalServerError, _env.IsDevelopment() || _env.EnvironmentName == "Testing"
                ? exception.Message 
                : "Beklenmeyen bir sunucu hatası oluştu. Lütfen daha sonra tekrar deneyiniz.")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            detail,
            status = statusCode,
            trace_id = traceId
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}

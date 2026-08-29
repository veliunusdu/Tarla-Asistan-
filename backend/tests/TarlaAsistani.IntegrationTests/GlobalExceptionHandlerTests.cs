using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.API.Middleware;

namespace TarlaAsistani.IntegrationTests;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _mockLogger = new();
    private readonly Mock<IHostEnvironment> _mockEnv = new();

    public GlobalExceptionHandlerTests()
    {
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Testing");
    }

    [Fact]
    public async Task TryHandleAsync_WhenKeyNotFoundException_ShouldReturn404Json()
    {
        // Arrange
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new KeyNotFoundException("Kayıt bulunamadı.");

        // Act
        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().StartWith("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("detail").GetString().Should().Be("Kayıt bulunamadı.");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.TryGetProperty("trace_id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_WhenUnauthorizedAccessException_ShouldReturn401Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new UnauthorizedAccessException("Yetkisiz işlem.");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TryHandleAsync_WhenArgumentException_ShouldReturn422Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new ArgumentException("Geçersiz parametre.");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TryHandleAsync_WhenInvalidOperationException_ShouldReturn409Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new InvalidOperationException("Çakışan durum.");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task TryHandleAsync_WhenGenericExceptionInProduction_ShouldHideSensitiveDetails()
    {
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new Exception("PostgreSQL NpgsqlException password=super_secret_db_pass");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);

        var detail = doc.RootElement.GetProperty("detail").GetString();
        detail.Should().NotContain("super_secret_db_pass");
        detail.Should().NotContain("PostgreSQL");
        detail.Should().Be("Beklenmeyen bir sunucu hatası oluştu. Lütfen daha sonra tekrar deneyiniz.");
    }

    [Fact]
    public async Task TryHandleAsync_WhenFarmNotFoundException_ShouldReturn404Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var farmId = Guid.NewGuid();
        var ex = new TarlaAsistani.Domain.Exceptions.FarmNotFoundException(farmId);

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("detail").GetString().Should().Contain(farmId.ToString());
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_WhenDuplicateTaskException_ShouldReturn409Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new TarlaAsistani.Domain.Exceptions.DuplicateTaskException("task-123-abc", isKey: true);

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task TryHandleAsync_WhenCropPeriodMismatchException_ShouldReturn422Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new TarlaAsistani.Domain.Exceptions.CropPeriodMismatchException("Üretim dönemi bu tarlaya ait değil.");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TryHandleAsync_WhenForbiddenException_ShouldReturn403Json()
    {
        var handler = new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new TarlaAsistani.Domain.Exceptions.ForbiddenException("Erişim engellendi.");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}

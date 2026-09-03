using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

public class AIAgentWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "AIAgentTestDb_" + Guid.NewGuid().ToString("N");

    public Mock<IWeatherProvider> MockWeatherProvider { get; } = new();
    public Mock<IAIAgentProvider> MockAIAgentProvider { get; } = new();
    public Mock<IAIChatProvider> MockAIChatProvider { get; } = new();
    public Mock<IFirebaseAuthService> MockFirebaseAuthService { get; } = new();
    public Mock<IPushNotificationService> MockPushService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:JwtSecret", "integration-test-jwt-secret-change-me-32-chars!");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("FIREBASE_AUTH_ENABLED", "false");
        builder.UseSetting("AI:Provider", "gemini");
        builder.UseSetting("AI:AgentEnabled", "true");

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var weatherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWeatherProvider));
            if (weatherDescriptor != null) services.Remove(weatherDescriptor);
            services.AddSingleton(MockWeatherProvider.Object);

            var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIChatProvider));
            if (aiDescriptor != null) services.Remove(aiDescriptor);
            services.AddSingleton(MockAIChatProvider.Object);

            var aiAgentDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIAgentProvider));
            if (aiAgentDescriptor != null) services.Remove(aiAgentDescriptor);
            services.AddSingleton(MockAIAgentProvider.Object);

            var firebaseAuthDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFirebaseAuthService));
            if (firebaseAuthDescriptor != null) services.Remove(firebaseAuthDescriptor);
            services.AddSingleton(MockFirebaseAuthService.Object);

            var pushDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPushNotificationService));
            if (pushDescriptor != null) services.Remove(pushDescriptor);
            services.AddSingleton(MockPushService.Object);
        });
    }
}

public class AIAgentChatIntegrationTests : IClassFixture<AIAgentWebApplicationFactory>
{
    private readonly AIAgentWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AIAgentChatIntegrationTests(AIAgentWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid FarmerId, Guid FarmId)> SeedFarmerAndFarmAsync(string farmName)
    {
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Id = farmerId,
            PhoneNumber = $"+90554{Random.Shared.Next(1000000, 9999999)}",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(user);

        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = farmName,
            Latitude = 39.925533,
            Longitude = 32.866287,
            SizeInHectares = 10.0,
            IrrigationMethod = IrrigationMethod.Drip
        };
        db.Farms.Add(farm);

        await db.SaveChangesAsync();

        return (farmerId, farmId);
    }

    [Fact]
    public async Task Chat_AgentReadFlow_ExecutesTools_ReturnsSuccess()
    {
        // Arrange
        var (farmerId, farmId) = await SeedFarmerAndFarmAsync("Kuzey Tarlası");

        var turn = 0;
        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgentRequest req, CancellationToken _) =>
            {
                turn++;
                if (turn == 1)
                {
                    using var doc = JsonDocument.Parse("{}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_1", "list_farms", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }
                if (turn == 2)
                {
                    using var doc = JsonDocument.Parse($"{{\"farm_id\":\"{farmId}\"}}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_2", "get_weather", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }

                return new AIAgentResponse(
                    content: "Kuzey Tarlası'nda hava açık ve sıcaklık 22 derecedir.",
                    toolCalls: null,
                    finishReason: AIAgentFinishReason.Stop,
                    promptTokens: 120,
                    completionTokens: 25,
                    totalTokens: 145);
            });

        _factory.MockWeatherProvider
            .Setup(w => w.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecastData(
                new List<WeatherPoint> { new(DateTime.UtcNow, 22.0, 40, 0, 10, 50, 1) },
                new CurrentWeatherDto(DateTime.UtcNow, 22.0, 22.0, 50.0, 10.0, null, "Açık", 1),
                null));

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Kuzey tarlasında hava nasıl?" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().Be("Kuzey Tarlası'nda hava açık ve sıcaklık 22 derecedir.");
        body.PromptTokens.Should().Be(120);
        body.CompletionTokens.Should().Be(25);
        body.TotalTokens.Should().Be(145);

        // Verify no internal tool/provider metadata is exposed
        var rawJson = await res.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("call_1");
        rawJson.Should().NotContain("call_2");
        rawJson.Should().NotContain("ProviderMetadata");
        rawJson.Should().NotContain("thought_signature");
        rawJson.Should().NotContain("deepseek_reasoning_content");
    }

    [Fact]
    public async Task Chat_AgentWriteFlow_CreatesRealTaskInDatabase_BeforeFinalResponse()
    {
        // Arrange
        var (farmerId, farmId) = await SeedFarmerAndFarmAsync("Kuzey Tarlası");

        var turn = 0;
        AIToolResult? capturedToolResult = null;
        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgentRequest req, CancellationToken _) =>
            {
                turn++;
                if (turn == 1)
                {
                    using var doc = JsonDocument.Parse("{}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_1", "list_farms", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }
                if (turn == 2)
                {
                    using var doc = JsonDocument.Parse($@"{{
                        ""farm_id"": ""{farmId}"",
                        ""title"": ""Damlama sulama"",
                        ""due_date"": ""2026-09-04"",
                        ""priority"": ""high"",
                        ""description"": ""Yarın için sulama planı""
                    }}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_2", "create_task", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }

                var lastMsg = req.Messages.LastOrDefault();
                capturedToolResult = lastMsg?.ToolResult;

                return new AIAgentResponse(
                    content: "Tamam, Kuzey Tarlası için yarına damlama sulama görevi oluşturdum.",
                    toolCalls: null,
                    finishReason: AIAgentFinishReason.Stop,
                    promptTokens: 200,
                    completionTokens: 30,
                    totalTokens: 230);
            });

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Yarın Kuzey tarlasına damlama sulama görevi oluştur." }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert HTTP
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().Be("Tamam, Kuzey Tarlası için yarına damlama sulama görevi oluşturdum.");

        capturedToolResult.Should().NotBeNull();
        capturedToolResult!.IsSuccess.Should().BeTrue(capturedToolResult.ErrorMessage);

        // Assert Database: Task MUST exist with authentic attributes committed
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tasks = await db.FarmTasks.Where(t => t.FarmId == farmId).ToListAsync();

        tasks.Should().HaveCount(1);
        var task = tasks[0];
        task.Title.Should().Be("Damlama sulama");
        task.DueDate.Should().Be(new DateOnly(2026, 9, 4));
        task.Priority.Should().Be(TaskPriority.High);
        task.CreatedById.Should().Be(farmerId);
        task.Source.Should().Be(TaskSource.Manual);
    }

    [Fact]
    public async Task Chat_AgentWriteFlow_UnauthorizedFarm_DoesNotCreateTask()
    {
        // Arrange
        var (farmerA, _) = await SeedFarmerAndFarmAsync("Çiftçi A Tarlası");
        var (farmerB, farmBId) = await SeedFarmerAndFarmAsync("Güney Tarlası");

        var turn = 0;
        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgentRequest req, CancellationToken _) =>
            {
                turn++;
                if (turn == 1)
                {
                    using var doc = JsonDocument.Parse($@"{{
                        ""farm_id"": ""{farmBId}"",
                        ""title"": ""Yetkisiz sulama görevi"",
                        ""due_date"": ""2026-09-04""
                    }}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_1", "create_task", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }

                return new AIAgentResponse(
                    content: "Bu tarlaya erişim izniniz bulunmadığı için görev oluşturulamadı.",
                    toolCalls: null,
                    finishReason: AIAgentFinishReason.Stop);
            });

        // Farmer A sends request attempting to write to Farmer B's farm
        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Güney tarlasına görev ekle" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerA.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body!.Reply.Should().Contain("oluşturulamadı");

        // Database MUST NOT contain any task on Farm B
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var taskExists = await db.FarmTasks.AnyAsync(t => t.FarmId == farmBId);
        taskExists.Should().BeFalse();
    }

    [Fact]
    public async Task Chat_ProviderFailure_Returns503SafeError_WithoutLeakingSecrets()
    {
        // Arrange
        var farmerId = Guid.NewGuid();

        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated 500 error from remote LLM: sk_live_secret_key"));

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Merhaba" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await res.Content.ReadAsStringAsync();

        // Must not leak internal exceptions or secrets
        content.Should().NotContain("sk_live_secret_key");
        content.Should().NotContain("HttpRequestException");
        content.Should().Contain("AI hizmeti şu anda kullanılamıyor.");
    }

    [Fact]
    public async Task Chat_DuplicateTask_ReportsTruthfulExplanation_DoesNotCreateDuplicateRow()
    {
        // Arrange
        var (farmerId, farmId) = await SeedFarmerAndFarmAsync("Kuzey Tarlası");

        // Pre-create the task in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new TarlaAsistani.Application.Features.Tasks.Commands.CreateExpertTaskCommand(
                FarmId: farmId,
                CreatedById: farmerId,
                CreatedByRole: UserRole.Farmer,
                Title: "Damlama sulama",
                Description: "Haftalık sulama",
                Reason: "Toprak nemi azaldı",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.High,
                DueDate: new DateOnly(2026, 9, 4)));
        }

        var turn = 0;
        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgentRequest req, CancellationToken _) =>
            {
                turn++;
                if (turn == 1)
                {
                    using var doc = JsonDocument.Parse($@"{{
                        ""farm_id"": ""{farmId}"",
                        ""title"": ""Damlama sulama"",
                        ""due_date"": ""2026-09-04"",
                        ""description"": ""Haftalık sulama"",
                        ""reason"": ""Toprak nemi azaldı""
                    }}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_1", "create_task", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }

                return new AIAgentResponse(
                    content: "Bu görev zaten mevcut olduğu için tekrar oluşturulmadı.",
                    toolCalls: null,
                    finishReason: AIAgentFinishReason.Stop);
            });

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Sulama görevi ekle" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body!.Reply.Should().Contain("zaten mevcut");

        // Still exactly ONE task row in database
        using var scopeCheck = _factory.Services.CreateScope();
        var dbCheck = scopeCheck.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await dbCheck.FarmTasks.CountAsync(t => t.FarmId == farmId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Chat_StreamingAgent_CreatesTaskAndYieldsSSEChunks()
    {
        // Arrange
        var (farmerId, farmId) = await SeedFarmerAndFarmAsync("Kuzey Tarlası");

        var turn = 0;
        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIAgentProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgentRequest req, CancellationToken _) =>
            {
                turn++;
                if (turn == 1)
                {
                    using var doc = JsonDocument.Parse($@"{{
                        ""farm_id"": ""{farmId}"",
                        ""title"": ""Budama"",
                        ""due_date"": ""2026-09-04""
                    }}");
                    return new AIAgentResponse(
                        content: null,
                        toolCalls: new[] { new AIToolCall("call_1", "create_task", doc.RootElement.Clone()) },
                        finishReason: AIAgentFinishReason.ToolCalls);
                }

                return new AIAgentResponse(
                    content: "Budama görevi oluşturuldu.",
                    toolCalls: null,
                    finishReason: AIAgentFinishReason.Stop,
                    promptTokens: 180,
                    completionTokens: 20,
                    totalTokens: 200);
            });

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat/stream")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Budama görevi ekle" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await _client.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var sseText = await res.Content.ReadAsStringAsync();

        sseText.Should().Contain("Budama görevi oluşturuldu.");
        sseText.Should().Contain("[DONE]");

        // Verify task was actually created in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = await db.FarmTasks.FirstOrDefaultAsync(t => t.FarmId == farmId && t.Title == "Budama");
        task.Should().NotBeNull();
        task!.DueDate.Should().Be(new DateOnly(2026, 9, 4));
    }

    [Fact]
    public async Task Chat_LocalProvider_UsesPassiveChat_DoesNotExecuteAgent()
    {
        // Arrange
        var localFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AI:Provider", "local");
        });
        var localClient = localFactory.CreateClient();

        var farmerId = Guid.NewGuid();

        _factory.MockAIAgentProvider.Reset();

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Merhaba" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await localClient.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().NotBeNullOrWhiteSpace();

        // Agent provider should NEVER have been called
        _factory.MockAIAgentProvider.Verify(
            p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_AgentDisabled_UsesPassiveChat_DoesNotExecuteAgent()
    {
        // Arrange
        var disabledFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AI:Provider", "gemini");
            builder.UseSetting("AI:AgentEnabled", "false");
        });
        var disabledClient = disabledFactory.CreateClient();

        var farmerId = Guid.NewGuid();

        _factory.MockAIAgentProvider.Reset();
        _factory.MockAIChatProvider.Reset();
        _factory.MockAIChatProvider
            .Setup(p => p.GenerateAsync(It.IsAny<AIChatRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIChatResponseDto("Pasif mod yanıtı", Guid.NewGuid().ToString("N")));

        var reqMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { message = "Merhaba" }),
                Encoding.UTF8,
                "application/json")
        };
        reqMessage.Headers.Add("X-User-Id", farmerId.ToString());

        // Act
        var res = await disabledClient.SendAsync(reqMessage);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AIChatResponseDto>(CustomWebApplicationFactory.JsonOptions);
        body!.Reply.Should().Be("Pasif mod yanıtı");

        // Agent provider should NEVER have been called
        _factory.MockAIAgentProvider.Verify(
            p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.Application;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Tools;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Queries;
using TarlaAsistani.Domain.Enums;
using Xunit;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

public class ReadOnlyAIToolsTests
{
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public UserRole? Role { get; set; } = UserRole.Farmer;
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
    }

    // =========================================================================
    // 1. ListFarmsTool Tests
    // =========================================================================

    [Fact]
    public void ListFarmsTool_Definition_HasCorrectNameAndExposesNoTrustedIdentityArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new ListFarmsTool(mediatorMock.Object, userContext);

        tool.Name.Should().Be("list_farms");
        tool.Definition.Name.Should().Be("list_farms");
        tool.Definition.Description.Should().Contain("Lists farms accessible to the authenticated user");

        var schema = tool.Definition.ParametersSchema;
        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.GetProperty("properties").EnumerateObject().Should().BeEmpty();

        var schemaRaw = schema.GetRawText();
        schemaRaw.Should().NotContain("user_id");
        schemaRaw.Should().NotContain("role");
    }

    [Fact]
    public async Task ListFarmsTool_ExecutesSuccessfully_PassingTrustedIdentityToGetFarmsQuery()
    {
        var mediatorMock = new Mock<IMediator>();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var farmId = Guid.NewGuid();
        var dummyFarms = new List<FarmDto>
        {
            new(
                Id: farmId,
                OwnerId: userId,
                Name: "Kuzey Tarlası",
                Latitude: 39.9,
                Longitude: 32.8,
                SizeInHectares: 12.5,
                IrrigationMethod: IrrigationMethod.Drip,
                SoilType: "Clay",
                Note: null,
                ArchivedAt: null,
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: null,
                CurrentCropPeriod: new CropPeriodDto(
                    Id: Guid.NewGuid(),
                    FarmId: farmId,
                    CropName: "Domates",
                    CropType: CropType.Tomato,
                    Variety: "Cherry",
                    PlantedAt: new DateOnly(2026, 4, 1),
                    HarvestedAt: null,
                    Status: CropPeriodStatus.Active,
                    CreatedAtUtc: DateTime.UtcNow,
                    UpdatedAtUtc: DateTime.UtcNow
                )
            )
        };

        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == userId && q.Role == UserRole.Farmer && !q.IncludeArchived), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyFarms);

        var tool = new ListFarmsTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_1", "list_farms", "{}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.CallId.Should().Be("call_1");
        result.ToolName.Should().Be("list_farms");

        var content = result.GetContentString();
        content.Should().Contain("Kuzey Tarlası");
        content.Should().Contain("Domates");
        content.Should().Contain("Cherry");
    }

    [Fact]
    public async Task ListFarmsTool_AttackerSupplyingIdentityArguments_DoesNotOverrideTrustedIdentity()
    {
        var mediatorMock = new Mock<IMediator>();
        var trustedUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = trustedUserId, Role = UserRole.Farmer };

        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == trustedUserId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FarmDto>());

        var tool = new ListFarmsTool(mediatorMock.Object, userContext);
        // Attacker attempts to pass a different user_id and role inside model arguments
        var maliciousJson = $"{{\"user_id\": \"{attackerUserId}\", \"role\": \"Agronomist\"}}";
        var call = AIToolCall.Create("call_sec", "list_farms", maliciousJson);

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mediatorMock.Verify(
            m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == trustedUserId && q.Role == UserRole.Farmer), It.IsAny<CancellationToken>()),
            Times.Once);
        mediatorMock.Verify(
            m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == attackerUserId), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListFarmsTool_WhenUserHasNoFarms_ReturnsSuccessfulEmptyCollection()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetFarmsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FarmDto>());

        var tool = new ListFarmsTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_empty", "list_farms", "{}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var content = result.GetContentString();
        content.Should().Contain("\"count\":0");
    }

    [Fact]
    public async Task ListFarmsTool_WhenUserIsUnauthenticated_ReturnsUnauthenticatedFailure()
    {
        var mediatorMock = new Mock<IMediator>();
        var unauthContext = new FakeCurrentUserContext { UserId = null, Role = null };

        var tool = new ListFarmsTool(mediatorMock.Object, unauthContext);
        var call = AIToolCall.Create("call_unauth", "list_farms", "{}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("unauthenticated");
        mediatorMock.Verify(m => m.Send(It.IsAny<GetFarmsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 2. GetWeatherTool Tests
    // =========================================================================

    [Fact]
    public void GetWeatherTool_Definition_RequiresFarmIdAndExposesNoIdentityOrCoordinates()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new GetWeatherTool(mediatorMock.Object, userContext);

        tool.Name.Should().Be("get_weather");
        tool.Definition.Description.Should().Contain("Returns current weather conditions and forecast");

        var schema = tool.Definition.ParametersSchema;
        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().Contain("farm_id");

        var schemaRaw = schema.GetRawText();
        schemaRaw.Should().NotContain("latitude");
        schemaRaw.Should().NotContain("longitude");
        schemaRaw.Should().NotContain("user_id");
        schemaRaw.Should().NotContain("role");
    }

    [Fact]
    public async Task GetWeatherTool_WithValidFarmId_SendsQueryAndReturnsStructuredWeather()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var dummyWeather = new FarmWeatherResponseDto(
            FarmId: farmId,
            Provider: "open_meteo",
            FetchedAt: DateTime.UtcNow,
            IsStale: false,
            StaleReason: null,
            Points: new List<WeatherPoint>(),
            Risks: new List<WeatherRiskDto>
            {
                new("Frost", "Medium", DateTime.UtcNow, DateTime.UtcNow.AddHours(6), "Düşük sıcaklık riski", "Önlem alın")
            },
            Current: new CurrentWeatherDto(DateTime.UtcNow, 22.5, 21.0, 45.0, 12.0, null, "Güneşli", 0),
            Daily: new List<DailyForecastDto>
            {
                new(new DateOnly(2026, 9, 4), 14.0, 26.0, 10.0, 0.0, "Parçalı Bulutlu", 1)
            }
        );

        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmWeatherQuery>(q => q.FarmId == farmId && q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyWeather);

        var tool = new GetWeatherTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_w1", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.CallId.Should().Be("call_w1");
        result.ToolName.Should().Be("get_weather");

        var content = result.GetContentString();
        content.Should().Contain("22.5");
        content.Should().Contain("Güneşli");
        content.Should().Contain("Düşük sıcaklık riski");
    }

    [Fact]
    public async Task GetWeatherTool_WithInvalidGuid_ReturnsInvalidArgumentsWithoutCallingMediator()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new GetWeatherTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_bad_id", "get_weather", "{\"farm_id\":\"banana\"}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        mediatorMock.Verify(m => m.Send(It.IsAny<GetFarmWeatherQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWeatherTool_WithMissingFarmId_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new GetWeatherTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_missing", "get_weather", "{}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
    }

    [Fact]
    public async Task GetWeatherTool_WhenFarmNotFound_ReturnsSafeFarmNotFoundFailure()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetFarmWeatherQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Tarla bulunamadı."));

        var tool = new GetWeatherTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_nf", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("farm_not_found");
        result.ErrorMessage.Should().Contain("not found or is not accessible");
    }

    [Fact]
    public async Task GetWeatherTool_Cancellation_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetFarmWeatherQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var tool = new GetWeatherTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_cancel", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}");

        var act = () => tool.ExecuteAsync(call, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =========================================================================
    // 3. GetTasksTool Tests
    // =========================================================================

    [Fact]
    public void GetTasksTool_Definition_RequiresFarmIdOptionalDateAndNoIdentity()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new GetTasksTool(mediatorMock.Object, userContext);

        tool.Name.Should().Be("get_tasks");
        var schema = tool.Definition.ParametersSchema;
        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().Contain("farm_id");
        schema.GetProperty("properties").TryGetProperty("date", out _).Should().BeTrue();

        var schemaRaw = schema.GetRawText();
        schemaRaw.Should().NotContain("user_id");
        schemaRaw.Should().NotContain("role");
    }

    [Fact]
    public async Task GetTasksTool_WithFarmIdOnly_CallsListFarmTasksQuery()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var dummyTasks = new List<TaskDto>
        {
            new(
                Id: Guid.NewGuid(),
                FarmId: farmId,
                CropPeriodId: null,
                CreatedById: userId,
                Title: "Damlama Borularını Kontrol Et",
                Description: "Basınç testi yap",
                Reason: "Rutin bakım",
                Priority: TaskPriority.High,
                Status: TaskStatus.Planned,
                Source: TaskSource.Manual,
                Confidence: TaskConfidence.High,
                DueDate: new DateOnly(2026, 9, 5),
                NotAppliedReason: null,
                CompletionNote: null,
                PhotoUrl: null,
                ViewedAtUtc: null,
                CompletedAtUtc: null,
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: DateTime.UtcNow,
                ExpertReviewRecommended: false
            )
        };

        mediatorMock
            .Setup(m => m.Send(It.Is<ListFarmTasksQuery>(q => q.FarmId == farmId && q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyTasks);

        var tool = new GetTasksTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_t1", "get_tasks", $"{{\"farm_id\":\"{farmId}\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.CallId.Should().Be("call_t1");
        var content = result.GetContentString();
        content.Should().Contain("Damlama Borularını Kontrol Et");
        content.Should().Contain("High");

        mediatorMock.Verify(m => m.Send(It.IsAny<ListFarmTasksQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        mediatorMock.Verify(m => m.Send(It.IsAny<ListDailyTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTasksTool_WithFarmIdAndValidIsoDate_CallsListDailyTasksQuery()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var targetDate = new DateOnly(2026, 9, 4);
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var dailyTasks = new DailyTaskListDto(
            Date: targetDate,
            Items: new List<TaskDto>
            {
                new(
                    Id: Guid.NewGuid(),
                    FarmId: farmId,
                    CropPeriodId: null,
                    CreatedById: userId,
                    Title: "Sulama vanasını aç",
                    Description: "Sabah 6'da açılacak",
                    Reason: "Su ihtiyacı",
                    Priority: TaskPriority.Medium,
                    Status: TaskStatus.Planned,
                    Source: TaskSource.Manual,
                    Confidence: TaskConfidence.High,
                    DueDate: targetDate,
                    NotAppliedReason: null,
                    CompletionNote: null,
                    PhotoUrl: null,
                    ViewedAtUtc: null,
                    CompletedAtUtc: null,
                    CreatedAtUtc: DateTime.UtcNow,
                    UpdatedAtUtc: DateTime.UtcNow,
                    ExpertReviewRecommended: false
                )
            },
            CriticalWeatherAlerts: new List<TaskDto>(),
            Overdue: new List<TaskDto>()
        );

        mediatorMock
            .Setup(m => m.Send(It.Is<ListDailyTasksQuery>(q => q.FarmId == farmId && q.UserId == userId && q.TargetDate == targetDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyTasks);

        var tool = new GetTasksTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_t2", "get_tasks", $"{{\"farm_id\":\"{farmId}\", \"date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var content = result.GetContentString();
        content.Should().Contain("Sulama vanasını aç");
        content.Should().Contain("2026-09-04");

        mediatorMock.Verify(m => m.Send(It.IsAny<ListDailyTasksQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        mediatorMock.Verify(m => m.Send(It.IsAny<ListFarmTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTasksTool_WithInvalidDate_ReturnsInvalidArgumentsWithoutCallingMediator()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        var tool = new GetTasksTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_bad_date", "get_tasks", $"{{\"farm_id\":\"{farmId}\", \"date\":\"tomorrow\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("YYYY-MM-DD");
        mediatorMock.Verify(m => m.Send(It.IsAny<ListDailyTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        mediatorMock.Verify(m => m.Send(It.IsAny<ListFarmTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTasksTool_WhenFarmNotFound_ReturnsFarmNotFound()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<ListFarmTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TaskDto>?)null);

        var tool = new GetTasksTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_t_nf", "get_tasks", $"{{\"farm_id\":\"{farmId}\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("farm_not_found");
    }

    // =========================================================================
    // 4. Registry Integration Test
    // =========================================================================

    [Fact]
    public void AgentToolRegistry_RegisteredViaDI_ExposesListFarms_GetWeather_GetTasks()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        // Stub external dependencies
        services.AddScoped(_ => new Mock<IMediator>().Object);
        services.AddScoped<ICurrentUserContext>(_ => new FakeCurrentUserContext());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IAgentToolRegistry>();
        var definitions = registry.GetToolDefinitions();

        definitions.Select(d => d.Name).Should().Contain(new[] { "list_farms", "get_weather", "get_tasks" });
    }

    // =========================================================================
    // 5. Orchestrator Integration-Style Composition Test
    // =========================================================================

    [Fact]
    public async Task Orchestrator_MultiTurnComposition_ListFarmsThenGetWeather_ProducesFinalResponse()
    {
        var mediatorMock = new Mock<IMediator>();
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        // 1. Setup GetFarmsQuery handler
        var dummyFarms = new List<FarmDto>
        {
            new(
                Id: farmId,
                OwnerId: userId,
                Name: "Kuzey Tarlası",
                Latitude: 39.9,
                Longitude: 32.8,
                SizeInHectares: 10.0,
                IrrigationMethod: IrrigationMethod.Drip,
                SoilType: null,
                Note: null,
                ArchivedAt: null,
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: null,
                CurrentCropPeriod: null
            )
        };

        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyFarms);

        // 2. Setup GetFarmWeatherQuery handler
        var dummyWeather = new FarmWeatherResponseDto(
            FarmId: farmId,
            Provider: "open_meteo",
            FetchedAt: DateTime.UtcNow,
            IsStale: false,
            StaleReason: null,
            Points: new List<WeatherPoint>(),
            Risks: new List<WeatherRiskDto>(),
            Current: new CurrentWeatherDto(DateTime.UtcNow, 24.0, 23.5, 40.0, 10.0, null, "Açık", 0),
            Daily: new List<DailyForecastDto>()
        );

        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmWeatherQuery>(q => q.FarmId == farmId && q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyWeather);

        // 3. Register tools in registry
        var listFarmsTool = new ListFarmsTool(mediatorMock.Object, userContext);
        var getWeatherTool = new GetWeatherTool(mediatorMock.Object, userContext);
        var getTasksTool = new GetTasksTool(mediatorMock.Object, userContext);
        var registry = new AgentToolRegistry(new IAgentTool[] { listFarmsTool, getWeatherTool, getTasksTool });

        // 4. Simulate Fake AI Provider sequence:
        // Turn 1: AI calls list_farms to resolve "Kuzey Tarlası"
        var turn1Response = AIAgentResponse.CreateToolCallsResponse(
            new[] { AIToolCall.Create("call_1", "list_farms", "{}") },
            "Tarlalarınızı listeliyorum.");

        // Turn 2: AI uses the resolved farmId to call get_weather
        var turn2Response = AIAgentResponse.CreateToolCallsResponse(
            new[] { AIToolCall.Create("call_2", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}") },
            "Kuzey Tarlası için hava durumunu sorguluyorum.");

        // Turn 3: AI produces the final answer
        var turn3Response = AIAgentResponse.CreateTextResponse(
            "Kuzey Tarlası'nda hava şu an açık ve sıcaklık 24°C.");

        var providerCalls = new List<AIAgentRequest>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                providerCalls.Add(req);
                return Task.FromResult(providerCalls.Count switch
                {
                    1 => turn1Response,
                    2 => turn2Response,
                    _ => turn3Response
                });
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, registry);

        // Run the agent with user prompt: "Kuzey tarlasında hava nasıl?"
        var result = await orchestrator.RunAsync("Kuzey tarlasında hava nasıl?");

        // Verification
        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Kuzey Tarlası'nda hava şu an açık ve sıcaklık 24°C.");
        result.Iterations.Should().Be(3);
        providerCalls.Should().HaveCount(3);

        // Verify conversation history contains all turns:
        // User -> Assistant(list_farms) -> Tool(farms) -> Assistant(get_weather) -> Tool(weather) -> Assistant(final)
        result.Messages.Should().HaveCount(6);
        result.Messages[0].Role.Should().Be(AIAgentRole.User);
        result.Messages[1].Role.Should().Be(AIAgentRole.Assistant);
        result.Messages[1].ToolCalls.First().ToolName.Should().Be("list_farms");
        result.Messages[2].Role.Should().Be(AIAgentRole.Tool);
        result.Messages[2].ToolResult!.ToolName.Should().Be("list_farms");
        result.Messages[2].ToolResult!.IsSuccess.Should().BeTrue();
        result.Messages[3].Role.Should().Be(AIAgentRole.Assistant);
        result.Messages[3].ToolCalls.First().ToolName.Should().Be("get_weather");
        result.Messages[4].Role.Should().Be(AIAgentRole.Tool);
        result.Messages[4].ToolResult!.ToolName.Should().Be("get_weather");
        result.Messages[4].ToolResult!.IsSuccess.Should().BeTrue();
        result.Messages[5].Role.Should().Be(AIAgentRole.Assistant);
        result.Messages[5].Content.Should().Be("Kuzey Tarlası'nda hava şu an açık ve sıcaklık 24°C.");
    }
}

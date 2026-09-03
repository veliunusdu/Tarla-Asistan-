using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using TarlaAsistani.Application;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Tools;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Application.Features.Weather.Queries;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Services;
using TarlaAsistani.Infrastructure.Services.AI.Gemini;
using Xunit;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class GeminiAIAgentProviderTests
{
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public UserRole? Role { get; set; } = UserRole.Farmer;
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
    }

    private static IConfiguration CreateConfig(string apiKey = "test-gemini-key", string model = "gemini-1.5-flash")
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "gemini",
            ["AI:GeminiApiKey"] = apiKey,
            ["AI:GeminiModel"] = model
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static (GeminiAIAgentProvider Provider, List<HttpRequestMessage> Requests) CreateProviderWithMockHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        string apiKey = "test-gemini-key",
        string model = "gemini-1.5-flash")
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                requests.Add(req);
                return Task.FromResult(responseFactory(req));
            });

        var client = new HttpClient(handlerMock.Object);
        var config = CreateConfig(apiKey, model);
        var provider = new GeminiAIAgentProvider(client, config);

        return (provider, requests);
    }

    // =========================================================================
    // 1. Tool Declaration & Schema Sanitization Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ToolDeclarationSerialization_CapturesAndSanitizesWithoutMutatingOriginal()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var dummyResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new[] { new { text = "Anladım." } }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(dummyResponse), Encoding.UTF8, "application/json")
            };
        });

        var mediatorMock = new Mock<IMediator>();
        var userCtx = new FakeCurrentUserContext();

        var listFarmsTool = new ListFarmsTool(mediatorMock.Object, userCtx);
        var getWeatherTool = new GetWeatherTool(mediatorMock.Object, userCtx);
        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userCtx);

        // Verify original definitions contain additionalProperties before call
        listFarmsTool.Definition.ParametersSchema.GetRawText().Should().Contain("additionalProperties");
        createTaskTool.Definition.ParametersSchema.GetRawText().Should().Contain("minLength");

        var tools = new[] { listFarmsTool.Definition, getWeatherTool.Definition, createTaskTool.Definition };
        var request = new AIAgentRequest(
            messages: new[] { AIAgentMessage.CreateUser("Merhaba") },
            tools: tools,
            systemPrompt: "Sen bir ziraat asistanısın.");

        var response = await provider.GenerateResponseAsync(request);
        response.Content.Should().Be("Anladım.");

        capturedBody.Should().NotBeNull();
        using var requestDoc = JsonDocument.Parse(capturedBody!);
        var root = requestDoc.RootElement;

        // Verify system instruction
        root.TryGetProperty("system_instruction", out var sysProp).Should().BeTrue();
        sysProp.GetProperty("parts")[0].GetProperty("text").GetString().Should().Be("Sen bir ziraat asistanısın.");

        // Verify tools exist
        root.TryGetProperty("tools", out var toolsProp).Should().BeTrue();
        var funcDecls = toolsProp[0].GetProperty("functionDeclarations");
        funcDecls.GetArrayLength().Should().Be(3);

        var names = funcDecls.EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();
        names.Should().Contain(new[] { "list_farms", "get_weather", "create_task" });

        // Verify create_task parameters schema
        var createDecl = funcDecls.EnumerateArray().First(f => f.GetProperty("name").GetString() == "create_task");
        var sanitizedParamsJson = createDecl.GetProperty("parameters").GetRawText();

        // Gemini sanitized schema must NOT contain additionalProperties or minLength
        sanitizedParamsJson.Should().NotContain("additionalProperties");
        sanitizedParamsJson.Should().NotContain("minLength");

        // But must preserve required, properties, enum
        sanitizedParamsJson.Should().Contain("\"farm_id\"");
        sanitizedParamsJson.Should().Contain("\"title\"");
        sanitizedParamsJson.Should().Contain("\"due_date\"");
        sanitizedParamsJson.Should().Contain("\"critical\"");

        // Crucial invariant: Original tool definition must remain unmutated
        createTaskTool.Definition.ParametersSchema.GetRawText().Should().Contain("additionalProperties");
        createTaskTool.Definition.ParametersSchema.GetRawText().Should().Contain("minLength");
    }

    // =========================================================================
    // 2. Normal Chat Response Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_NormalChatResponse_ReturnsTextAndStopFinishReason()
    {
        var (provider, requests) = CreateProviderWithMockHandler(_ =>
        {
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new[] { new { text = "Merhaba, size nasıl yardımcı olabilirim?" } }
                        },
                        finishReason = "STOP"
                    }
                },
                usageMetadata = new
                {
                    promptTokenCount = 15,
                    candidatesTokenCount = 10,
                    totalTokenCount = 25
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Selam") });
        var response = await provider.GenerateResponseAsync(request);

        response.Content.Should().Be("Merhaba, size nasıl yardımcı olabilirim?");
        response.HasToolCalls.Should().BeFalse();
        response.FinishReason.Should().Be(AIAgentFinishReason.Stop);
        response.PromptTokens.Should().Be(15);
        response.CompletionTokens.Should().Be(10);
        response.TotalTokens.Should().Be(25);

        requests.Should().HaveCount(1);
        requests[0].Headers.GetValues("x-goog-api-key").First().Should().Be("test-gemini-key");
    }

    // =========================================================================
    // 3. Single Function Call Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_SingleFunctionCall_ReturnsToolCallWithStructuredArgsAndId()
    {
        var farmId = Guid.NewGuid().ToString();
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new object[]
                            {
                                new
                                {
                                    functionCall = new
                                    {
                                        id = "call-123",
                                        name = "get_weather",
                                        args = new { farm_id = farmId }
                                    }
                                }
                            }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Hava durumu nedir?") });
        var response = await provider.GenerateResponseAsync(request);

        response.HasToolCalls.Should().BeTrue();
        response.ToolCalls.Should().HaveCount(1);

        var toolCall = response.ToolCalls[0];
        toolCall.CallId.Should().Be("call-123");
        toolCall.ToolName.Should().Be("get_weather");
        toolCall.Arguments.GetProperty("farm_id").GetString().Should().Be(farmId);
        response.FinishReason.Should().Be(AIAgentFinishReason.ToolCalls);
    }

    // =========================================================================
    // 4. Multiple Function Calls Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_MultipleFunctionCalls_PreservesOriginalOrder()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new object[]
                            {
                                new
                                {
                                    functionCall = new
                                    {
                                        id = "call-1",
                                        name = "list_farms",
                                        args = new { }
                                    }
                                },
                                new
                                {
                                    functionCall = new
                                    {
                                        id = "call-2",
                                        name = "get_weather",
                                        args = new { farm_id = "00000000-0000-0000-0000-000000000001" }
                                    }
                                }
                            }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Tarlaları ve havayı getir") });
        var response = await provider.GenerateResponseAsync(request);

        response.ToolCalls.Should().HaveCount(2);
        response.ToolCalls[0].ToolName.Should().Be("list_farms");
        response.ToolCalls[0].CallId.Should().Be("call-1");
        response.ToolCalls[1].ToolName.Should().Be("get_weather");
        response.ToolCalls[1].CallId.Should().Be("call-2");
    }

    // =========================================================================
    // 5. History Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_HistoryRoundTrip_SerializesModelCallAndFunctionResponseCorrectly()
    {
        string? capturedSecondBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedSecondBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new[] { new { text = "Kuzey Tarlası'nda hava 25 derece." } }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var farmId = Guid.NewGuid().ToString();
        var toolCall = AIToolCall.Create("call-weather-42", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}");
        var toolResult = AIToolResult.Success("call-weather-42", "get_weather", new { temp_c = 25.0, condition = "Açık" });

        var messages = new List<AIAgentMessage>
        {
            AIAgentMessage.CreateUser("Kuzey tarlasında hava nasıl?"),
            AIAgentMessage.CreateAssistant(null, new[] { toolCall }),
            AIAgentMessage.CreateToolResult(toolResult)
        };

        var request = new AIAgentRequest(messages);
        var response = await provider.GenerateResponseAsync(request);

        response.Content.Should().Be("Kuzey Tarlası'nda hava 25 derece.");

        capturedSecondBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedSecondBody!);
        var contents = doc.RootElement.GetProperty("contents");

        // Turn 0: User
        contents[0].GetProperty("role").GetString().Should().Be("user");
        contents[0].GetProperty("parts")[0].GetProperty("text").GetString().Should().Be("Kuzey tarlasında hava nasıl?");

        // Turn 1: Model (assistant function call)
        contents[1].GetProperty("role").GetString().Should().Be("model");
        var fc = contents[1].GetProperty("parts")[0].GetProperty("functionCall");
        fc.GetProperty("name").GetString().Should().Be("get_weather");
        fc.GetProperty("id").GetString().Should().Be("call-weather-42");
        fc.GetProperty("args").GetProperty("farm_id").GetString().Should().Be(farmId);

        // Turn 2: User (functionResponse)
        contents[2].GetProperty("role").GetString().Should().Be("user");
        var fr = contents[2].GetProperty("parts")[0].GetProperty("functionResponse");
        fr.GetProperty("name").GetString().Should().Be("get_weather");
        fr.GetProperty("id").GetString().Should().Be("call-weather-42");
        fr.GetProperty("response").GetProperty("temp_c").GetDouble().Should().Be(25.0);
    }

    // =========================================================================
    // 6. Thought Signature Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ThoughtSignatureRoundTrip_PreservesOpaqueStateWithoutExposingIt()
    {
        // 1. First request returns a function call with thoughtSignature
        var (provider1, _) = CreateProviderWithMockHandler(_ =>
        {
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new object[]
                            {
                                new
                                {
                                    functionCall = new
                                    {
                                        id = "call-think-1",
                                        name = "list_farms",
                                        args = new { }
                                    },
                                    thoughtSignature = "AQ==encrypted-thought-token"
                                }
                            }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var firstRequest = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Tarlalarımı listele") });
        var firstResponse = await provider1.GenerateResponseAsync(firstRequest);

        firstResponse.HasToolCalls.Should().BeTrue();
        var call = firstResponse.ToolCalls[0];
        call.ProviderMetadata.Should().NotBeNull();
        call.ProviderMetadata!["thought_signature"].Should().Be("AQ==encrypted-thought-token");

        // Invariant: signature must NOT be exposed in user-facing content
        firstResponse.Content.Should().BeNull();

        // 2. Convert to assistant message (as orchestrator does)
        var assistantMsg = firstResponse.ToAssistantMessage();
        var toolResult = AIToolResult.Success("call-think-1", "list_farms", new { count = 1 });
        var toolMsg = AIAgentMessage.CreateToolResult(toolResult);

        // 3. Second request passes conversation history back to provider
        string? secondRequestBody = null;
        var (provider2, _) = CreateProviderWithMockHandler(req =>
        {
            secondRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new[] { new { text = "1 adet tarlanız var." } }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var secondRequest = new AIAgentRequest(new[] { firstRequest.Messages[0], assistantMsg, toolMsg });
        var secondResponse = await provider2.GenerateResponseAsync(secondRequest);
        secondResponse.Content.Should().Be("1 adet tarlanız var.");

        secondRequestBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(secondRequestBody!);
        var modelTurn = doc.RootElement.GetProperty("contents")[1];
        var part = modelTurn.GetProperty("parts")[0];

        // Must preserve the exact thoughtSignature on the functionCall part
        part.GetProperty("thoughtSignature").GetString().Should().Be("AQ==encrypted-thought-token");
    }

    // =========================================================================
    // 7. Tool Failure Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ToolFailureRoundTrip_SendsStructuredFailureInFunctionResponse()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new[] { new { text = "Tarla bulunamadı, lütfen adı kontrol edin." } }
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var failedResult = AIToolResult.Failure(
            callId: "call-fail-99",
            toolName: "get_weather",
            errorMessage: "Farm was not found or is not accessible to the current user.",
            errorCode: "farm_not_found");

        var messages = new[]
        {
            AIAgentMessage.CreateUser("Hava durumu getir"),
            AIAgentMessage.CreateAssistant(null, new[] { AIToolCall.Create("call-fail-99", "get_weather", "{}") }),
            AIAgentMessage.CreateToolResult(failedResult)
        };

        await provider.GenerateResponseAsync(new AIAgentRequest(messages));

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var userTurn = doc.RootElement.GetProperty("contents")[2];
        var fr = userTurn.GetProperty("parts")[0].GetProperty("functionResponse");

        fr.GetProperty("name").GetString().Should().Be("get_weather");
        fr.GetProperty("id").GetString().Should().Be("call-fail-99");

        var responseObj = fr.GetProperty("response");
        responseObj.GetProperty("success").GetBoolean().Should().BeFalse();
        responseObj.GetProperty("error_code").GetString().Should().Be("farm_not_found");
        responseObj.GetProperty("error_message").GetString().Should().Contain("Farm was not found");
    }

    // =========================================================================
    // 8. Missing API Key & Error Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_WhenApiKeyMissing_ThrowsInvalidOperationException()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK), apiKey: "");

        var act = () => provider.GenerateResponseAsync(new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Test") }));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*API key is not configured*");
    }

    [Fact]
    public async Task Orchestrator_WhenGeminiApiKeyMissing_ReturnsAgentProviderError()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK), apiKey: "");
        var registry = new AgentToolRegistry(Enumerable.Empty<IAgentTool>());
        var orchestrator = new AIAgentOrchestrator(provider, registry);

        var result = await orchestrator.RunAsync("Merhaba");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_provider_error");
    }

    [Fact]
    public async Task Orchestrator_WhenHttpErrorOccurs_MapsToAgentProviderErrorWithoutExposingSecrets()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("Resource has been exhausted (rate limit).", Encoding.UTF8, "text/plain")
        });

        var registry = new AgentToolRegistry(Enumerable.Empty<IAgentTool>());
        var orchestrator = new AIAgentOrchestrator(provider, registry);

        var result = await orchestrator.RunAsync("Merhaba");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_provider_error");
        result.ErrorMessage.Should().NotContain("test-gemini-key");
    }

    [Fact]
    public async Task GenerateResponseAsync_Cancellation_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var (provider, _) = CreateProviderWithMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var act = () => provider.GenerateResponseAsync(
            new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Test") }),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Orchestrator_WhenGeminiReturnsEmptyContent_ProducesAgentEmptyResponse()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = Array.Empty<object>()
                        },
                        finishReason = "STOP"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var registry = new AgentToolRegistry(Enumerable.Empty<IAgentTool>());
        var orchestrator = new AIAgentOrchestrator(provider, registry);

        var result = await orchestrator.RunAsync("Test");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_empty_response");
    }

    // =========================================================================
    // 9. Dependency Injection & Configuration Tests
    // =========================================================================

    [Fact]
    public void AddInfrastructure_WhenProviderIsGemini_RegistersBothIAIAgentProviderAndIAIChatProvider()
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI_CHAT_PROVIDER"] = "gemini",
            ["AI:GeminiApiKey"] = "fake-key",
            ["AI:GeminiModel"] = "gemini-1.5-flash"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        // 1. IAIAgentProvider resolves to GeminiAIAgentProvider
        var agentProvider = scope.ServiceProvider.GetService<IAIAgentProvider>();
        agentProvider.Should().NotBeNull();
        agentProvider.Should().BeOfType<GeminiAIAgentProvider>();

        // 2. IAIChatProvider still resolves to GeminiAIChatProvider (backwards compatible)
        var chatProvider = scope.ServiceProvider.GetService<IAIChatProvider>();
        chatProvider.Should().NotBeNull();
        chatProvider.Should().BeOfType<GeminiAIChatProvider>();
    }

    // =========================================================================
    // 10. Orchestrator Multi-Turn Composition Test (Read-Only)
    // =========================================================================

    [Fact]
    public async Task Orchestrator_CompositionWithRealGeminiAdapterShape_ReadOnlyFlow_Succeeds()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userCtx = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        // 1. Setup MediatR GetFarmsQuery
        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FarmDto>
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
            });

        // 2. Setup MediatR GetFarmWeatherQuery
        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmWeatherQuery>(q => q.FarmId == farmId && q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FarmWeatherResponseDto(
                FarmId: farmId,
                Provider: "open_meteo",
                FetchedAt: DateTime.UtcNow,
                IsStale: false,
                StaleReason: null,
                Points: new List<WeatherPoint>(),
                Risks: new List<WeatherRiskDto>(),
                Current: new CurrentWeatherDto(DateTime.UtcNow, 24.0, 23.5, 40.0, 10.0, null, "Açık", 0),
                Daily: new List<DailyForecastDto>()
            ));

        var listFarmsTool = new ListFarmsTool(mediatorMock.Object, userCtx);
        var getWeatherTool = new GetWeatherTool(mediatorMock.Object, userCtx);
        var registry = new AgentToolRegistry(new IAgentTool[] { listFarmsTool, getWeatherTool });

        var turn = 0;
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            turn++;
            object geminiResponse = turn switch
            {
                // Turn 1: Model calls list_farms
                1 => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new object[]
                                {
                                    new
                                    {
                                        functionCall = new
                                        {
                                            id = "call-turn-1",
                                            name = "list_farms",
                                            args = new { }
                                        }
                                    }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                },
                // Turn 2: Model calls get_weather with resolved farmId
                2 => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new object[]
                                {
                                    new
                                    {
                                        functionCall = new
                                        {
                                            id = "call-turn-2",
                                            name = "get_weather",
                                            args = new { farm_id = farmId.ToString() }
                                        }
                                    }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                },
                // Turn 3: Model responds with final text
                _ => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new[]
                                {
                                    new { text = "Kuzey Tarlası'nda hava açık ve sıcaklık 24 derece." }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var orchestrator = new AIAgentOrchestrator(provider, registry);
        var result = await orchestrator.RunAsync("Kuzey tarlasında hava nasıl?");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Kuzey Tarlası'nda hava açık ve sıcaklık 24 derece.");
        result.Iterations.Should().Be(3);
        turn.Should().Be(3);

        result.Messages.Should().HaveCount(6);
        result.Messages[1].ToolCalls.First().ToolName.Should().Be("list_farms");
        result.Messages[3].ToolCalls.First().ToolName.Should().Be("get_weather");
    }

    // =========================================================================
    // 11. Orchestrator Multi-Turn Composition Test (Write Task)
    // =========================================================================

    [Fact]
    public async Task Orchestrator_CompositionWithRealGeminiAdapterShape_WriteTaskFlow_Succeeds()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userCtx = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        // 1. Setup MediatR GetFarmsQuery
        mediatorMock
            .Setup(m => m.Send(It.Is<GetFarmsQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FarmDto>
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
            });

        // 2. Setup MediatR CreateExpertTaskCommand
        var sampleDto = new TaskDto(
            Id: Guid.NewGuid(),
            FarmId: farmId,
            CropPeriodId: null,
            CreatedById: userId,
            Title: "Damlama sulama",
            Description: "Damlama sulama",
            Reason: "Kullanıcı talebi.",
            Priority: TaskPriority.Medium,
            Status: TaskStatus.New,
            Source: TaskSource.Manual,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 9, 4),
            NotAppliedReason: null,
            CompletionNote: null,
            PhotoUrl: null,
            ViewedAtUtc: null,
            CompletedAtUtc: null,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            ExpertReviewRecommended: false
        );

        mediatorMock
            .Setup(m => m.Send(It.Is<CreateExpertTaskCommand>(c => c.FarmId == farmId && c.CreatedById == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleDto);

        var listFarmsTool = new ListFarmsTool(mediatorMock.Object, userCtx);
        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userCtx);
        var registry = new AgentToolRegistry(new IAgentTool[] { listFarmsTool, createTaskTool });

        var turn = 0;
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            turn++;
            object geminiResponse = turn switch
            {
                // Turn 1: Model calls list_farms
                1 => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new object[]
                                {
                                    new
                                    {
                                        functionCall = new
                                        {
                                            id = "call-wt-1",
                                            name = "list_farms",
                                            args = new { }
                                        }
                                    }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                },
                // Turn 2: Model calls create_task
                2 => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new object[]
                                {
                                    new
                                    {
                                        functionCall = new
                                        {
                                            id = "call-wt-2",
                                            name = "create_task",
                                            args = new
                                            {
                                                farm_id = farmId.ToString(),
                                                title = "Damlama sulama",
                                                due_date = "2026-09-04"
                                            }
                                        }
                                    }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                },
                // Turn 3: Final confirmation
                _ => new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                role = "model",
                                parts = new[]
                                {
                                    new { text = "Tamam, Kuzey Tarlası için damlama sulama görevi oluşturdum." }
                                }
                            },
                            finishReason = "STOP"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse), Encoding.UTF8, "application/json")
            };
        });

        var orchestrator = new AIAgentOrchestrator(provider, registry);
        var result = await orchestrator.RunAsync("Yarın Kuzey tarlasına damlama sulama görevi oluştur.");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Tamam, Kuzey Tarlası için damlama sulama görevi oluşturdum.");
        result.Iterations.Should().Be(3);

        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Messages[4].ToolResult!.GetContentString().Should().Contain("\"created\":true");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
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
using TarlaAsistani.Domain.Exceptions;
using TarlaAsistani.Infrastructure;
using TarlaAsistani.Infrastructure.Services;
using TarlaAsistani.Infrastructure.Services.AI.DeepSeek;
using TarlaAsistani.Infrastructure.Services.AI.Gemini;
using Xunit;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class DeepSeekAIAgentProviderTests
{
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public UserRole? Role { get; set; } = UserRole.Farmer;
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
    }

    private static IConfiguration CreateConfig(
        string apiKey = "test-deepseek-key",
        string model = "deepseek-chat",
        string baseUrl = "https://api.deepseek.com")
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "deepseek",
            ["AI:DeepSeekApiKey"] = apiKey,
            ["AI:DeepSeekModel"] = model,
            ["AI:DeepSeekBaseUrl"] = baseUrl
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static (DeepSeekAIAgentProvider Provider, List<HttpRequestMessage> Requests) CreateProviderWithMockHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        string apiKey = "test-deepseek-key",
        string model = "deepseek-chat",
        string baseUrl = "https://api.deepseek.com")
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
        var config = CreateConfig(apiKey, model, baseUrl);
        var provider = new DeepSeekAIAgentProvider(client, config);

        return (provider, requests);
    }

    // =========================================================================
    // 1. Tool Declaration Serialization Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ToolDeclarationSerialization_CapturesSchemasWithoutMutatingOriginal()
    {
        string? capturedBody = null;
        var (provider, requests) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var dummyResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Anladım."
                        },
                        finish_reason = "stop"
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

        var originalListFarmsRaw = listFarmsTool.Definition.ParametersSchema.GetRawText();
        var originalCreateTaskRaw = createTaskTool.Definition.ParametersSchema.GetRawText();

        var tools = new[] { listFarmsTool.Definition, getWeatherTool.Definition, createTaskTool.Definition };
        var request = new AIAgentRequest(
            messages: new[] { AIAgentMessage.CreateUser("Merhaba") },
            tools: tools,
            systemPrompt: "Sen bir ziraat asistanısın.");

        var response = await provider.GenerateResponseAsync(request);
        response.Content.Should().Be("Anladım.");

        requests.Should().HaveCount(1);
        requests[0].Headers.Authorization.Should().NotBeNull();
        requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        requests[0].Headers.Authorization!.Parameter.Should().Be("test-deepseek-key");

        capturedBody.Should().NotBeNull();
        using var requestDoc = JsonDocument.Parse(capturedBody!);
        var root = requestDoc.RootElement;

        // Verify model
        root.GetProperty("model").GetString().Should().Be("deepseek-chat");

        // Verify system prompt in messages
        var messages = root.GetProperty("messages");
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("Sen bir ziraat asistanısın.");

        // Verify tools exist in OpenAI format
        root.TryGetProperty("tools", out var toolsProp).Should().BeTrue();
        toolsProp.GetArrayLength().Should().Be(3);

        var toolList = toolsProp.EnumerateArray().ToList();
        toolList.All(t => t.GetProperty("type").GetString() == "function").Should().BeTrue();

        var names = toolList.Select(t => t.GetProperty("function").GetProperty("name").GetString()).ToList();
        names.Should().Contain(new[] { "list_farms", "get_weather", "create_task" });

        // Verify tool_choice is auto
        root.GetProperty("tool_choice").GetString().Should().Be("auto");

        // Verify create_task parameters schema preserved
        var createDecl = toolList.First(t => t.GetProperty("function").GetProperty("name").GetString() == "create_task");
        var createParamsJson = createDecl.GetProperty("function").GetProperty("parameters").GetRawText();
        createParamsJson.Should().Contain("\"farm_id\"");
        createParamsJson.Should().Contain("\"title\"");
        createParamsJson.Should().Contain("\"due_date\"");
        createParamsJson.Should().Contain("\"critical\"");

        // Invariant: Original definitions must remain identical
        listFarmsTool.Definition.ParametersSchema.GetRawText().Should().Be(originalListFarmsRaw);
        createTaskTool.Definition.ParametersSchema.GetRawText().Should().Be(originalCreateTaskRaw);
    }

    // =========================================================================
    // 2. Normal Text Response Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_NormalTextResponse_MapsContentAndStopFinishReason()
    {
        var (provider, requests) = CreateProviderWithMockHandler(_ =>
        {
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Merhaba, size nasıl yardımcı olabilirim?"
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 20,
                    completion_tokens = 12,
                    total_tokens = 32
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Selam") });
        var response = await provider.GenerateResponseAsync(request);

        response.Content.Should().Be("Merhaba, size nasıl yardımcı olabilirim?");
        response.HasToolCalls.Should().BeFalse();
        response.FinishReason.Should().Be(AIAgentFinishReason.Stop);
        response.PromptTokens.Should().Be(20);
        response.CompletionTokens.Should().Be(12);
        response.TotalTokens.Should().Be(32);

        requests.Should().HaveCount(1);
        requests[0].RequestUri!.ToString().Should().Be("https://api.deepseek.com/chat/completions");
    }

    // =========================================================================
    // 3. Single Tool Call Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_SingleToolCall_ReturnsParsedToolCallWithArguments()
    {
        var farmId = Guid.NewGuid().ToString();
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = (string?)null,
                            tool_calls = new object[]
                            {
                                new
                                {
                                    id = "call_123",
                                    type = "function",
                                    function = new
                                    {
                                        name = "get_weather",
                                        arguments = $"{{\"farm_id\":\"{farmId}\"}}"
                                    }
                                }
                            }
                        },
                        finish_reason = "tool_calls"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Hava durumu getir") });
        var response = await provider.GenerateResponseAsync(request);

        response.HasToolCalls.Should().BeTrue();
        response.ToolCalls.Should().HaveCount(1);

        var toolCall = response.ToolCalls[0];
        toolCall.CallId.Should().Be("call_123");
        toolCall.ToolName.Should().Be("get_weather");
        toolCall.Arguments.GetProperty("farm_id").GetString().Should().Be(farmId);
        response.FinishReason.Should().Be(AIAgentFinishReason.ToolCalls);
    }

    // =========================================================================
    // 4. Multiple Tool Calls Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_MultipleToolCalls_PreservesOriginalOrderAndIds()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = (string?)null,
                            tool_calls = new object[]
                            {
                                new
                                {
                                    id = "call_1",
                                    type = "function",
                                    function = new
                                    {
                                        name = "list_farms",
                                        arguments = "{}"
                                    }
                                },
                                new
                                {
                                    id = "call_2",
                                    type = "function",
                                    function = new
                                    {
                                        name = "get_weather",
                                        arguments = "{\"farm_id\":\"00000000-0000-0000-0000-000000000001\"}"
                                    }
                                }
                            }
                        },
                        finish_reason = "tool_calls"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var request = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Tarlaları ve havayı getir") });
        var response = await provider.GenerateResponseAsync(request);

        response.ToolCalls.Should().HaveCount(2);
        response.ToolCalls[0].ToolName.Should().Be("list_farms");
        response.ToolCalls[0].CallId.Should().Be("call_1");
        response.ToolCalls[1].ToolName.Should().Be("get_weather");
        response.ToolCalls[1].CallId.Should().Be("call_2");
    }

    // =========================================================================
    // 5. Arguments Wire Format & Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ArgumentsWireFormat_InboundStringBecomesJsonElement_OutboundBecomesJsonString()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "İşlem tamamlandı."
                        },
                        finish_reason = "stop"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var farmId = Guid.NewGuid().ToString();
        var toolCall = AIToolCall.Create("call_weather_42", "get_weather", $"{{\"farm_id\":\"{farmId}\"}}");
        toolCall.Arguments.ValueKind.Should().Be(JsonValueKind.Object);

        var toolResult = AIToolResult.Success("call_weather_42", "get_weather", new { temp_c = 25.0, condition = "Açık" });

        var messages = new List<AIAgentMessage>
        {
            AIAgentMessage.CreateUser("Kuzey tarlasında hava nasıl?"),
            AIAgentMessage.CreateAssistant(null, new[] { toolCall }),
            AIAgentMessage.CreateToolResult(toolResult)
        };

        var response = await provider.GenerateResponseAsync(new AIAgentRequest(messages));
        response.Content.Should().Be("İşlem tamamlandı.");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var msgList = doc.RootElement.GetProperty("messages");

        // Turn 1: Assistant with tool_calls
        var assistantMsg = msgList[1];
        assistantMsg.GetProperty("role").GetString().Should().Be("assistant");
        var tc = assistantMsg.GetProperty("tool_calls")[0];
        tc.GetProperty("id").GetString().Should().Be("call_weather_42");
        tc.GetProperty("function").GetProperty("name").GetString().Should().Be("get_weather");

        // Must be serialized as a JSON string on the wire!
        var wireArgsString = tc.GetProperty("function").GetProperty("arguments").GetString();
        wireArgsString.Should().NotBeNull();
        using var argsDoc = JsonDocument.Parse(wireArgsString!);
        argsDoc.RootElement.GetProperty("farm_id").GetString().Should().Be(farmId);
    }

    // =========================================================================
    // 6. Tool Result Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ToolResultRoundTrip_SerializesRoleToolWithToolCallIdAndStructuredJson()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Kuzey Tarlası'nda sıcaklık 24 derece."
                        },
                        finish_reason = "stop"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var toolCall = AIToolCall.Create("call_weather_1", "get_weather", "{\"farm_id\":\"00000000-0000-0000-0000-000000000001\"}");
        var toolResult = AIToolResult.Success("call_weather_1", "get_weather", new { farm_id = "00000000-0000-0000-0000-000000000001", temperature = 24 });

        var messages = new List<AIAgentMessage>
        {
            AIAgentMessage.CreateUser("Hava durumu?"),
            AIAgentMessage.CreateAssistant(null, new[] { toolCall }),
            AIAgentMessage.CreateToolResult(toolResult)
        };

        var response = await provider.GenerateResponseAsync(new AIAgentRequest(messages));
        response.Content.Should().Be("Kuzey Tarlası'nda sıcaklık 24 derece.");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var toolMsg = doc.RootElement.GetProperty("messages")[2];

        toolMsg.GetProperty("role").GetString().Should().Be("tool");
        toolMsg.GetProperty("tool_call_id").GetString().Should().Be("call_weather_1");

        var rawContent = toolMsg.GetProperty("content").GetString();
        rawContent.Should().NotBeNull();

        using var contentDoc = JsonDocument.Parse(rawContent!);
        contentDoc.RootElement.GetProperty("temperature").GetInt32().Should().Be(24);
        contentDoc.RootElement.GetProperty("farm_id").GetString().Should().Be("00000000-0000-0000-0000-000000000001");
    }

    // =========================================================================
    // 7. Failed Tool Result Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_FailedToolResultRoundTrip_SerializesTruthfulFailureInformation()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Tarla bulunamadı."
                        },
                        finish_reason = "stop"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var failedResult = AIToolResult.Failure(
            callId: "call_fail_99",
            toolName: "get_weather",
            errorMessage: "Farm was not found or is not accessible to the current user.",
            errorCode: "farm_not_found");

        var messages = new[]
        {
            AIAgentMessage.CreateUser("Hava durumu getir"),
            AIAgentMessage.CreateAssistant(null, new[] { AIToolCall.Create("call_fail_99", "get_weather", "{}") }),
            AIAgentMessage.CreateToolResult(failedResult)
        };

        await provider.GenerateResponseAsync(new AIAgentRequest(messages));

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var toolMsg = doc.RootElement.GetProperty("messages")[2];

        toolMsg.GetProperty("role").GetString().Should().Be("tool");
        toolMsg.GetProperty("tool_call_id").GetString().Should().Be("call_fail_99");

        var rawContent = toolMsg.GetProperty("content").GetString();
        rawContent.Should().NotBeNull();

        using var contentDoc = JsonDocument.Parse(rawContent!);
        contentDoc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        contentDoc.RootElement.GetProperty("error_code").GetString().Should().Be("farm_not_found");
        contentDoc.RootElement.GetProperty("error_message").GetString().Should().Contain("Farm was not found");
    }

    // =========================================================================
    // 8. Thinking Mode & reasoning_content Round Trip Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ReasoningContentRoundTrip_PreservesOpaqueStateWithoutExposingIt()
    {
        // 1. First request returns a tool call with reasoning_content
        var (provider1, _) = CreateProviderWithMockHandler(_ =>
        {
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = (string?)null,
                            reasoning_content = "Kullanıcı tarlalarını görmek istiyor, list_farms çağırmalıyım.",
                            tool_calls = new object[]
                            {
                                new
                                {
                                    id = "call_think_1",
                                    type = "function",
                                    function = new
                                    {
                                        name = "list_farms",
                                        arguments = "{}"
                                    }
                                }
                            }
                        },
                        finish_reason = "tool_calls"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var firstRequest = new AIAgentRequest(new[] { AIAgentMessage.CreateUser("Tarlalarımı listele") });
        var firstResponse = await provider1.GenerateResponseAsync(firstRequest);

        firstResponse.HasToolCalls.Should().BeTrue();
        firstResponse.ProviderMetadata.Should().NotBeNull();
        firstResponse.ProviderMetadata![DeepSeekAIAgentProvider.ReasoningContentMetadataKey]
            .Should().Be("Kullanıcı tarlalarını görmek istiyor, list_farms çağırmalıyım.");

        // Invariant: reasoning_content must NOT be in Content
        firstResponse.Content.Should().BeNull();

        // 2. Convert to assistant message (as orchestrator does)
        var assistantMsg = firstResponse.ToAssistantMessage();
        assistantMsg.ProviderMetadata.Should().NotBeNull();
        assistantMsg.ProviderMetadata![DeepSeekAIAgentProvider.ReasoningContentMetadataKey]
            .Should().Be("Kullanıcı tarlalarını görmek istiyor, list_farms çağırmalıyım.");

        var toolResult = AIToolResult.Success("call_think_1", "list_farms", new { count = 1 });
        var toolMsg = AIAgentMessage.CreateToolResult(toolResult);

        // 3. Second request passes conversation history back to provider
        string? secondRequestBody = null;
        var (provider2, _) = CreateProviderWithMockHandler(req =>
        {
            secondRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "1 adet tarlanız var."
                        },
                        finish_reason = "stop"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var secondRequest = new AIAgentRequest(new[] { firstRequest.Messages[0], assistantMsg, toolMsg });
        var secondResponse = await provider2.GenerateResponseAsync(secondRequest);
        secondResponse.Content.Should().Be("1 adet tarlanız var.");

        secondRequestBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(secondRequestBody!);
        var modelTurn = doc.RootElement.GetProperty("messages")[1];

        // Must preserve the exact reasoning_content on the assistant turn!
        modelTurn.GetProperty("reasoning_content").GetString()
            .Should().Be("Kullanıcı tarlalarını görmek istiyor, list_farms çağırmalıyım.");
    }

    // =========================================================================
    // 9. Provider Metadata Isolation Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_ProviderMetadataIsolation_DoesNotSerializeGeminiMetadataToDeepSeek()
    {
        string? capturedBody = null;
        var (provider, _) = CreateProviderWithMockHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = "Tamam."
                        },
                        finish_reason = "stop"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        // Message containing Gemini-specific thought_signature metadata
        var geminiMetadata = new Dictionary<string, string>
        {
            ["thought_signature"] = "gemini-opaque-token"
        };
        var toolCall = AIToolCall.Create("call_1", "list_farms", "{}", geminiMetadata);
        var assistantMsg = AIAgentMessage.CreateAssistant(null, new[] { toolCall }, geminiMetadata);

        var request = new AIAgentRequest(new[]
        {
            AIAgentMessage.CreateUser("Merhaba"),
            assistantMsg,
            AIAgentMessage.CreateToolResult(AIToolResult.Success("call_1", "list_farms", new { }))
        });

        await provider.GenerateResponseAsync(request);

        capturedBody.Should().NotBeNull();
        capturedBody.Should().NotContain("thought_signature");
        capturedBody.Should().NotContain("gemini-opaque-token");
    }

    // =========================================================================
    // 10. Malformed Tool Argument JSON Tests
    // =========================================================================

    [Fact]
    public async Task GenerateResponseAsync_MalformedToolArgumentJson_ThrowsControlledExceptionAndOrchestratorFailsSafely()
    {
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            var deepSeekResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = (string?)null,
                            tool_calls = new object[]
                            {
                                new
                                {
                                    id = "call_bad",
                                    type = "function",
                                    function = new
                                    {
                                        name = "create_task",
                                        arguments = "{bad json"
                                    }
                                }
                            }
                        },
                        finish_reason = "tool_calls"
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var mediatorMock = new Mock<IMediator>();
        var userCtx = new FakeCurrentUserContext();
        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userCtx);
        var registry = new AgentToolRegistry(new[] { createTaskTool });
        var orchestrator = new AIAgentOrchestrator(provider, registry);

        var result = await orchestrator.RunAsync("Görev oluştur");

        // Must fail safely without escaping unhandled JsonException
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_provider_error");

        // Invariant: create_task must NOT have been executed
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 11. Missing API Key & Error Tests
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
    public async Task Orchestrator_WhenDeepSeekApiKeyMissing_ReturnsAgentProviderError()
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
            Content = new StringContent("Rate limit exceeded for test-deepseek-key.", Encoding.UTF8, "text/plain")
        });

        var registry = new AgentToolRegistry(Enumerable.Empty<IAgentTool>());
        var orchestrator = new AIAgentOrchestrator(provider, registry);

        var result = await orchestrator.RunAsync("Merhaba");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_provider_error");
        result.ErrorMessage.Should().NotContain("test-deepseek-key");
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

    // =========================================================================
    // 12. Dependency Injection Provider Selection Tests
    // =========================================================================

    [Fact]
    public void AddInfrastructure_WhenProviderIsDeepSeek_RegistersDeepSeekProviders()
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI_CHAT_PROVIDER"] = "deepseek",
            ["AI:DeepSeekApiKey"] = "fake-key",
            ["AI:DeepSeekModel"] = "deepseek-chat"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        // 1. IAIAgentProvider resolves to DeepSeekAIAgentProvider
        var agentProvider = scope.ServiceProvider.GetService<IAIAgentProvider>();
        agentProvider.Should().NotBeNull();
        agentProvider.Should().BeOfType<DeepSeekAIAgentProvider>();

        // 2. IAIChatProvider resolves to DeepSeekAIChatProvider
        var chatProvider = scope.ServiceProvider.GetService<IAIChatProvider>();
        chatProvider.Should().NotBeNull();
        chatProvider.Should().BeOfType<DeepSeekAIChatProvider>();
    }

    [Fact]
    public void AddInfrastructure_WhenProviderIsGemini_RegistersGeminiProviders()
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

        var agentProvider = scope.ServiceProvider.GetService<IAIAgentProvider>();
        agentProvider.Should().NotBeNull();
        agentProvider.Should().BeOfType<GeminiAIAgentProvider>();

        var chatProvider = scope.ServiceProvider.GetService<IAIChatProvider>();
        chatProvider.Should().NotBeNull();
        chatProvider.Should().BeOfType<GeminiAIChatProvider>();
    }

    // =========================================================================
    // 13. Read-Only End-to-End Composition Test
    // =========================================================================

    [Fact]
    public async Task Orchestrator_CompositionWithDeepSeekAdapter_ReadOnlyFlow_Succeeds()
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
            object deepSeekResponse = turn switch
            {
                // Turn 1: Model calls list_farms
                1 => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new object[]
                                {
                                    new
                                    {
                                        id = "call_ds_1",
                                        type = "function",
                                        function = new
                                        {
                                            name = "list_farms",
                                            arguments = "{}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    }
                },
                // Turn 2: Model calls get_weather with resolved farmId
                2 => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new object[]
                                {
                                    new
                                    {
                                        id = "call_ds_2",
                                        type = "function",
                                        function = new
                                        {
                                            name = "get_weather",
                                            arguments = $"{{\"farm_id\":\"{farmId}\"}}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    }
                },
                // Turn 3: Model responds with final text
                _ => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = "Kuzey Tarlası'nda hava açık ve sıcaklık 24 derece."
                            },
                            finish_reason = "stop"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
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
    // 14. Write Tool End-to-End Composition Test
    // =========================================================================

    [Fact]
    public async Task Orchestrator_CompositionWithDeepSeekAdapter_WriteTaskFlow_Succeeds()
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
            object deepSeekResponse = turn switch
            {
                // Turn 1: Model calls list_farms
                1 => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new object[]
                                {
                                    new
                                    {
                                        id = "call_wt_1",
                                        type = "function",
                                        function = new
                                        {
                                            name = "list_farms",
                                            arguments = "{}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    }
                },
                // Turn 2: Model calls create_task
                2 => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new object[]
                                {
                                    new
                                    {
                                        id = "call_wt_2",
                                        type = "function",
                                        function = new
                                        {
                                            name = "create_task",
                                            arguments = $"{{\"farm_id\":\"{farmId}\",\"title\":\"Damlama sulama\",\"due_date\":\"2026-09-04\"}}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    }
                },
                // Turn 3: Final confirmation
                _ => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = "Tamam, Kuzey Tarlası için damlama sulama görevi oluşturdum."
                            },
                            finish_reason = "stop"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
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

    // =========================================================================
    // 15. Write Failure Composition Test
    // =========================================================================

    [Fact]
    public async Task Orchestrator_CompositionWithDeepSeekAdapter_WriteFailure_PropagatesTruthfully()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userCtx = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateTaskException("Bu görev zaten mevcut."));

        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userCtx);
        var registry = new AgentToolRegistry(new[] { createTaskTool });

        var turn = 0;
        var (provider, _) = CreateProviderWithMockHandler(_ =>
        {
            turn++;
            object deepSeekResponse = turn switch
            {
                // Turn 1: Model requests create_task
                1 => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new object[]
                                {
                                    new
                                    {
                                        id = "call_fail_1",
                                        type = "function",
                                        function = new
                                        {
                                            name = "create_task",
                                            arguments = $"{{\"farm_id\":\"{farmId}\",\"title\":\"Budama\",\"due_date\":\"2026-09-04\"}}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    }
                },
                // Turn 2: Model recognizes failure and explains to user
                _ => new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = "Bu görev zaten mevcut olduğu için tekrar oluşturulmadı."
                            },
                            finish_reason = "stop"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deepSeekResponse), Encoding.UTF8, "application/json")
            };
        });

        var orchestrator = new AIAgentOrchestrator(provider, registry);
        var result = await orchestrator.RunAsync("Budama görevi ekle");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Bu görev zaten mevcut olduğu için tekrar oluşturulmadı.");
        result.Iterations.Should().Be(2);

        // Verify tool result was recorded as duplicate_task failure
        var toolResultMsg = result.Messages[2];
        toolResultMsg.Role.Should().Be(AIAgentRole.Tool);
        toolResultMsg.ToolResult!.IsSuccess.Should().BeFalse();
        toolResultMsg.ToolResult.ErrorCode.Should().Be("duplicate_task");
    }
}

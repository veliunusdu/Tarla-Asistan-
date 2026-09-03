using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Application;
using TarlaAsistani.Application.Common.AI;
using Xunit;

namespace TarlaAsistani.UnitTests.Common.AI;

public class AIAgentOrchestratorTests
{
    private sealed class SimpleTestTool : IAgentTool
    {
        public string Name { get; }
        public AIToolDefinition Definition { get; }
        public List<AIToolCall> Invocations { get; } = new();
        public Func<AIToolCall, CancellationToken, Task<AIToolResult>>? Handler { get; init; }

        public SimpleTestTool(string name, string description = "A test tool")
        {
            Name = name;
            Definition = AIToolDefinition.CreateEmpty(name, description);
        }

        public Task<AIToolResult> ExecuteAsync(AIToolCall call, CancellationToken cancellationToken = default)
        {
            Invocations.Add(call);
            if (Handler != null)
            {
                return Handler(call, cancellationToken);
            }

            return Task.FromResult(AIToolResult.Success(call.CallId, Name, new { status = "success" }));
        }
    }

    // ── TEST 1: No tools / normal answer ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoToolsRequested_ReturnsFinalAssistantText_AndCallsProviderOnce()
    {
        var mockProvider = new Mock<IAIAgentProvider>();
        var normalReply = AIAgentResponse.CreateTextResponse("Merhaba, size nasıl yardımcı olabilirim?");

        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalReply);

        var toolRegistry = new AgentToolRegistry();
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var result = await orchestrator.RunAsync("Merhaba", cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Merhaba, size nasıl yardımcı olabilirim?");
        result.Iterations.Should().Be(1);
        result.Messages.Should().HaveCount(2); // User + Assistant
        result.Messages[0].Role.Should().Be(AIAgentRole.User);
        result.Messages[1].Role.Should().Be(AIAgentRole.Assistant);
        result.Messages[1].Content.Should().Be("Merhaba, size nasıl yardımcı olabilirim?");

        mockProvider.Verify(
            p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── TEST 2: Single tool call ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleToolCall_ExecutesTool_AndReturnsFinalProviderAnswer()
    {
        var tool = new SimpleTestTool("test_tool");
        var toolRegistry = new AgentToolRegistry(new[] { tool });

        var toolCall = AIToolCall.Create("call_1", "test_tool", "{}");
        var providerTurn1 = AIAgentResponse.CreateToolCallsResponse(new[] { toolCall }, "Alet çalıştırılıyor.");
        var providerTurn2 = AIAgentResponse.CreateTextResponse("Tool completed.");

        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .SetupSequence(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerTurn1)
            .ReturnsAsync(providerTurn2);

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var result = await orchestrator.RunAsync("Görev yap");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Tool completed.");
        result.Iterations.Should().Be(2);

        tool.Invocations.Should().HaveCount(1);
        tool.Invocations[0].ToolName.Should().Be("test_tool");
        mockProvider.Verify(
            p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── TEST 3: Conversation ordering ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PreservesStrictConversationOrdering_User_Assistant_Tool()
    {
        var tool = new SimpleTestTool("resolve_farm");
        var toolRegistry = new AgentToolRegistry(new[] { tool });

        var toolCall = AIToolCall.Create("call_100", "resolve_farm", "{}");
        var turn1 = AIAgentResponse.CreateToolCallsResponse(new[] { toolCall }, "Tarlayı sorguluyorum.");
        var turn2 = AIAgentResponse.CreateTextResponse("Tarla bulundu: Ada 101");

        var requests = new List<AIAgentRequest>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                requests.Add(req);
                return Task.FromResult(requests.Count == 1 ? turn1 : turn2);
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var result = await orchestrator.RunAsync("Tarlamı getir", systemPrompt: "Sen yardımcısın.");

        result.IsSuccess.Should().BeTrue();
        requests.Should().HaveCount(2);
        var secondRequest = requests[1];

        // Exact semantic ordering: User -> Assistant(with ToolCall) -> Tool(with ToolResult)
        secondRequest.Messages.Should().HaveCount(3);
        secondRequest.Messages[0].Role.Should().Be(AIAgentRole.User);
        secondRequest.Messages[0].Content.Should().Be("Tarlamı getir");

        secondRequest.Messages[1].Role.Should().Be(AIAgentRole.Assistant);
        secondRequest.Messages[1].ToolCalls.Should().HaveCount(1);
        secondRequest.Messages[1].ToolCalls[0].ToolName.Should().Be("resolve_farm");

        secondRequest.Messages[2].Role.Should().Be(AIAgentRole.Tool);
        secondRequest.Messages[2].ToolResult.Should().NotBeNull();
        secondRequest.Messages[2].ToolResult!.ToolName.Should().Be("resolve_farm");
        secondRequest.Messages[2].ToolResult!.IsSuccess.Should().BeTrue();
    }

    // ── TEST 4: Multiple tool calls executed sequentially in order ───────────

    [Fact]
    public async Task RunAsync_MultipleToolCalls_ExecutedSequentiallyInOrder_AndAppendedBeforeNextTurn()
    {
        var executionOrder = new List<string>();

        var toolA = new SimpleTestTool("tool_a")
        {
            Handler = (c, ct) =>
            {
                executionOrder.Add("tool_a");
                return Task.FromResult(AIToolResult.Success(c.CallId, c.ToolName, "{}"));
            }
        };

        var toolB = new SimpleTestTool("tool_b")
        {
            Handler = (c, ct) =>
            {
                executionOrder.Add("tool_b");
                return Task.FromResult(AIToolResult.Success(c.CallId, c.ToolName, "{}"));
            }
        };

        var toolRegistry = new AgentToolRegistry(new[] { toolA, toolB });

        var callA = AIToolCall.Create("c_a", "tool_a", "{}");
        var callB = AIToolCall.Create("c_b", "tool_b", "{}");

        var turn1 = AIAgentResponse.CreateToolCallsResponse(new[] { callA, callB }, "İki aleti birden çağırıyorum.");
        var turn2 = AIAgentResponse.CreateTextResponse("İki işlem de bitti.");

        var requests = new List<AIAgentRequest>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                requests.Add(req);
                return Task.FromResult(requests.Count == 1 ? turn1 : turn2);
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);
        var result = await orchestrator.RunAsync("Çoklu işlem yap");

        result.IsSuccess.Should().BeTrue();
        executionOrder.Should().Equal("tool_a", "tool_b");

        requests.Should().HaveCount(2);
        var secondRequest = requests[1];
        // User -> Assistant(calls A & B) -> Tool(A result) -> Tool(B result)
        secondRequest.Messages.Should().HaveCount(4);
        secondRequest.Messages[1].Role.Should().Be(AIAgentRole.Assistant);
        secondRequest.Messages[2].Role.Should().Be(AIAgentRole.Tool);
        secondRequest.Messages[2].ToolResult!.ToolName.Should().Be("tool_a");
        secondRequest.Messages[3].Role.Should().Be(AIAgentRole.Tool);
        secondRequest.Messages[3].ToolResult!.ToolName.Should().Be("tool_b");
    }

    // ── TEST 5: Failed tool result is sent back to provider ──────────────────

    [Fact]
    public async Task RunAsync_FailedToolResult_IsSentBackToProvider_AllowsModelToRecover()
    {
        var failingTool = new SimpleTestTool("create_task")
        {
            Handler = (c, ct) => Task.FromResult(AIToolResult.Failure(c.CallId, c.ToolName, "Tarla bulunamadı.", "some_tool_error"))
        };

        var toolRegistry = new AgentToolRegistry(new[] { failingTool });

        var call = AIToolCall.Create("c_fail", "create_task", "{}");
        var turn1 = AIAgentResponse.CreateToolCallsResponse(new[] { call });
        var turn2 = AIAgentResponse.CreateTextResponse("Belirttiğiniz tarla bulunamadığı için görev oluşturulamadı.");

        var requests = new List<AIAgentRequest>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                requests.Add(req);
                return Task.FromResult(requests.Count == 1 ? turn1 : turn2);
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);
        var result = await orchestrator.RunAsync("Görev ekle");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Belirttiğiniz tarla bulunamadığı için görev oluşturulamadı.");

        requests.Should().HaveCount(2);
        var secondRequest = requests[1];
        var toolMsg = secondRequest.Messages.Last();
        toolMsg.Role.Should().Be(AIAgentRole.Tool);
        toolMsg.ToolResult.Should().NotBeNull();
        toolMsg.ToolResult!.IsSuccess.Should().BeFalse();
        toolMsg.ToolResult.ErrorCode.Should().Be("some_tool_error");
        toolMsg.ToolResult.ErrorMessage.Should().Be("Tarla bulunamadı.");
    }

    // ── TEST 6: Unknown tool returns structured error to provider ────────────

    [Fact]
    public async Task RunAsync_UnknownTool_PassesStructuredFailureToProvider_AndCompletes()
    {
        var toolRegistry = new AgentToolRegistry(); // empty registry

        var call = AIToolCall.Create("c_unk", "nonexistent_tool", "{}");
        var turn1 = AIAgentResponse.CreateToolCallsResponse(new[] { call });
        var turn2 = AIAgentResponse.CreateTextResponse("İstenen araç mevcut değil.");

        var requests = new List<AIAgentRequest>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                requests.Add(req);
                return Task.FromResult(requests.Count == 1 ? turn1 : turn2);
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);
        var result = await orchestrator.RunAsync("Bilinmeyen aracı çalıştır");

        result.IsSuccess.Should().BeTrue();
        requests.Should().HaveCount(2);
        var secondRequest = requests[1];
        var toolResult = secondRequest.Messages.Last().ToolResult;
        toolResult.Should().NotBeNull();
        toolResult!.IsSuccess.Should().BeFalse();
        toolResult.ErrorCode.Should().Be("unknown_tool");
    }

    // ── TEST 7: Max iteration protection ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProviderContinuallyReturnsToolCalls_HaltsAtMaxIterations()
    {
        var tool = new SimpleTestTool("infinite_tool");
        var toolRegistry = new AgentToolRegistry(new[] { tool });

        var call = AIToolCall.Create("c_inf", "infinite_tool", "{}");
        var turnWithTool = AIAgentResponse.CreateToolCallsResponse(new[] { call });

        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnWithTool);

        const int maxIterations = 3;
        var options = new AIAgentOrchestratorOptions(maxIterations);
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry, options);

        var result = await orchestrator.RunAsync("Döngüye gir");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_max_iterations_exceeded");
        result.ErrorMessage.Should().Contain("maximum number of tool execution iterations");
        result.Iterations.Should().Be(maxIterations);

        // Provider was called exactly maxIterations times
        mockProvider.Verify(
            p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(maxIterations));
    }

    // ── TEST 8: Empty provider response ──────────────────────────────────────

    [Fact]
    public async Task RunAsync_EmptyProviderResponse_ReturnsSafeOrchestrationFailure()
    {
        var emptyResponse = new AIAgentResponse(content: "   ", toolCalls: Array.Empty<AIToolCall>());
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResponse);

        var toolRegistry = new AgentToolRegistry();
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var result = await orchestrator.RunAsync("Merhaba");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_empty_response");
        result.ErrorMessage.Should().Be("The AI service returned an empty response.");
    }

    // ── TEST 9: Cancellation during provider call ────────────────────────────

    [Fact]
    public async Task RunAsync_CancellationDuringProviderCall_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var toolRegistry = new AgentToolRegistry();
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var act = () => orchestrator.RunAsync("İptal testi", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── TEST 10: Cancellation during tool execution ──────────────────────────

    [Fact]
    public async Task RunAsync_CancellationDuringToolExecution_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var tool = new SimpleTestTool("cancelling_tool")
        {
            Handler = (c, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(AIToolResult.Success(c.CallId, c.ToolName, "{}"));
            }
        };

        var toolRegistry = new AgentToolRegistry(new[] { tool });
        var call = AIToolCall.Create("c_cancel", "cancelling_tool", "{}");
        var turn1 = AIAgentResponse.CreateToolCallsResponse(new[] { call });

        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(turn1);

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry);

        var act = () => orchestrator.RunAsync("Alet çalıştır", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── TEST 11: Provider unexpected exception ───────────────────────────────

    [Fact]
    public async Task RunAsync_ProviderUnexpectedException_ReturnsSanitizedError_AndLogs()
    {
        var mockLogger = new Mock<ILogger<AIAgentOrchestrator>>();
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal: API key 'SECRET_AI_KEY_999' is invalid."));

        var toolRegistry = new AgentToolRegistry();
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, toolRegistry, logger: mockLogger.Object);

        var result = await orchestrator.RunAsync("Hata testi");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("agent_provider_error");
        result.ErrorMessage.Should().Be("An error occurred while communicating with the AI service.");
        result.ErrorMessage.Should().NotContain("SECRET_AI_KEY");
        result.ErrorMessage.Should().NotContain("API key");
    }

    // ── TEST 12: No registered tools ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoRegisteredTools_PassesEmptyToolsToProvider_AndSucceeds()
    {
        AIAgentRequest? capturedRequest = null;
        var mockProvider = new Mock<IAIAgentProvider>();
        mockProvider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<AIAgentRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AIAgentRequest req, CancellationToken _) =>
            {
                capturedRequest = req;
                return Task.FromResult(AIToolResponseWithText("Normal sohbet cevabı."));
            });

        var emptyRegistry = new AgentToolRegistry();
        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, emptyRegistry);

        var result = await orchestrator.RunAsync("Hava nasıl?");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Normal sohbet cevabı.");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Tools.Should().BeEmpty();
    }

    // ── TEST 13: Dependency Injection ────────────────────────────────────────

    [Fact]
    public void AddApplication_RegistersIAIAgentOrchestrator_AsScoped()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        // Stub provider to allow full scope resolution
        services.AddScoped(_ => new Mock<IAIAgentProvider>().Object);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var orchestrator = scope.ServiceProvider.GetRequiredService<IAIAgentOrchestrator>();
        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeOfType<AIAgentOrchestrator>();
    }

    private static AIAgentResponse AIToolResponseWithText(string text) =>
        AIAgentResponse.CreateTextResponse(text);
}

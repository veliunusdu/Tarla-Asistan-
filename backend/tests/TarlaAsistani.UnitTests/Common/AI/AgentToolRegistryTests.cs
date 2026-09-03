using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Application;
using TarlaAsistani.Application.Common.AI;
using Xunit;

namespace TarlaAsistani.UnitTests.Common.AI;

public class AgentToolRegistryTests
{
    private sealed class FakeAgentTool : IAgentTool
    {
        public string Name { get; }
        public AIToolDefinition Definition { get; }
        public Func<AIToolCall, CancellationToken, Task<AIToolResult>>? Handler { get; init; }

        public FakeAgentTool(string name, string description = "Test description", string? customDefinitionName = null)
        {
            Name = name;
            var defName = customDefinitionName ?? name;
            Definition = AIToolDefinition.CreateEmpty(defName, description);
        }

        public Task<AIToolResult> ExecuteAsync(AIToolCall call, CancellationToken cancellationToken = default)
        {
            if (Handler != null)
            {
                return Handler(call, cancellationToken);
            }

            return Task.FromResult(AIToolResult.Success(call.CallId, Name, new { status = "executed" }));
        }
    }

    // ── A. Registry with zero tools ──────────────────────────────────────────

    [Fact]
    public async Task AgentToolRegistry_WithZeroTools_HasEmptyDefinitions_AndFailsOnUnknownTool()
    {
        var registry = new AgentToolRegistry();

        registry.GetToolDefinitions().Should().BeEmpty();
        registry.GetTool("nonexistent").Should().BeNull();
        registry.TryGetTool("nonexistent", out var tool).Should().BeFalse();
        tool.Should().BeNull();

        var call = AIToolCall.Create("call_1", "create_task", "{}");
        var result = await registry.ExecuteToolAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("unknown_tool");
        result.ErrorMessage.Should().Contain("create_task");
    }

    // ── B. Registry with one tool ────────────────────────────────────────────

    [Fact]
    public async Task AgentToolRegistry_WithOneTool_ResolvesAndExecutesCorrectly()
    {
        var expectedResult = AIToolResult.Success("call_123", "get_weather", new { temp = 25 });
        var tool = new FakeAgentTool("get_weather", "Fetches weather")
        {
            Handler = (call, ct) => Task.FromResult(expectedResult)
        };

        var registry = new AgentToolRegistry(new[] { tool });

        registry.GetToolDefinitions().Should().HaveCount(1);
        registry.GetToolDefinitions().First().Name.Should().Be("get_weather");

        registry.GetTool("get_weather").Should().BeSameAs(tool);
        registry.TryGetTool("get_weather", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(tool);

        var call = AIToolCall.Create("call_123", "get_weather", "{\"farm_id\":\"123\"}");
        var actualResult = await registry.ExecuteToolAsync(call, CancellationToken.None);

        actualResult.Should().BeSameAs(expectedResult);
        actualResult.IsSuccess.Should().BeTrue();
    }

    // ── C. Unknown tool ──────────────────────────────────────────────────────

    [Fact]
    public async Task AgentToolRegistry_UnknownTool_ReturnsSafeStructuredFailure_WithoutThrowing()
    {
        var tool = new FakeAgentTool("list_farms");
        var registry = new AgentToolRegistry(new[] { tool });

        var call = AIToolCall.Create("call_x", "delete_entire_database", "{}");
        var result = await registry.ExecuteToolAsync(call, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.CallId.Should().Be("call_x");
        result.ToolName.Should().Be("delete_entire_database");
        result.ErrorCode.Should().Be("unknown_tool");
        result.ErrorMessage.Should().Be("Tool 'delete_entire_database' is not registered.");
    }

    // ── D. Duplicate tool names ──────────────────────────────────────────────

    [Fact]
    public void AgentToolRegistry_DuplicateToolNames_FailsFastWithClearException()
    {
        var tool1 = new FakeAgentTool("create_task");
        var tool2 = new FakeAgentTool("create_task");

        var act = () => new AgentToolRegistry(new[] { tool1, tool2 });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate*create_task*");
    }

    // ── E. Tool exception safety ─────────────────────────────────────────────

    [Fact]
    public async Task AgentToolRegistry_ToolThrowsException_ConvertsToFailedAIToolResult_WithoutLeakingInternals()
    {
        var mockLogger = new Mock<ILogger<AgentToolRegistry>>();
        var tool = new FakeAgentTool("failing_tool")
        {
            Handler = (call, ct) => throw new InvalidOperationException("Internal SQL Error: Connection string Password=SuperSecret123;")
        };

        var registry = new AgentToolRegistry(new[] { tool }, mockLogger.Object);

        var call = AIToolCall.Create("call_err", "failing_tool", "{}");
        var result = await registry.ExecuteToolAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("tool_execution_error");
        result.ErrorMessage.Should().Be("An error occurred while executing tool 'failing_tool'.");
        result.ErrorMessage.Should().NotContain("Password");
        result.ErrorMessage.Should().NotContain("SuperSecret123");
        result.ErrorMessage.Should().NotContain("SQL");
    }

    // ── F. Cancellation propagation ──────────────────────────────────────────

    [Fact]
    public async Task AgentToolRegistry_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var tool = new FakeAgentTool("long_running_tool")
        {
            Handler = (call, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(AIToolResult.Success(call.CallId, call.ToolName, "{}"));
            }
        };

        var registry = new AgentToolRegistry(new[] { tool });
        var call = AIToolCall.Create("call_cancel", "long_running_tool", "{}");

        var act = () => registry.ExecuteToolAsync(call, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── G. Tool naming validation ────────────────────────────────────────────

    [Theory]
    [InlineData("create_task")]
    [InlineData("get_weather")]
    [InlineData("list_farms")]
    [InlineData("a")]
    [InlineData("tool_123_abc")]
    public void AgentToolNameValidator_ValidNames_PassValidation(string validName)
    {
        AgentToolNameValidator.IsValid(validName).Should().BeTrue();
        var act = () => AgentToolNameValidator.Validate(validName);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("CreateTask")]
    [InlineData("create-task")]
    [InlineData("1tool")]
    [InlineData("tool name")]
    [InlineData("create.task")]
    [InlineData("TASK")]
    [InlineData("")]
    [InlineData("   ")]
    public void AgentToolNameValidator_InvalidNames_FailValidation(string invalidName)
    {
        AgentToolNameValidator.IsValid(invalidName).Should().BeFalse();
        var act = () => AgentToolNameValidator.Validate(invalidName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgentToolRegistry_ToolNameMismatchWithDefinition_ThrowsInvalidOperationException()
    {
        var tool = new FakeAgentTool("tool_one", customDefinitionName: "tool_two");

        var act = () => new AgentToolRegistry(new[] { tool });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*mismatched names*");
    }

    // ── H. Dependency Injection Support ──────────────────────────────────────

    [Fact]
    public void AgentToolRegistry_WithNoRegisteredToolsInDI_ResolvesCleanlyWithEmptyDefinitions()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAgentToolRegistry, AgentToolRegistry>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IAgentToolRegistry>();
        registry.Should().NotBeNull();
        registry.Should().BeOfType<AgentToolRegistry>();
        registry.GetToolDefinitions().Should().BeEmpty();
    }

    [Fact]
    public void AddApplication_RegistersAgentToolRegistry_AndContainsBuiltInTools()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IAgentToolRegistry>();
        registry.Should().NotBeNull();
        registry.Should().BeOfType<AgentToolRegistry>();
        registry.GetToolDefinitions().Select(d => d.Name).Should().Contain(new[] { "list_farms", "get_weather", "get_tasks", "create_task" });
    }
}

using System.Text.Json;
using FluentAssertions;
using Moq;
using TarlaAsistani.Application.Common.AI;
using Xunit;

namespace TarlaAsistani.UnitTests.Common.AI;

public class AIAgentCoreContractsTests
{
    // ── AIToolDefinition Tests ───────────────────────────────────────────────

    [Fact]
    public void AIToolDefinition_Create_WithValidJsonSchema_ParsesCorrectly()
    {
        const string schema = """
        {
            "type": "object",
            "properties": {
                "farm_id": { "type": "string" },
                "task_title": { "type": "string" }
            },
            "required": ["farm_id", "task_title"]
        }
        """;

        var def = AIToolDefinition.Create("create_task", "Creates a new farm task", schema);

        def.Name.Should().Be("create_task");
        def.Description.Should().Be("Creates a new farm task");
        def.ParametersSchema.ValueKind.Should().Be(JsonValueKind.Object);
        def.ParametersSchema.GetProperty("properties").GetProperty("task_title").GetProperty("type").GetString()
            .Should().Be("string");
    }

    [Fact]
    public void AIToolDefinition_CreateEmpty_InitializesEmptyObjectSchema()
    {
        var def = AIToolDefinition.CreateEmpty("get_current_time", "Returns server time");

        def.Name.Should().Be("get_current_time");
        def.ParametersSchema.GetProperty("type").GetString().Should().Be("object");
        def.ParametersSchema.GetProperty("properties").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AIToolDefinition_ThrowsOnInvalidName(string? invalidName)
    {
        var act = () => new AIToolDefinition(invalidName!, "Description", default);
        act.Should().Throw<ArgumentException>();
    }

    // ── AIToolCall Tests ─────────────────────────────────────────────────────

    private record TestTaskArgs(string FarmId, string Title, int Priority);

    [Fact]
    public void AIToolCall_PreservesStructuredArguments_AndDeserializesProperly()
    {
        const string argsJson = """
        {
            "farmId": "farm-123",
            "title": "İlaçlama yap",
            "priority": 1
        }
        """;

        var call = AIToolCall.Create("call_abc123", "create_task", argsJson);

        call.CallId.Should().Be("call_abc123");
        call.ToolName.Should().Be("create_task");
        call.Arguments.ValueKind.Should().Be(JsonValueKind.Object);

        var typed = call.DeserializeArguments<TestTaskArgs>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        typed.Should().NotBeNull();
        typed!.FarmId.Should().Be("farm-123");
        typed.Title.Should().Be("İlaçlama yap");
        typed.Priority.Should().Be(1);
    }

    [Fact]
    public void AIToolCall_Create_WithEmptyArguments_DefaultsToEmptyObject()
    {
        var call = AIToolCall.Create("call_1", "get_status", null);

        call.Arguments.ValueKind.Should().Be(JsonValueKind.Object);
        call.Arguments.GetRawText().Should().Be("{}");
    }

    // ── AIToolResult Tests ───────────────────────────────────────────────────

    [Fact]
    public void AIToolResult_Success_WithTypedObject_SerializesStructuredResult()
    {
        var payload = new { TaskId = "task-999", Status = "Created" };
        var result = AIToolResult.Success("call_1", "create_task", payload);

        result.IsSuccess.Should().BeTrue();
        result.CallId.Should().Be("call_1");
        result.ToolName.Should().Be("create_task");
        result.ErrorMessage.Should().BeNull();
        result.Result.Should().NotBeNull();
        result.Result!.Value.GetProperty("TaskId").GetString().Should().Be("task-999");
        result.GetContentString().Should().Contain("task-999");
    }

    [Fact]
    public void AIToolResult_Failure_ContainsErrorInformation()
    {
        var result = AIToolResult.Failure("call_2", "create_task", "Tarla bulunamadı.");

        result.IsSuccess.Should().BeFalse();
        result.CallId.Should().Be("call_2");
        result.ToolName.Should().Be("create_task");
        result.ErrorMessage.Should().Be("Tarla bulunamadı.");
        result.Result.Should().BeNull();
        result.GetContentString().Should().Be("Tarla bulunamadı.");
    }

    // ── AIAgentMessage Tests ─────────────────────────────────────────────────

    [Fact]
    public void AIAgentMessage_CreateSystemAndUser_SetsCorrectRolesAndContent()
    {
        var sys = AIAgentMessage.CreateSystem("Sen uzman bir ziraat mühendisisin.");
        sys.Role.Should().Be(AIAgentRole.System);
        sys.Content.Should().Be("Sen uzman bir ziraat mühendisisin.");
        sys.ToolCalls.Should().BeEmpty();

        var user = AIAgentMessage.CreateUser("Yarın yağmur var mı?");
        user.Role.Should().Be(AIAgentRole.User);
        user.Content.Should().Be("Yarın yağmur var mı?");
    }

    [Fact]
    public void AIAgentMessage_CreateAssistant_WithToolCalls_StoresToolCalls()
    {
        var call = AIToolCall.Create("c1", "check_weather", "{\"date\":\"tomorrow\"}");
        var msg = AIAgentMessage.CreateAssistant("Hava durumunu kontrol ediyorum.", new[] { call });

        msg.Role.Should().Be(AIAgentRole.Assistant);
        msg.Content.Should().Be("Hava durumunu kontrol ediyorum.");
        msg.ToolCalls.Should().HaveCount(1);
        msg.ToolCalls[0].ToolName.Should().Be("check_weather");
    }

    [Fact]
    public void AIAgentMessage_CreateAssistant_WithoutContentAndToolCalls_ThrowsArgumentException()
    {
        var act = () => AIAgentMessage.CreateAssistant(null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AIAgentMessage_CreateToolResult_BindsResultAndToolRole()
    {
        var toolResult = AIToolResult.Success("c1", "check_weather", new { Forecast = "Sunny" });
        var msg = AIAgentMessage.CreateToolResult(toolResult);

        msg.Role.Should().Be(AIAgentRole.Tool);
        msg.ToolResult.Should().BeSameAs(toolResult);
        msg.Content.Should().Contain("Sunny");
    }

    // ── AIAgentResponse Tests ────────────────────────────────────────────────

    [Fact]
    public void AIAgentResponse_CreateTextResponse_MarksFinishReasonAsStop()
    {
        var resp = AIAgentResponse.CreateTextResponse("İşleminiz tamamlandı.", promptTokens: 10, completionTokens: 5);

        resp.Content.Should().Be("İşleminiz tamamlandı.");
        resp.HasToolCalls.Should().BeFalse();
        resp.FinishReason.Should().Be(AIAgentFinishReason.Stop);
        resp.TotalTokens.Should().Be(15);
    }

    [Fact]
    public void AIAgentResponse_CreateToolCallsResponse_MarksToolCallsAndAllowsMessageConversion()
    {
        var call = AIToolCall.Create("c1", "create_task", "{}");
        var resp = AIAgentResponse.CreateToolCallsResponse(new[] { call }, "Gereken görevi oluşturuyorum.");

        resp.HasToolCalls.Should().BeTrue();
        resp.FinishReason.Should().Be(AIAgentFinishReason.ToolCalls);
        resp.ToolCalls.Should().HaveCount(1);

        var asMessage = resp.ToAssistantMessage();
        asMessage.Role.Should().Be(AIAgentRole.Assistant);
        asMessage.Content.Should().Be("Gereken görevi oluşturuyorum.");
        asMessage.ToolCalls.Should().HaveCount(1);
    }

    // ── IAIAgentProvider Integration Tests ─────────────────────────────────

    private sealed class TestAgentProvider : IAIAgentProvider
    {
        public AIAgentRequest? LastRequest { get; private set; }
        public AIAgentResponse ResponseToReturn { get; set; } = AIAgentResponse.CreateTextResponse("Test reply");

        public Task<AIAgentResponse> GenerateResponseAsync(AIAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(ResponseToReturn);
        }
    }

    [Fact]
    public async Task IAIAgentProvider_DefaultOverload_ForwardsToGenerateResponseAsync()
    {
        IAIAgentProvider provider = new TestAgentProvider();
        var messages = new[] { AIAgentMessage.CreateUser("Merhaba") };
        var tools = new[] { AIToolDefinition.CreateEmpty("ping", "Checks connectivity") };

        var actual = await provider.GenerateResponseAsync(messages, tools, CancellationToken.None);

        actual.Content.Should().Be("Test reply");
        var testProvider = (TestAgentProvider)provider;
        testProvider.LastRequest.Should().NotBeNull();
        testProvider.LastRequest!.Messages.Should().HaveCount(1);
        testProvider.LastRequest.Tools.Should().HaveCount(1);
        testProvider.LastRequest.Messages[0].Content.Should().Be("Merhaba");
    }
}

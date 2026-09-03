using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.Application;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Tools;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;
using Xunit;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.AI;

public class CreateTaskToolTests
{
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public UserRole? Role { get; set; } = UserRole.Farmer;
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
    }

    private static TaskDto CreateSampleTaskDto(
        Guid farmId,
        Guid createdById,
        string title,
        string description,
        string reason,
        TaskPriority priority,
        DateOnly dueDate)
    {
        return new TaskDto(
            Id: Guid.NewGuid(),
            FarmId: farmId,
            CropPeriodId: null,
            CreatedById: createdById,
            Title: title,
            Description: description,
            Reason: reason,
            Priority: priority,
            Status: TaskStatus.New,
            Source: TaskSource.Manual,
            Confidence: TaskConfidence.High,
            DueDate: dueDate,
            NotAppliedReason: null,
            CompletionNote: null,
            PhotoUrl: null,
            ViewedAtUtc: null,
            CompletedAtUtc: null,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            ExpertReviewRecommended: false
        );
    }

    // =========================================================================
    // 1. Tool Definition Tests
    // =========================================================================

    [Fact]
    public void CreateTaskTool_Definition_HasCorrectNameAndMinimalModelSchema()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        tool.Name.Should().Be("create_task");
        tool.Definition.Name.Should().Be("create_task");
        tool.Definition.Description.Should().Contain("Creates a persistent farm task");

        var schema = tool.Definition.ParametersSchema;
        var requiredList = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        requiredList.Should().Contain(new[] { "farm_id", "title", "due_date" });

        var properties = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
        properties.Should().BeEquivalentTo(new[] { "farm_id", "title", "due_date", "description", "reason", "priority" });

        var schemaRaw = schema.GetRawText();
        schemaRaw.Should().NotContain("user_id");
        schemaRaw.Should().NotContain("userId");
        schemaRaw.Should().NotContain("role");
        schemaRaw.Should().NotContain("created_by_id");
        schemaRaw.Should().NotContain("source");
        schemaRaw.Should().NotContain("confidence");
        schemaRaw.Should().NotContain("crop_period_id");
        schemaRaw.Should().NotContain("status");
        schemaRaw.Should().NotContain("dedupe_key");
        schemaRaw.Should().NotContain("tenant_id");
    }

    // =========================================================================
    // 2. Success Tests
    // =========================================================================

    [Fact]
    public async Task CreateTaskTool_Success_DispatchesCreateExpertTaskCommandWithTrustedIdentityAndDefaults()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var sampleDto = CreateSampleTaskDto(
            farmId: farmId,
            createdById: userId,
            title: "Damlama sulama",
            description: "Damlama sulama",
            reason: "Kullanıcı talebi.",
            priority: TaskPriority.Medium,
            dueDate: new DateOnly(2026, 9, 4));

        mediatorMock
            .Setup(m => m.Send(It.Is<CreateExpertTaskCommand>(c =>
                c.FarmId == farmId &&
                c.CreatedById == userId &&
                c.CreatedByRole == UserRole.Farmer &&
                c.Title == "Damlama sulama" &&
                c.Description == "Damlama sulama" &&
                c.Reason == "Kullanıcı talebi." &&
                c.Priority == TaskPriority.Medium &&
                c.Confidence == TaskConfidence.High &&
                c.DueDate == new DateOnly(2026, 9, 4) &&
                c.CropPeriodId == null
            ), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleDto);

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var jsonArgs = $"{{\"farm_id\":\"{farmId}\", \"title\":\"  Damlama sulama  \", \"due_date\":\"2026-09-04\"}}";
        var call = AIToolCall.Create("call_create_1", "create_task", jsonArgs);

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.CallId.Should().Be("call_create_1");
        result.ToolName.Should().Be("create_task");

        var content = result.GetContentString();
        content.Should().Contain("\"created\":true");
        content.Should().Contain("\"title\":\"Damlama sulama\"");
        content.Should().Contain("\"priority\":\"medium\"");

        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTaskTool_Success_WithExplicitOptionalFields_PassesAllNormalizedValues()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Agronomist };

        var sampleDto = CreateSampleTaskDto(
            farmId: farmId,
            createdById: userId,
            title: "Budama",
            description: "Ana dalları temizle",
            reason: "Aşırı sürgün var",
            priority: TaskPriority.High,
            dueDate: new DateOnly(2026, 10, 1));

        mediatorMock
            .Setup(m => m.Send(It.Is<CreateExpertTaskCommand>(c =>
                c.FarmId == farmId &&
                c.CreatedById == userId &&
                c.CreatedByRole == UserRole.Agronomist &&
                c.Title == "Budama" &&
                c.Description == "Ana dalları temizle" &&
                c.Reason == "Aşırı sürgün var" &&
                c.Priority == TaskPriority.High &&
                c.DueDate == new DateOnly(2026, 10, 1)
            ), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleDto);

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var jsonArgs = $"{{\"farm_id\":\"{farmId}\", \"title\":\"Budama\", \"due_date\":\"2026-10-01\", \"description\":\"Ana dalları temizle\", \"reason\":\"Aşırı sürgün var\", \"priority\":\"high\"}}";
        var call = AIToolCall.Create("call_create_2", "create_task", jsonArgs);

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.GetContentString().Should().Contain("\"created\":true");
        result.GetContentString().Should().Contain("\"priority\":\"high\"");
    }

    // =========================================================================
    // 3. Security & Unknown Properties Tests
    // =========================================================================

    [Fact]
    public async Task CreateTaskTool_MaliciousCallWithUnknownProperties_RejectsAndNeverCallsMediator()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var maliciousArgs = $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\", \"user_id\":\"attacker\", \"role\":\"Agronomist\"}}";
        var call = AIToolCall.Create("call_sec", "create_task", maliciousArgs);

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("Unsupported argument");
        result.GetContentString().Should().NotContain("\"created\":true");
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTaskTool_Unauthenticated_ReturnsUnauthenticatedFailure()
    {
        var mediatorMock = new Mock<IMediator>();
        var unauthContext = new FakeCurrentUserContext { UserId = null, Role = null };

        var tool = new CreateTaskTool(mediatorMock.Object, unauthContext);
        var call = AIToolCall.Create("call_unauth", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("unauthenticated");
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 4. Runtime Validation Tests
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTaskTool_MissingFarmId_ReturnsInvalidArguments(string badFarmId)
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_v1", "create_task", $"{{\"farm_id\":\"{badFarmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTaskTool_InvalidGuidFarmId_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_v2", "create_task", "{\"farm_id\":\"not-a-uuid\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("valid UUID");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")] // less than 2 chars
    public async Task CreateTaskTool_MissingOrTooShortTitle_ReturnsInvalidArguments(string badTitle)
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_v3", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"{badTitle}\", \"due_date\":\"2026-09-04\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
    }

    [Fact]
    public async Task CreateTaskTool_TitleExceeds160Chars_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var longTitle = new string('A', 161);
        var call = AIToolCall.Create("call_v4", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"{longTitle}\", \"due_date\":\"2026-09-04\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("between 2 and 160");
    }

    [Theory]
    [InlineData("")]
    [InlineData("tomorrow")]
    [InlineData("2026/09/04")]
    [InlineData("04-09-2026")]
    [InlineData("2026-02-31")]
    public async Task CreateTaskTool_InvalidDueDate_ReturnsInvalidArguments(string badDate)
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_v5", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"Sulama\", \"due_date\":\"{badDate}\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
    }

    [Fact]
    public async Task CreateTaskTool_InvalidPriority_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var call = AIToolCall.Create("call_v6", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\", \"priority\":\"super-important\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("Allowed values are");
    }

    [Fact]
    public async Task CreateTaskTool_DescriptionExceeds4000Chars_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var longDesc = new string('D', 4001);
        var call = AIToolCall.Create("call_v7", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\", \"description\":\"{longDesc}\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("4000");
    }

    [Fact]
    public async Task CreateTaskTool_ReasonExceeds2000Chars_ReturnsInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var tool = new CreateTaskTool(mediatorMock.Object, userContext);

        var longReason = new string('R', 2001);
        var call = AIToolCall.Create("call_v8", "create_task", $"{{\"farm_id\":\"{Guid.NewGuid()}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\", \"reason\":\"{longReason}\"}}");
        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("2000");
    }

    // =========================================================================
    // 5. Expected Business Failure Mappings
    // =========================================================================

    [Fact]
    public async Task CreateTaskTool_FarmNotFoundException_MapsToFarmNotFound()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FarmNotFoundException(farmId));

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_f_nf", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("farm_not_found");
        result.ErrorMessage.Should().Contain("Farm was not found or is not accessible");
        result.GetContentString().Should().NotContain("\"created\":true");
    }

    [Fact]
    public async Task CreateTaskTool_UnauthorizedAccessException_MapsToFarmNotFoundSafely()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Yalnızca kendi tarlanıza görev ekleyebilirsiniz."));

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_f_unauth", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("farm_not_found");
        result.ErrorMessage.Should().Contain("Farm was not found or is not accessible");
        result.GetContentString().Should().NotContain("\"created\":true");
    }

    [Fact]
    public async Task CreateTaskTool_DuplicateTaskException_MapsToDuplicateTaskErrorCode()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateTaskException("dummy-dedupe-key", isKey: true));

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_f_dup", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("duplicate_task");
        result.ErrorMessage.Should().Contain("An equivalent task already exists.");
        result.GetContentString().Should().NotContain("\"created\":true");
    }

    [Fact]
    public async Task CreateTaskTool_ValidationException_MapsToInvalidArguments()
    {
        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        var failures = new[] { new ValidationFailure("Title", "Görev başlığı geçersiz.") };
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(failures));

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_f_val", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var result = await tool.ExecuteAsync(call, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_arguments");
        result.ErrorMessage.Should().Contain("Görev başlığı geçersiz.");
    }

    [Fact]
    public async Task CreateTaskTool_Cancellation_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mediatorMock = new Mock<IMediator>();
        var userContext = new FakeCurrentUserContext();
        var farmId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var call = AIToolCall.Create("call_cancel", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}");

        var act = () => tool.ExecuteAsync(call, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // =========================================================================
    // 6. Duplicate Safety Test
    // =========================================================================

    [Fact]
    public async Task CreateTaskTool_RepeatedEquivalentRequest_ReturnsDuplicateTaskAndNeverFakesSecondSuccess()
    {
        var mediatorMock = new Mock<IMediator>();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        var sampleDto = CreateSampleTaskDto(
            farmId: farmId,
            createdById: userId,
            title: "Sulama",
            description: "Sulama",
            reason: "Kullanıcı talebi.",
            priority: TaskPriority.Medium,
            dueDate: new DateOnly(2026, 9, 4));

        var invocationCount = 0;
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateExpertTaskCommand _, CancellationToken _) =>
            {
                invocationCount++;
                if (invocationCount == 1)
                {
                    return Task.FromResult(sampleDto);
                }
                throw new DuplicateTaskException("dummy-hash", isKey: true);
            });

        var tool = new CreateTaskTool(mediatorMock.Object, userContext);
        var callArgs = $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}";

        // First attempt: should succeed with created: true
        var call1 = AIToolCall.Create("call_rep_1", "create_task", callArgs);
        var result1 = await tool.ExecuteAsync(call1, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result1.GetContentString().Should().Contain("\"created\":true");

        // Second identical attempt: existing dedupe rejects with DuplicateTaskException
        var call2 = AIToolCall.Create("call_rep_2", "create_task", callArgs);
        var result2 = await tool.ExecuteAsync(call2, CancellationToken.None);

        result2.IsSuccess.Should().BeFalse();
        result2.ErrorCode.Should().Be("duplicate_task");
        result2.GetContentString().Should().NotContain("\"created\":true");
    }

    // =========================================================================
    // 7. Registry & Orchestrator Composition Tests
    // =========================================================================

    [Fact]
    public async Task Orchestrator_MultiTurnComposition_ListFarmsThenCreateTask_Succeeds()
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

        // 2. Setup CreateExpertTaskCommand handler
        var createdTaskDto = CreateSampleTaskDto(
            farmId: farmId,
            createdById: userId,
            title: "Damlama sulama",
            description: "Damlama sulama",
            reason: "Kullanıcı talebi.",
            priority: TaskPriority.Medium,
            dueDate: new DateOnly(2026, 9, 4));

        mediatorMock
            .Setup(m => m.Send(It.Is<CreateExpertTaskCommand>(c => c.FarmId == farmId && c.CreatedById == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTaskDto);

        // 3. Register tools in registry
        var listFarmsTool = new ListFarmsTool(mediatorMock.Object, userContext);
        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userContext);
        var registry = new AgentToolRegistry(new IAgentTool[] { listFarmsTool, createTaskTool });

        // 4. Simulate Fake AI Provider sequence:
        // Turn 1: AI calls list_farms
        var turn1Response = AIAgentResponse.CreateToolCallsResponse(
            new[] { AIToolCall.Create("call_1", "list_farms", "{}") },
            "Tarlanızı listeliyorum.");

        // Turn 2: AI calls create_task with resolved farmId
        var turn2Response = AIAgentResponse.CreateToolCallsResponse(
            new[] { AIToolCall.Create("call_2", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Damlama sulama\", \"due_date\":\"2026-09-04\"}}") },
            "Kuzey Tarlası için görevi oluşturuyorum.");

        // Turn 3: AI produces final answer
        var turn3Response = AIAgentResponse.CreateTextResponse(
            "Tamam, Kuzey Tarlası için damlama sulama görevi oluşturdum.");

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

        var result = await orchestrator.RunAsync("Yarın Kuzey tarlasına damlama sulama görevi oluştur.");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Tamam, Kuzey Tarlası için damlama sulama görevi oluşturdum.");
        result.Iterations.Should().Be(3);
        providerCalls.Should().HaveCount(3);

        // 6 messages in history: User -> Assistant(list_farms) -> Tool(farms) -> Assistant(create_task) -> Tool(created) -> Assistant(final)
        result.Messages.Should().HaveCount(6);
        result.Messages[1].ToolCalls.First().ToolName.Should().Be("list_farms");
        result.Messages[3].ToolCalls.First().ToolName.Should().Be("create_task");
        result.Messages[4].ToolResult!.IsSuccess.Should().BeTrue();
        result.Messages[4].ToolResult!.GetContentString().Should().Contain("\"created\":true");

        mediatorMock.Verify(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Orchestrator_FailureComposition_WhenCreateTaskFails_PropagatesTruthfulFailureToAI()
    {
        var mediatorMock = new Mock<IMediator>();
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var userContext = new FakeCurrentUserContext { UserId = userId, Role = UserRole.Farmer };

        // Command fails due to duplicate
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateExpertTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateTaskException("dummy-dedupe", isKey: true));

        var createTaskTool = new CreateTaskTool(mediatorMock.Object, userContext);
        var registry = new AgentToolRegistry(new IAgentTool[] { createTaskTool });

        // Turn 1: AI calls create_task
        var turn1Response = AIAgentResponse.CreateToolCallsResponse(
            new[] { AIToolCall.Create("call_fail_1", "create_task", $"{{\"farm_id\":\"{farmId}\", \"title\":\"Sulama\", \"due_date\":\"2026-09-04\"}}") });

        // Turn 2: AI observes tool failure and explains to user
        var turn2Response = AIAgentResponse.CreateTextResponse(
            "Bu görev zaten mevcut olduğu için tekrar eklenmedi.");

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
                    _ => turn2Response
                });
            });

        var orchestrator = new AIAgentOrchestrator(mockProvider.Object, registry);

        var result = await orchestrator.RunAsync("Sulama görevi ekle.");

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("Bu görev zaten mevcut olduğu için tekrar eklenmedi.");
        result.Iterations.Should().Be(2);

        // History: User -> Assistant(create_task) -> Tool(failure) -> Assistant(explanation)
        result.Messages.Should().HaveCount(4);
        result.Messages[2].Role.Should().Be(AIAgentRole.Tool);
        result.Messages[2].ToolResult!.IsSuccess.Should().BeFalse();
        result.Messages[2].ToolResult!.ErrorCode.Should().Be("duplicate_task");
        result.Messages[2].ToolResult!.GetContentString().Should().NotContain("\"created\":true");
    }
}

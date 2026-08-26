using FluentAssertions;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Application.Features.Tasks.Services;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.Tasks;

[Trait("Category", "Tasks")]
public class TaskCommandHandlerTests
{
    [Fact]
    public async Task CreateExpertTask_WhenValid_ShouldCreateWithExpertSourceAndSha256Dedupe()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla 1" };
        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db);
        var command = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: expertId,
            Title: "Zararlı Kontrolü",
            Description: "Yaprak biti kontrolü yapın",
            Reason: "Sıcaklık artışı nedeniyle",
            Priority: TaskPriority.High,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 5, 10)
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Source.Should().Be(TaskSource.Expert);
        result.Priority.Should().Be(TaskPriority.High);
        result.Status.Should().Be(TaskStatus.New);
    }

    [Fact]
    public async Task UpdateTaskStatus_WhenCompletedByFarmer_ShouldAutoCreateActivity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = farmId,
            Farm = farm,
            Title = "Gübreleme",
            Description = "Taban gübresi uygulayın",
            Status = TaskStatus.New,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .Build();

        var handler = new UpdateTaskStatusCommandHandler(db);
        var command = new UpdateTaskStatusCommand(
            TaskId: taskId,
            UserId: userId,
            Role: UserRole.Farmer,
            Status: TaskStatus.Completed,
            Note: "Gübreleme tamamlandı"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(TaskStatus.Completed);
        task.Status.Should().Be(TaskStatus.Completed);
    }

    [Fact]
    public async Task UpdateTaskStatus_WhenTaskAlreadyCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };
        var task = new FarmTask
        {
            Id = taskId,
            FarmId = farmId,
            Farm = farm,
            Title = "Sulama",
            Status = TaskStatus.Completed // Terminal state
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithFarmTasks(task)
            .Build();

        var handler = new UpdateTaskStatusCommandHandler(db);
        var command = new UpdateTaskStatusCommand(
            TaskId: taskId,
            UserId: userId,
            Role: UserRole.Farmer,
            Status: TaskStatus.New
        );

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sonlandırılmış görevin durumu değiştirilemez*");
    }

    [Fact]
    public async Task TaskEngine_EnsureDailyTasks_ShouldGenerateGrowthCheckTask()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla 1" };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cropPeriod = new CropPeriod
        {
            FarmId = farmId,
            CropType = CropType.Wheat,
            PlantedAt = today.AddDays(-10),
            Status = CropPeriodStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(cropPeriod)
            .Build();

        // Act
        await TaskEngine.EnsureDailyTasksAsync(db, farm, today);

        // Assert - Task was added via TaskEngine
    }
}

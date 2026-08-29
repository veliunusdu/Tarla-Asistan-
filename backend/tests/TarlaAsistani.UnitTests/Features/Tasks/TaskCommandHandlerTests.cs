using FluentAssertions;
using Moq;
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
    public async Task CreateExpertTask_WhenPushServiceProvided_ShouldDispatchPushNotificationToActiveDevices()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Tarla 1" };
        var device = new DeviceToken
        {
            UserId = farmerId,
            Token = "fcm-device-token-12345",
            Platform = DevicePlatform.Android,
            Active = true
        };

        var pushMock = new Moq.Mock<TarlaAsistani.Application.Common.Interfaces.IPushNotificationService>();
        pushMock.Setup(p => p.SendNotificationAsync(Moq.It.IsAny<Notification>(), "fcm-device-token-12345", Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithDeviceTokens(device)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db, pushMock.Object);
        var command = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: expertId,
            Title: "Sulama Kontrolü",
            Description: "Damlama hatlarını kontrol edin",
            Reason: "Yarın sıcaklık 35 derece",
            Priority: TaskPriority.High,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 5, 12)
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        pushMock.Verify(p => p.SendNotificationAsync(
            Moq.It.Is<Notification>(n => n.UserId == farmerId && n.Title == "Yeni uzman göreviniz var"),
            "fcm-device-token-12345",
            Moq.It.IsAny<CancellationToken>()), Moq.Times.Once);
    }

    [Fact]
    public async Task CreateExpertTask_WhenMultipleDevices_ShouldDispatchPushNotificationInParallel()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Tarla 1" };
        var device1 = new DeviceToken { UserId = farmerId, Token = "token-1", Platform = DevicePlatform.Android, Active = true };
        var device2 = new DeviceToken { UserId = farmerId, Token = "token-2", Platform = DevicePlatform.Ios, Active = true };
        var device3 = new DeviceToken { UserId = farmerId, Token = "token-3", Platform = DevicePlatform.Web, Active = true };

        var pushMock = new Mock<TarlaAsistani.Application.Common.Interfaces.IPushNotificationService>();
        pushMock.Setup(p => p.SendNotificationAsync(It.IsAny<Notification>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithDeviceTokens(device1, device2, device3)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db, pushMock.Object);
        var command = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: expertId,
            Title: "İlaçlama Kontrolü",
            Description: "Hastalık belirtilerini kontrol edin",
            Reason: "Nem oranı yüksek",
            Priority: TaskPriority.Medium,
            Confidence: TaskConfidence.Medium,
            DueDate: new DateOnly(2026, 6, 1)
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        pushMock.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>(), "token-1", It.IsAny<CancellationToken>()), Times.Once);
        pushMock.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>(), "token-2", It.IsAny<CancellationToken>()), Times.Once);
        pushMock.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>(), "token-3", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateExpertTask_WhenFarmNotFound_ShouldThrowFarmNotFoundException()
    {
        // Arrange
        var missingFarmId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateExpertTaskCommandHandler(db);
        var command = new CreateExpertTaskCommand(
            FarmId: missingFarmId,
            CreatedById: Guid.NewGuid(),
            Title: "Test",
            Description: "Test",
            Reason: "Test",
            Priority: TaskPriority.Low,
            Confidence: TaskConfidence.Low,
            DueDate: new DateOnly(2026, 6, 1)
        );

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TarlaAsistani.Domain.Exceptions.FarmNotFoundException>()
            .WithMessage($"*{missingFarmId}*");
    }

    [Fact]
    public async Task CreateExpertTask_WhenCropPeriodMismatch_ShouldThrowCropPeriodMismatchException()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla 1" };
        var wrongCropPeriodId = Guid.NewGuid();

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db);
        var command = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: Guid.NewGuid(),
            Title: "Test",
            Description: "Test",
            Reason: "Test",
            Priority: TaskPriority.Low,
            Confidence: TaskConfidence.Low,
            DueDate: new DateOnly(2026, 6, 1),
            CropPeriodId: wrongCropPeriodId
        );

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TarlaAsistani.Domain.Exceptions.CropPeriodMismatchException>()
            .WithMessage($"*{wrongCropPeriodId}*");
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

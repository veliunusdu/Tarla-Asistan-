using FluentAssertions;
using TarlaAsistani.Application.Features.Pilot.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.UnitTests.Features.Pilot;

[Trait("Category", "Pilot")]
public class GetPilotMetricsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalledByAgronomist_ShouldCalculateAccurateMetrics()
    {
        // Arrange
        var agronomistId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var task1 = new FarmTask
        {
            FarmId = farmId,
            Status = TaskStatus.Completed,
            Source = TaskSource.Weather,
            Priority = TaskPriority.Critical,
            CreatedAtUtc = now.AddDays(-2)
        };

        var task2 = new FarmTask
        {
            FarmId = farmId,
            Status = TaskStatus.New,
            Source = TaskSource.CropCalendar,
            Priority = TaskPriority.Medium,
            CreatedAtUtc = now.AddDays(-1)
        };

        var feedback = new PilotFeedback
        {
            FeedbackType = FeedbackType.FalseAlert,
            Rating = 4,
            Comment = "Yanlış don uyarısı",
            CreatedAtUtc = now.AddDays(-1)
        };

        var db = new MockDbContextBuilder()
            .WithFarmTasks(task1, task2)
            .WithPilotFeedbacks(feedback)
            .Build();

        var handler = new GetPilotMetricsQueryHandler(db);
        var query = new GetPilotMetricsQuery(agronomistId, UserRole.Agronomist, WindowDays: 7);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TasksCreated.Should().Be(2);
        result.TasksCompleted.Should().Be(1);
        result.TaskCompletionRate.Should().Be(50.0);
        result.CriticalWeatherAlerts.Should().Be(1);
        result.FalseAlerts.Should().Be(1);
        result.FalseAlertRate.Should().Be(100.0);
        result.FeedbackCount.Should().Be(1);
        result.AverageFeedbackRating.Should().Be(4.0);
    }

    [Fact]
    public async Task Handle_WhenCalledByFarmer_ShouldThrowUnauthorized()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();

        var handler = new GetPilotMetricsQueryHandler(db);
        var query = new GetPilotMetricsQuery(farmerId, UserRole.Farmer);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

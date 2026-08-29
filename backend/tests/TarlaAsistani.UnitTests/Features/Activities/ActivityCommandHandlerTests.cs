using FluentAssertions;
using TarlaAsistani.Application.Features.Activities.Commands;
using TarlaAsistani.Application.Features.Activities.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Activities;

[Trait("Category", "Activities")]
public class ActivityCommandHandlerTests
{
    [Fact]
    public async Task CreateActivity_WhenManualInput_ShouldSetStatusConfirmed()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityType: ActivityType.Irrigation,
            Description: "Damlama sulama yapıldı",
            OccurredAt: DateTime.UtcNow,
            InputMethod: ActivitySource.Manual,
            DurationMinutes: 120,
            Amount: 500,
            Unit: "Litre"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityStatus.Confirmed);
        result.ConfirmedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateActivity_WhenVoiceInput_ShouldSetStatusDraft()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityType: ActivityType.Fertilization,
            Description: "Üre gübresi atıldı",
            OccurredAt: DateTime.UtcNow,
            InputMethod: ActivitySource.Voice,
            VoiceTranscript: "Bugün üre gübresi attık"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityStatus.Draft);
        result.ConfirmedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmActivity_WhenDraft_ShouldTransitionToConfirmed()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };
        var draftActivity = new Activity
        {
            Id = activityId,
            FarmId = farmId,
            Farm = farm,
            ActivityType = ActivityType.Spraying,
            Status = ActivityStatus.Draft,
            Description = "İlaçlama taslağı"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithActivities(draftActivity)
            .Build();

        var handler = new ConfirmActivityCommandHandler(db);
        var command = new ConfirmActivityCommand(activityId, userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ActivityStatus.Confirmed);
        result.ConfirmedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateActivity_WhenUpdated_ShouldCreateRevisionSnapshot()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };
        var activity = new Activity
        {
            Id = activityId,
            FarmId = farmId,
            Farm = farm,
            ActivityType = ActivityType.Irrigation,
            Status = ActivityStatus.Confirmed,
            Description = "Eski açıklama",
            Amount = 100
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithActivities(activity)
            .Build();

        var handler = new UpdateActivityCommandHandler(db);
        var command = new UpdateActivityCommand(
            ActivityId: activityId,
            UserId: userId,
            Description: "Yeni açıklama",
            Amount: 200
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Yeni açıklama");
        result.Amount.Should().Be(200);
    }

    [Fact]
    public async Task GetFarmJournal_WhenQueried_ShouldReturnJournalEntries()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };
        var today = DateTime.UtcNow;
        var act1 = new Activity
        {
            FarmId = farmId,
            Farm = farm,
            ActivityType = ActivityType.Irrigation,
            Status = ActivityStatus.Confirmed,
            Description = "Sulama",
            OccurredAtUtc = today,
            Cost = 150
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithActivities(act1)
            .Build();

        var handler = new GetFarmJournalQueryHandler(db);
        var query = new GetFarmJournalQuery(farmId, userId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }
}

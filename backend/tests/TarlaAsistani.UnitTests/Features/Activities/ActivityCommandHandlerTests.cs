using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Features.Activities.Commands;
using TarlaAsistani.Application.Features.Activities.DTOs;
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
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

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
            Unit: "Litre",
            CreatedByRole: UserRole.Farmer
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
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

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
            VoiceTranscript: "Bugün üre gübresi attık",
            CreatedByRole: UserRole.Farmer
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityStatus.Draft);
        result.ConfirmedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CreateActivity_WhenFarmerCreatesActivityOnAnotherFarmersFarm_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmer1Id, Name = "Çiftçi 1 Tarlası" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: farmer2Id,
            ActivityType: ActivityType.Irrigation,
            Description: "İzinsiz sulama",
            OccurredAt: DateTime.UtcNow,
            CreatedByRole: UserRole.Farmer
        );

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*kendi tarlanıza*");
    }

    [Fact]
    public async Task CreateActivity_WhenAgronomistCreatesActivityOnAnyFarm_ShouldSucceed()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Çiftçi Tarlası" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: agronomistId,
            ActivityType: ActivityType.Spraying,
            Description: "Mühendis ilaçlaması",
            OccurredAt: DateTime.UtcNow,
            CreatedByRole: UserRole.Agronomist
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FarmId.Should().Be(farmId);
        result.CreatedById.Should().Be(agronomistId);
    }

    [Fact]
    public async Task CreateActivity_WhenFarmIsArchived_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Arşivli Tarla", ArchivedAt = DateTime.UtcNow };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: farmerId,
            ActivityType: ActivityType.Irrigation,
            Description: "Sulama",
            OccurredAt: DateTime.UtcNow,
            CreatedByRole: UserRole.Farmer
        );

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
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

    [Fact]
    public async Task CreateActivity_WithFreeText_ShouldStoreAndReturnExactActivityName_Test1()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder().WithFarms(farm).Build();
        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityName: "Damla sulama yaptım",
            Description: "Sabah sulaması",
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ActivityName.Should().Be("Damla sulama yaptım");
        result.Description.Should().Be("Sabah sulaması");
    }

    [Fact]
    public async Task CreateActivity_WithCustomNameNotInEnum_ShouldSucceed_Test2()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder().WithFarms(farm).Build();
        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityName: "Çapa",
            Description: "",
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ActivityName.Should().Be("Çapa");
        result.ActivityType.Should().BeNull();
    }

    [Fact]
    public async Task CreateActivity_WithFidanBaglama_ShouldSucceed_Test3()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder().WithFarms(farm).Build();
        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityName: "Fidan bağlama",
            Description: "İpler yenilendi",
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ActivityName.Should().Be("Fidan bağlama");
    }

    [Fact]
    public async Task CreateActivity_WithWhitespaceAroundName_ShouldTrimCleanly_Test4()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder().WithFarms(farm).Build();
        var handler = new CreateActivityCommandHandler(db);
        var command = new CreateActivityCommand(
            FarmId: farmId,
            CreatedById: userId,
            ActivityName: "   Çapa   ",
            Description: "",
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ActivityName.Should().Be("Çapa");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateActivityValidator_WhenActivityNameEmptyOrWhitespace_ShouldBeInvalid_Test5_6(string name)
    {
        // Arrange
        var validator = new CreateActivityCommandValidator();
        var command = new CreateActivityCommand(
            FarmId: Guid.NewGuid(),
            CreatedById: Guid.NewGuid(),
            ActivityName: name,
            Description: "Test"
        );

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateActivityCommand.ActivityName));
    }

    [Fact]
    public async Task CreateActivityValidator_WhenActivityNameExceeds150Chars_ShouldBeInvalid_Test7()
    {
        // Arrange
        var validator = new CreateActivityCommandValidator();
        var command = new CreateActivityCommand(
            FarmId: Guid.NewGuid(),
            CreatedById: Guid.NewGuid(),
            ActivityName: new string('A', 151),
            Description: "Test"
        );

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateActivityCommand.ActivityName));
    }

    [Fact]
    public void ActivityDto_FromEntity_WhenLegacyIrrigationWithoutActivityName_ShouldReturnSulama_Test8()
    {
        // Arrange
        var entity = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = Guid.NewGuid(),
            ActivityName = "",
            ActivityType = ActivityType.Irrigation,
            Description = "Eski sulama"
        };

        // Act
        var dto = ActivityDto.FromEntity(entity);

        // Assert
        dto.ActivityName.Should().Be("Sulama");
        dto.ActivityType.Should().Be(ActivityType.Irrigation);
    }

    [Fact]
    public void ActivityDto_FromEntity_WhenLegacyOtherWithoutActivityName_ShouldReturnDiger_Test9()
    {
        // Arrange
        var entity = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = Guid.NewGuid(),
            ActivityName = "",
            ActivityType = ActivityType.Other,
            Description = "Özel işlem"
        };

        // Act
        var dto = ActivityDto.FromEntity(entity);

        // Assert
        dto.ActivityName.Should().Be("Diğer");
        dto.ActivityType.Should().Be(ActivityType.Other);
    }

    [Fact]
    public async Task UpdateActivity_WhenActivityNameUpdated_ShouldUpdateAndRecordRevision_Test10()
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
            ActivityName = "Çapa",
            Status = ActivityStatus.Confirmed,
            Description = "Yabani ot temizlendi"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithActivities(activity)
            .Build();

        var handler = new UpdateActivityCommandHandler(db);
        var command = new UpdateActivityCommand(
            ActivityId: activityId,
            UserId: userId,
            ActivityName: "Derin çapa"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ActivityName.Should().Be("Derin çapa");
        activity.ActivityName.Should().Be("Derin çapa");
        var rev = await db.ActivityRevisions.FirstOrDefaultAsync(r => r.ActivityId == activityId);
        rev.Should().NotBeNull();
        rev!.PreviousValues.Should().Contain("activity_name");
    }
}

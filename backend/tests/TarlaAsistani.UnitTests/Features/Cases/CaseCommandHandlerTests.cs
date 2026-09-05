using FluentAssertions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;
using Moq;

namespace TarlaAsistani.UnitTests.Features.Cases;

[Trait("Category", "Cases")]
public class CaseCommandHandlerTests
{
    private readonly Mock<IPushNotificationService> _pushServiceMock = new();

    [Fact]
    public async Task CreateCase_WhenFarmerOwnsFarm_ShouldCreateOpenCase()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Tarla 1" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateCaseCommandHandler(db);
        var command = new CreateCaseCommand(
            FarmId: farmId,
            CreatedById: farmerId,
            Category: CaseCategory.Disease,
            Title: "Yaprak Sararması",
            Description: "Alt yapraklarda sararma ve lekeler var"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Yaprak Sararması");
        result.Status.Should().Be(CaseStatus.Open);
        result.Priority.Should().Be(CasePriority.Medium);
    }

    [Fact]
    public async Task CreateCase_ShouldPersistImmutableFarmContextSnapshot()
    {
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var plantedAt = new DateOnly(2026, 4, 15);
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Snapshot Tarlası",
            Latitude = 38.1,
            Longitude = 32.4,
            SizeInHectares = 12.5,
            IrrigationMethod = IrrigationMethod.Drip,
            SoilType = "Tınlı",
            CropPeriods = new List<CropPeriod>
            {
                new CropPeriod { Id = Guid.NewGuid(), FarmId = farmId, CropName = "Nohut", PlantedAt = plantedAt, Status = CropPeriodStatus.Active }
            }
        };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ActivityName = "Damla sulama",
            ActivityType = ActivityType.Irrigation,
            OccurredAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(farm.CropPeriods.ToArray())
            .WithActivities(activity)
            .Build();

        var result = await new CreateCaseCommandHandler(db).Handle(
            new CreateCaseCommand(farmId, farmerId, CaseCategory.Disease, "Test", "Açıklama"),
            CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Context!.FarmName.Should().Be("Snapshot Tarlası");
        result.Context.CropName.Should().Be("Nohut");
        result.Context.Latitude.Should().Be(38.1);
        result.Context.RecentActivities.Should().ContainSingle(a => a.ActivityName == "Damla sulama");
    }

    [Fact]
    public async Task UpdateCaseStatus_WhenAgronomistSetsInReview_ShouldUpdateStatus()
    {
        // Arrange
        var agronomistId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, Name = "Tarla" };
        var supportCase = new SupportCase
        {
            Id = caseId,
            FarmId = farmId,
            Farm = farm,
            Status = CaseStatus.Open
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithSupportCases(supportCase)
            .Build();

        var handler = new UpdateCaseStatusCommandHandler(db);
        var command = new UpdateCaseStatusCommand(
            CaseId: caseId,
            UserId: agronomistId,
            Role: UserRole.Agronomist,
            Status: CaseStatus.InReview,
            AssignToMe: true
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CaseStatus.InReview);
        result.AssignedExpertId.Should().Be(agronomistId);
    }

    [Fact]
    public async Task UpdateCaseStatus_WhenCallerIsFarmer_ShouldThrowUnauthorized()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();

        var handler = new UpdateCaseStatusCommandHandler(db);
        var command = new UpdateCaseStatusCommand(
            CaseId: caseId,
            UserId: farmerId,
            Role: UserRole.Farmer,
            Status: CaseStatus.Closed
        );

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateCaseMessage_WhenExpertRequestsInfo_ShouldTransitionToWaitingFarmer()
    {
        // Arrange
        var agronomistId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Tarla" };
        var supportCase = new SupportCase
        {
            Id = caseId,
            FarmId = farmId,
            Farm = farm,
            Status = CaseStatus.InReview
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithSupportCases(supportCase)
            .Build();

        var handler = new CreateCaseMessageCommandHandler(db, _pushServiceMock.Object);
        var command = new CreateCaseMessageCommand(
            CaseId: caseId,
            SenderId: agronomistId,
            Role: UserRole.Agronomist,
            MessageType: CaseMessageType.AdditionalInfoRequest,
            Body: "Yaprakların alt yüzeyinin fotoğrafını çekebilir misiniz?"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.MessageType.Should().Be(CaseMessageType.AdditionalInfoRequest);
        supportCase.Status.Should().Be(CaseStatus.WaitingFarmer);
    }

    [Fact]
    public async Task CreateExpertResponse_WhenFarmerHasActiveDevice_ShouldSendPushNotification()
    {
        var expertId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = farmerId,
            Name = "Tarla",
            Owner = new User
            {
                Id = farmerId,
                Profile = new Profile { NotificationsEnabled = true },
            },
        };
        var supportCase = new SupportCase
        {
            Id = Guid.NewGuid(),
            FarmId = farm.Id,
            Farm = farm,
            Status = CaseStatus.InReview,
        };
        var device = new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = farmerId,
            Token = "farmer-device-token",
            Active = true,
        };
        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithUsers(farm.Owner)
            .WithProfiles(farm.Owner.Profile!)
            .WithSupportCases(supportCase)
            .WithDeviceTokens(device)
            .Build();
        Notification? dispatchedNotification = null;
        _pushServiceMock
            .Setup(p => p.SendNotificationAsync(It.IsAny<Notification>(), device.Token, It.IsAny<CancellationToken>()))
            .Callback<Notification, string, CancellationToken>((notification, _, _) => dispatchedNotification = notification)
            .ReturnsAsync(true);

        var handler = new CreateExpertResponseCommandHandler(db, _pushServiceMock.Object);

        await handler.Handle(
            new CreateExpertResponseCommand(
                supportCase.Id,
                expertId,
                UserRole.Agronomist,
                "İlaçlamayı iki gün erteleyin."),
            CancellationToken.None);

        _pushServiceMock.Verify(
            p => p.SendNotificationAsync(
                It.Is<Notification>(n => n.NotificationType == NotificationType.ExpertResponse),
                device.Token,
                It.IsAny<CancellationToken>()),
            Times.Once);
        dispatchedNotification.Should().NotBeNull();
        dispatchedNotification!.Status.Should().Be(NotificationStatus.Sent);
        dispatchedNotification.SentAtUtc.Should().NotBeNull();
    }
}

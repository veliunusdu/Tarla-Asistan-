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
}

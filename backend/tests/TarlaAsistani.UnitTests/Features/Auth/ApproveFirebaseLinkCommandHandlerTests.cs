using FluentAssertions;
using TarlaAsistani.Application.Features.Auth.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Auth;

[Trait("Category", "Auth")]
public class ApproveFirebaseLinkCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenValidAgronomist_ShouldCreateApprovalWith24HourExpiry()
    {
        // Arrange
        var agronomist = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905559990011",
            FirebaseUid = null,
            Role = UserRole.Agronomist,
            AccountStatus = AccountStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(agronomist.Id, "target_firebase_uid_123", "operator@tarla.gov.tr");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(agronomist.Id);
        result.FirebaseUid.Should().Be("target_firebase_uid_123");
        result.ApprovedBy.Should().Be("operator@tarla.gov.tr");
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task Handle_WhenUserIsFarmer_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var farmer = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905559990022",
            FirebaseUid = null,
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithUsers(farmer)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(farmer.Id, "target_uid_456", "operator@tarla.gov.tr");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uzman*");
    }
}

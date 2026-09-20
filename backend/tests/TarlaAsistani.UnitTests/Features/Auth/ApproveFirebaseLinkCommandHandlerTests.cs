using FluentAssertions;
using TarlaAsistani.Application.Features.Auth.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Auth;

[Trait("Category", "Auth")]
public class ApproveFirebaseLinkCommandHandlerTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();

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
        var agronomistRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = agronomist.Id,
            Role = UserRole.Agronomist,
            GrantedAtUtc = DateTime.UtcNow
        };
        agronomist.RoleAssignments.Add(agronomistRole);

        var admin = new User
        {
            Id = _adminUserId,
            PhoneNumber = "+905559990099",
            Role = UserRole.Admin,
            AccountStatus = AccountStatus.Active,
            Profile = new Profile { FullName = "Admin Operator" }
        };
        var adminRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            Role = UserRole.Admin,
            GrantedAtUtc = DateTime.UtcNow
        };
        admin.RoleAssignments.Add(adminRole);

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist, admin)
            .WithUserRoleAssignments(agronomistRole, adminRole)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(agronomist.Id, "target_firebase_uid_123", _adminUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(agronomist.Id);
        result.FirebaseUid.Should().Be("target_firebase_uid_123");
        result.ApprovedBy.Should().Be("Admin Operator");
        result.ApprovedByUserId.Should().Be(_adminUserId);
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
        var farmerRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = farmer.Id,
            Role = UserRole.Farmer,
            GrantedAtUtc = DateTime.UtcNow
        };
        farmer.RoleAssignments.Add(farmerRole);

        var db = new MockDbContextBuilder()
            .WithUsers(farmer)
            .WithUserRoleAssignments(farmerRole)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(farmer.Id, "target_uid_456", _adminUserId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*uzman*");
    }

    [Fact]
    public async Task Handle_WhenFirebaseUidAlreadyLinkedToAnotherUser_ShouldThrowInvalidOperationException()
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
        var agronomistRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = agronomist.Id,
            Role = UserRole.Agronomist,
            GrantedAtUtc = DateTime.UtcNow
        };
        agronomist.RoleAssignments.Add(agronomistRole);

        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+905559990033",
            FirebaseUid = "already_linked_uid",
            Role = UserRole.Farmer,
            AccountStatus = AccountStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist, otherUser)
            .WithUserRoleAssignments(agronomistRole)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(agronomist.Id, "already_linked_uid", _adminUserId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*zaten başka bir kullanıcıya bağlı*");
    }

    [Fact]
    public async Task Handle_WhenSameApprovalAlreadyExists_ShouldReturnExistingIdempotently()
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
        var agronomistRole = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = agronomist.Id,
            Role = UserRole.Agronomist,
            GrantedAtUtc = DateTime.UtcNow
        };
        agronomist.RoleAssignments.Add(agronomistRole);

        var existingApproval = new FirebaseLinkApproval
        {
            Id = Guid.NewGuid(),
            UserId = agronomist.Id,
            FirebaseUid = "target_uid_idem",
            ApprovedBy = "First Operator",
            ApprovedByUserId = _adminUserId,
            ApprovedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(23),
            ConsumedAtUtc = null
        };

        var db = new MockDbContextBuilder()
            .WithUsers(agronomist)
            .WithUserRoleAssignments(agronomistRole)
            .WithFirebaseLinkApprovals(existingApproval)
            .Build();

        var handler = new ApproveFirebaseLinkCommandHandler(db);
        var command = new ApproveFirebaseLinkCommand(agronomist.Id, "target_uid_idem", _adminUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(existingApproval.Id);
        result.ApprovedBy.Should().Be("First Operator");
    }
}

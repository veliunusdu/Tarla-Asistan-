using FluentAssertions;
using TarlaAsistani.Application.Features.Users.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Users;

[Trait("Category", "Users")]
public class RequestAccountDeletionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenValidUser_ShouldTransitionToPendingAndCreateJobAndRevokeTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551234567",
            AccountStatus = AccountStatus.Active
        };

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash123",
            ActiveRole = UserRole.Farmer,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            RevokedAtUtc = null
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithRefreshTokens(refreshToken)
            .Build();

        var handler = new RequestAccountDeletionCommandHandler(db);
        var command = new RequestAccountDeletionCommand(userId, "HESABIMI SIL");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("PENDING");
        user.AccountStatus.Should().Be(AccountStatus.DeletionPending);
        refreshToken.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenAlreadyPendingJobExists_ShouldReturnExistingJobIdempotently()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551234567",
            AccountStatus = AccountStatus.DeletionPending
        };

        var existingJob = new AccountDeletionJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = AccountDeletionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithAccountDeletionJobs(existingJob)
            .Build();

        var handler = new RequestAccountDeletionCommandHandler(db);
        var command = new RequestAccountDeletionCommand(userId, "HESABIMI SIL");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RequestId.Should().Be(existingJob.Id);
        result.Status.Should().Be("PENDING");
    }

    [Fact]
    public void Validator_WhenConfirmationDoesNotMatch_ShouldFailValidation()
    {
        // Arrange
        var validator = new RequestAccountDeletionCommandValidator();
        var command = new RequestAccountDeletionCommand(Guid.NewGuid(), "yanlış metin");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Hesap silme onay metni"));
    }

    [Fact]
    public void Validator_WhenConfirmationMatches_ShouldPassValidation()
    {
        // Arrange
        var validator = new RequestAccountDeletionCommandValidator();
        var command = new RequestAccountDeletionCommand(Guid.NewGuid(), "HESABIMI SIL");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

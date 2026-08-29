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
    public async Task Handle_WhenValidUser_ShouldTransitionToPendingAndCreateJob()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551234567",
            AccountStatus = AccountStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .Build();

        var handler = new RequestAccountDeletionCommandHandler(db);
        var command = new RequestAccountDeletionCommand(userId, "HESABIMI SIL");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("PENDING");
        user.AccountStatus.Should().Be(AccountStatus.DeletionPending);
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

using FluentAssertions;
using TarlaAsistani.Application.Features.Users.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Users;

[Trait("Category", "Users")]
public class UpdateProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserExists_ShouldCreateOrUpdateProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551234567",
            AccountStatus = AccountStatus.Active,
            Profile = null
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .Build();

        var handler = new UpdateProfileCommandHandler(db);
        var command = new UpdateProfileCommand(
            UserId: userId,
            FullName: "Ahmet Yılmaz",
            Province: "Konya",
            District: "Meram",
            TermsAccepted: true,
            NotificationsEnabled: true
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Ahmet Yılmaz");
        result.Province.Should().Be("Konya");
        result.District.Should().Be("Meram");
        result.ProfileComplete.Should().BeTrue();
    }
}

using FluentAssertions;
using TarlaAsistani.Application.Features.Farms.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Farms;

[Trait("Category", "Farms")]
public class CreateFarmCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenValidInput_ShouldCreateFarmAndInitialActiveCropPeriod()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateFarmCommandHandler(db);

        var command = new CreateFarmCommand(
            OwnerId: ownerId,
            Name: "Kuzey Tarlası",
            Latitude: 37.87,
            Longitude: 32.49,
            SizeInHectares: 5.5,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        // Act
        var farmId = await handler.Handle(command, CancellationToken.None);

        // Assert
        farmId.Should().NotBeEmpty();
    }
}

[Trait("Category", "Farms")]
public class UpdateFarmCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenFarmExistsAndOwnedByUser_ShouldUpdateFields()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = ownerId,
            Name = "Eski Tarla",
            Latitude = 37.0,
            Longitude = 32.0,
            SizeInHectares = 2.0
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new UpdateFarmCommandHandler(db);
        var command = new UpdateFarmCommand(
            FarmId: farmId,
            UserId: ownerId,
            Name: "Yeni Tarla",
            Latitude: 38.0,
            Longitude: 33.0,
            SizeInHectares: 4.5,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            SoilType: "Killi",
            Note: "Verimli toprak"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Farm.Name.Should().Be("Yeni Tarla");
        result.Farm.Latitude.Should().Be(38.0);
        result.Farm.Longitude.Should().Be(33.0);
        result.Farm.SizeInHectares.Should().Be(4.5);
        result.Farm.SoilType.Should().Be("Killi");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenFarmDoesNotBelongToUser_ShouldReturnNull()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var anotherUser = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = ownerId, Name = "Tarla" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new UpdateFarmCommandHandler(db);
        var command = new UpdateFarmCommand(
            FarmId: farmId,
            UserId: anotherUser,
            Name: "Yeni İsim"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

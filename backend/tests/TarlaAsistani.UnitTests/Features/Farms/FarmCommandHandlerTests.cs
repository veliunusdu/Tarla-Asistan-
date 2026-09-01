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

    [Fact]
    public async Task Handle_WithoutLocation_ShouldCreateFarm()
    {
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateFarmCommandHandler(db);

        var farmId = await handler.Handle(
            new CreateFarmCommand(
                OwnerId: Guid.NewGuid(),
                Name: "Konumsuz Tarla",
                Latitude: null,
                Longitude: null,
                SizeInHectares: null,
                IrrigationMethod: null,
                InitialCropType: CropType.Wheat,
                InitialPlantedAt: new DateOnly(2026, 3, 1)),
            CancellationToken.None);

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

[Trait("Category", "Farms")]
public class ArchiveFarmCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenFarmerArchivesOwnFarm_ShouldArchiveFarmAndReturnTrue()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Benim Tarlam",
            ArchivedAt = null
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new ArchiveFarmCommandHandler(db);
        var command = new ArchiveFarmCommand(farmId, farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        farm.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenFarmerTriesToArchiveAnotherFarmersFarm_ShouldReturnFalseAndNotModifyFarm()
    {
        // Arrange
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmer1Id,
            Name = "Çiftçi 1 Tarlası",
            ArchivedAt = null
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new ArchiveFarmCommandHandler(db);
        var command = new ArchiveFarmCommand(farmId, farmer2Id, UserRole.Farmer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        farm.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAgronomistArchivesFarm_ShouldArchiveFarmAndReturnTrue()
    {
        // Arrange
        var agronomistId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Çiftçi Tarlası",
            ArchivedAt = null
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new ArchiveFarmCommandHandler(db);
        var command = new ArchiveFarmCommand(farmId, agronomistId, UserRole.Agronomist);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        farm.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenFarmDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();

        var handler = new ArchiveFarmCommandHandler(db);
        var command = new ArchiveFarmCommand(Guid.NewGuid(), farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFarmIsAlreadyArchived_ShouldReturnFalse()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = farmId,
            OwnerId = farmerId,
            Name = "Zaten Arşivlenmiş Tarla",
            ArchivedAt = DateTime.UtcNow
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new ArchiveFarmCommandHandler(db);
        var command = new ArchiveFarmCommand(farmId, farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}

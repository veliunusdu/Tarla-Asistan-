using FluentAssertions;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Farms;

[Trait("Category", "Farms")]
public class GetFarmByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenFarmerRequestsOwnFarm_ShouldReturnFarmDto()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = farmerId,
            Name = "Kendi Tarlam",
            SizeInHectares = 5.0
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new GetFarmByIdQueryHandler(db);
        var query = new GetFarmByIdQuery(farm.Id, farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(farm.Id);
        result.OwnerId.Should().Be(farmerId);
        result.Name.Should().Be("Kendi Tarlam");
    }

    [Fact]
    public async Task Handle_WhenFarmerRequestsAnotherFarmersFarm_ShouldReturnNull()
    {
        // Arrange
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var farm1 = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = farmer1Id,
            Name = "Çiftçi 1 Tarlası"
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm1)
            .Build();

        var handler = new GetFarmByIdQueryHandler(db);
        var query = new GetFarmByIdQuery(farm1.Id, farmer2Id, UserRole.Farmer);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAgronomistRequestsAnyFarm_ShouldReturnFarmDto()
    {
        // Arrange
        var agronomistId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = farmerId,
            Name = "Çiftçi Tarlası",
            SizeInHectares = 10.0
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new GetFarmByIdQueryHandler(db);
        var query = new GetFarmByIdQuery(farm.Id, agronomistId, UserRole.Agronomist);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(farm.Id);
        result.OwnerId.Should().Be(farmerId);
    }

    [Fact]
    public async Task Handle_WhenFarmIsArchived_ShouldReturnNull()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var archivedFarm = new Farm
        {
            Id = Guid.NewGuid(),
            OwnerId = farmerId,
            Name = "Arşivlenmiş Tarla",
            ArchivedAt = DateTime.UtcNow
        };

        var db = new MockDbContextBuilder()
            .WithFarms(archivedFarm)
            .Build();

        var handler = new GetFarmByIdQueryHandler(db);
        var query = new GetFarmByIdQuery(archivedFarm.Id, farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenFarmDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var db = new MockDbContextBuilder().Build();

        var handler = new GetFarmByIdQueryHandler(db);
        var query = new GetFarmByIdQuery(Guid.NewGuid(), farmerId, UserRole.Farmer);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

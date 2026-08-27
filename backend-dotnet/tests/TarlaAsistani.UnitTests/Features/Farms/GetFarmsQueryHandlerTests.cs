using FluentAssertions;
using TarlaAsistani.Application.Features.Farms.Queries;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Farms;

[Trait("Category", "Farms")]
public class GetFarmsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenFarmerCalls_ShouldReturnOnlyFarmsOwnedByFarmer()
    {
        // Arrange
        var farmer1 = Guid.NewGuid();
        var farmer2 = Guid.NewGuid();

        var farm1 = new Farm { Id = Guid.NewGuid(), OwnerId = farmer1, Name = "Çiftçi 1 Tarlası" };
        var farm2 = new Farm { Id = Guid.NewGuid(), OwnerId = farmer2, Name = "Çiftçi 2 Tarlası" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm1, farm2)
            .Build();

        var handler = new GetFarmsQueryHandler(db);
        var query = new GetFarmsQuery(farmer1, UserRole.Farmer, IncludeArchived: false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().Id.Should().Be(farm1.Id);
        result.First().Name.Should().Be("Çiftçi 1 Tarlası");
    }

    [Fact]
    public async Task Handle_WhenAgronomistCalls_ShouldReturnAllFarms()
    {
        // Arrange
        var agronomist = Guid.NewGuid();
        var farmer1 = Guid.NewGuid();
        var farmer2 = Guid.NewGuid();

        var farm1 = new Farm { Id = Guid.NewGuid(), OwnerId = farmer1, Name = "Tarla 1" };
        var farm2 = new Farm { Id = Guid.NewGuid(), OwnerId = farmer2, Name = "Tarla 2" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm1, farm2)
            .Build();

        var handler = new GetFarmsQueryHandler(db);
        var query = new GetFarmsQuery(agronomist, UserRole.Agronomist, IncludeArchived: false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenIncludeArchivedIsFalse_ShouldExcludeArchivedFarms()
    {
        // Arrange
        var farmer = Guid.NewGuid();
        var activeFarm = new Farm { Id = Guid.NewGuid(), OwnerId = farmer, Name = "Aktif Tarla" };
        var archivedFarm = new Farm { Id = Guid.NewGuid(), OwnerId = farmer, Name = "Arşiv Tarla", ArchivedAt = DateTime.UtcNow };

        var db = new MockDbContextBuilder()
            .WithFarms(activeFarm, archivedFarm)
            .Build();

        var handler = new GetFarmsQueryHandler(db);
        var query = new GetFarmsQuery(farmer, UserRole.Farmer, IncludeArchived: false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().Id.Should().Be(activeFarm.Id);
    }
}

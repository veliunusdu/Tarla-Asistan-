using FluentAssertions;
using TarlaAsistani.Application.Features.CropPeriods.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.CropPeriods;

[Trait("Category", "CropPeriods")]
public class CropPeriodCommandHandlerTests
{
    [Fact]
    public async Task CreateCropPeriod_WhenActivePeriodExistsAndCloseExistingFalse_ShouldThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla" };
        var activePeriod = new CropPeriod
        {
            FarmId = farmId,
            CropType = CropType.Wheat,
            PlantedAt = new DateOnly(2026, 1, 1),
            Status = CropPeriodStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(activePeriod)
            .Build();

        var handler = new CreateCropPeriodCommandHandler(db);
        var command = new CreateCropPeriodCommand(
            FarmId: farmId,
            UserId: userId,
            CropType: CropType.Corn,
            Variety: "Pioneer",
            PlantedAt: new DateOnly(2026, 4, 1),
            CloseExisting: false
        );

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*close_existing=true*");
    }

    [Fact]
    public async Task CreateCropPeriod_WhenActivePeriodExistsAndCloseExistingTrue_ShouldAutoCloseAndCreateNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla" };
        var activePeriod = new CropPeriod
        {
            FarmId = farmId,
            CropType = CropType.Wheat,
            PlantedAt = new DateOnly(2026, 1, 1),
            Status = CropPeriodStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(activePeriod)
            .Build();

        var handler = new CreateCropPeriodCommandHandler(db);
        var command = new CreateCropPeriodCommand(
            FarmId: farmId,
            UserId: userId,
            CropType: CropType.Corn,
            Variety: "Pioneer",
            PlantedAt: new DateOnly(2026, 4, 1),
            CloseExisting: true
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CropType.Should().Be(CropType.Corn);
        result.Status.Should().Be(CropPeriodStatus.Active);
        activePeriod.Status.Should().Be(CropPeriodStatus.Archived);
        activePeriod.HarvestedAt.Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public async Task CloseCropPeriod_WhenActive_ShouldArchiveAndSetHarvestDate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Tarla" };
        var activePeriod = new CropPeriod
        {
            Id = periodId,
            FarmId = farmId,
            CropType = CropType.Wheat,
            PlantedAt = new DateOnly(2026, 1, 1),
            Status = CropPeriodStatus.Active
        };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .WithCropPeriods(activePeriod)
            .Build();

        var handler = new CloseCropPeriodCommandHandler(db);
        var command = new CloseCropPeriodCommand(farmId, periodId, userId, new DateOnly(2026, 6, 15));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CropPeriodStatus.Archived);
        result.HarvestedAt.Should().Be(new DateOnly(2026, 6, 15));
    }
}

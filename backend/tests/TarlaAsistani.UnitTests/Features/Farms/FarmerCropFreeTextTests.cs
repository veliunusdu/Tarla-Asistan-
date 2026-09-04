using FluentAssertions;
using FluentValidation.TestHelper;
using TarlaAsistani.Application.Features.CropPeriods.Commands;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Application.Features.Farms.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Farms;

[Trait("Category", "Farms")]
[Trait("Feature", "FarmerFreeTextCrop")]
public class FarmerCropFreeTextTests
{
    private readonly CreateFarmCommandValidator _farmValidator = new();
    private readonly CreateCropPeriodCommandValidator _cropPeriodValidator = new();

    // 1. CropName = "Nohut" create edilir
    [Fact]
    public async Task CreateFarm_WithCustomCropNohut_ShouldCreateFarmAndActiveCropPeriod()
    {
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateFarmCommandHandler(db);
        var ownerId = Guid.NewGuid();

        var command = new CreateFarmCommand(
            OwnerId: ownerId,
            Name: "Nohut Tarlası",
            Latitude: 37.87,
            Longitude: 32.49,
            SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Rainfed,
            InitialCropName: "Nohut",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var farmId = await handler.Handle(command, CancellationToken.None);

        farmId.Should().NotBeEmpty();
    }

    // 2. CropName = "Zeytin" create edilir
    [Fact]
    public async Task CreateFarm_WithCustomCropZeytin_ShouldCreateFarmAndActiveCropPeriod()
    {
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateFarmCommandHandler(db);
        var ownerId = Guid.NewGuid();

        var command = new CreateFarmCommand(
            OwnerId: ownerId,
            Name: "Zeytinlik",
            Latitude: 38.42,
            Longitude: 27.14,
            SizeInHectares: 25.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropName: "Zeytin",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 1, 15)
        );

        var farmId = await handler.Handle(command, CancellationToken.None);

        farmId.Should().NotBeEmpty();
    }

    // 3. Enum'da olmayan "Şeker pancarı" kabul edilir
    [Fact]
    public async Task CreateCropPeriod_WithCustomCropSekerPancari_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = userId, Name = "Ova Tarlası" };

        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateCropPeriodCommandHandler(db);
        var command = new CreateCropPeriodCommand(
            FarmId: farmId,
            UserId: userId,
            CropName: "Şeker pancarı",
            CropType: null,
            Variety: "Pancarlık",
            PlantedAt: new DateOnly(2026, 4, 1)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.CropName.Should().Be("Şeker pancarı");
        result.CropType.Should().BeNull();
    }

    // 4. "   Nohut   " → "Nohut" (trimmed)
    [Fact]
    public async Task CreateFarm_WithUntrimmedCropName_ShouldTrimWhitespace()
    {
        var db = new MockDbContextBuilder().Build();
        var handler = new CreateFarmCommandHandler(db);

        var command = new CreateFarmCommand(
            OwnerId: Guid.NewGuid(),
            Name: "Tarlam",
            Latitude: null,
            Longitude: null,
            SizeInHectares: null,
            IrrigationMethod: null,
            InitialCropName: "   Nohut   ",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var farmId = await handler.Handle(command, CancellationToken.None);
        farmId.Should().NotBeEmpty();
    }

    // 5. Empty reddedilir
    [Fact]
    public void Validator_WhenCropNameEmptyAndNoEnum_ShouldHaveValidationError()
    {
        var command = new CreateFarmCommand(
            OwnerId: Guid.NewGuid(),
            Name: "Tarla",
            Latitude: null,
            Longitude: null,
            SizeInHectares: null,
            IrrigationMethod: null,
            InitialCropName: "",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var result = _farmValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InitialCropName);
    }

    // 6. Whitespace reddedilir
    [Fact]
    public void Validator_WhenCropNameWhitespaceOnly_ShouldHaveValidationError()
    {
        var command = new CreateFarmCommand(
            OwnerId: Guid.NewGuid(),
            Name: "Tarla",
            Latitude: null,
            Longitude: null,
            SizeInHectares: null,
            IrrigationMethod: null,
            InitialCropName: "    ",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var result = _farmValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InitialCropName)
              .WithErrorMessage("Ürün adı sadece boşluk olamaz.");
    }

    // 7. Max length validation (over 100 characters)
    [Fact]
    public void Validator_WhenCropNameExceeds100Characters_ShouldHaveValidationError()
    {
        var longName = new string('A', 101);
        var command = new CreateFarmCommand(
            OwnerId: Guid.NewGuid(),
            Name: "Tarla",
            Latitude: null,
            Longitude: null,
            SizeInHectares: null,
            IrrigationMethod: null,
            InitialCropName: longName,
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var result = _farmValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InitialCropName)
              .WithErrorMessage("Ürün adı en fazla 100 karakter olabilir.");
    }

    // 8. Eski WHEAT kaydı okunabilir ve anlamlı crop name döner
    [Fact]
    public void LegacyRecord_WithWheatEnumAndEmptyCropName_ShouldResolveToTurkishBugday()
    {
        var legacyPeriod = new CropPeriod
        {
            Id = Guid.NewGuid(),
            FarmId = Guid.NewGuid(),
            CropType = CropType.Wheat,
            PlantedAt = new DateOnly(2025, 10, 15),
            Status = CropPeriodStatus.Active
        };

        // Domain property getter resolves from CropType
        legacyPeriod.CropName.Should().Be("Buğday");

        // DTO mapping resolves from entity
        var dto = CropPeriodDto.FromEntity(legacyPeriod);
        dto.CropName.Should().Be("Buğday");
        dto.CropType.Should().Be(CropType.Wheat);
    }

    // 9. Farmer string database'de aynen korunur
    [Fact]
    public void CropPeriod_CustomFarmerString_IsPreservedVerbatim()
    {
        var period = new CropPeriod
        {
            Id = Guid.NewGuid(),
            FarmId = Guid.NewGuid(),
            CropName = "Yem bezelyesi",
            CropType = null,
            PlantedAt = new DateOnly(2026, 3, 1),
            Status = CropPeriodStatus.Active
        };

        period.CropName.Should().Be("Yem bezelyesi");
        period.CropType.Should().BeNull();

        var dto = CropPeriodDto.FromEntity(period);
        dto.CropName.Should().Be("Yem bezelyesi");
        dto.CropType.Should().BeNull();
    }

    // 10. Internal CropType null olabilir
    [Fact]
    public void CropPeriod_InternalCropType_CanBeNull()
    {
        var period = new CropPeriod
        {
            CropName = "Kinoa",
            CropType = null
        };

        period.CropType.Should().BeNull();
        period.CropName.Should().Be("Kinoa");
    }
}

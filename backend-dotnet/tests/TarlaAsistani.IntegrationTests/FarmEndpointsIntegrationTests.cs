using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class FarmEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FarmEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAndQueryFarm_ShouldSucceed()
    {
        // 1. Create Farm
        var ownerId = Guid.NewGuid();
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Anadolu Buğday Sahası",
            Latitude: 38.42,
            Longitude: 35.12,
            SizeInHectares: 12.5,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 15)
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        createResult.Should().NotBeNull();
        var farmId = createResult!["id"];
        farmId.Should().NotBeEmpty();

        // 2. Get Farm by ID
        var getResponse = await _client.GetAsync($"/api/v1/farms/{farmId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var farm = await getResponse.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farm.Should().NotBeNull();
        farm!.Name.Should().Be("Anadolu Buğday Sahası");
        farm.CurrentCropPeriod.Should().NotBeNull();
        farm.CurrentCropPeriod!.CropType.Should().Be(CropType.Wheat);

        // 3. Update Farm
        var updateRequest = new UpdateFarmRequest(
            UserId: ownerId,
            Name: "Anadolu Buğday Sahası - Güncellendi",
            Latitude: 38.45,
            Longitude: 35.15,
            SizeInHectares: 15.0,
            IrrigationMethod: IrrigationMethod.Drip,
            SoilType: "Kumlu Tınlı",
            Note: "Sulama sistemi modernize edildi"
        );

        var updateResponse = await _client.PatchAsJsonAsync($"/api/v1/farms/{farmId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedResult = await updateResponse.Content.ReadFromJsonAsync<FarmMutationResultDto>(CustomWebApplicationFactory.JsonOptions);
        updatedResult.Should().NotBeNull();
        updatedResult!.Farm.Name.Should().Be("Anadolu Buğday Sahası - Güncellendi");
        updatedResult.Farm.SizeInHectares.Should().Be(15.0);
    }
}

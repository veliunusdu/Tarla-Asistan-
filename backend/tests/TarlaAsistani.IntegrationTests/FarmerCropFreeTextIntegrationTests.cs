using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.CropPeriods.DTOs;
using TarlaAsistani.Application.Features.Farms.DTOs;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "Farms")]
[Trait("Feature", "FarmerFreeTextCrop")]
public class FarmerCropFreeTextIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FarmerCropFreeTextIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // 11. API response crop_name döndürür
    [Fact]
    public async Task CreateFarm_WithCustomCrop_ApiResponseShouldContainCropName()
    {
        var ownerId = Guid.NewGuid();
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Güneydoğu Nohut Sahası",
            Latitude: 37.15,
            Longitude: 38.79,
            SizeInHectares: 20.0,
            IrrigationMethod: null,
            InitialCropName: "Nohut",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest, CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        createResult.Should().NotBeNull();
        var farmId = createResult!["id"];

        // Query farm by ID
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        getRequest.Headers.Add("X-User-Id", ownerId.ToString());
        getRequest.Headers.Add("X-User-Role", "Farmer");

        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var farm = await getResponse.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farm.Should().NotBeNull();
        farm!.CurrentCropPeriod.Should().NotBeNull();
        farm.CurrentCropPeriod!.CropName.Should().Be("Nohut");
        farm.CurrentCropPeriod.CropType.Should().BeNull();

        // Also check raw json property crop_name
        var rawJson = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var hasCrop = doc.RootElement.TryGetProperty("current_crop_period", out var currentCrop)
                      || doc.RootElement.TryGetProperty("current_crop", out currentCrop);
        hasCrop.Should().BeTrue();
        currentCrop.GetProperty("crop_name").GetString().Should().Be("Nohut");
    }

    // 12. Update/new season akışı custom crop destekler
    [Fact]
    public async Task CreateCropPeriod_WithCustomCrop_ShouldSupportNewSeasonWithCustomCrop()
    {
        // 1. Create farm with initial crop "Buğday"
        var ownerId = Guid.NewGuid();
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Çukurova Sahası",
            Latitude: 36.99,
            Longitude: 35.32,
            SizeInHectares: 35.0,
            IrrigationMethod: null,
            InitialCropName: "Buğday",
            InitialCropType: null,
            InitialPlantedAt: new DateOnly(2025, 10, 15)
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest, CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Start new crop period with custom crop "Zeytin" and close_existing = true
        var newSeasonRequest = new CreateCropPeriodApiRequest(
            UserId: ownerId,
            CropName: "Zeytin",
            CropType: null,
            Variety: "Gemlik",
            PlantedAt: new DateOnly(2026, 3, 15),
            CloseExisting: true
        );

        using var postSeasonRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/production-periods")
        {
            Content = JsonContent.Create(newSeasonRequest, options: CustomWebApplicationFactory.JsonOptions)
        };
        postSeasonRequest.Headers.Add("X-User-Id", ownerId.ToString());

        var seasonResponse = await _client.SendAsync(postSeasonRequest);
        seasonResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdSeason = await seasonResponse.Content.ReadFromJsonAsync<TarlaAsistani.Application.Features.CropPeriods.DTOs.CropPeriodDto>(CustomWebApplicationFactory.JsonOptions);
        createdSeason.Should().NotBeNull();
        createdSeason!.CropName.Should().Be("Zeytin");
        createdSeason.CropType.Should().BeNull();
        createdSeason.Variety.Should().Be("Gemlik");

        // 3. Verify that farm now has "Zeytin" as current crop
        using var getFarmRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        getFarmRequest.Headers.Add("X-User-Id", ownerId.ToString());
        getFarmRequest.Headers.Add("X-User-Role", "Farmer");

        var getFarmResponse = await _client.SendAsync(getFarmRequest);
        getFarmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var farm = await getFarmResponse.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farm.Should().NotBeNull();
        farm!.CurrentCropPeriod.Should().NotBeNull();
        farm.CurrentCropPeriod!.CropName.Should().Be("Zeytin");
    }
}

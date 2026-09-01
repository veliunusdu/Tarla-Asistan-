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

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", createRequest, CustomWebApplicationFactory.JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        createResult.Should().NotBeNull();
        var farmId = createResult!["id"];
        farmId.Should().NotBeEmpty();

        // 2. Get Farm by ID (as owner)
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        getRequest.Headers.Add("X-User-Id", ownerId.ToString());
        getRequest.Headers.Add("X-User-Role", "Farmer");
        var getResponse = await _client.SendAsync(getRequest);
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

        var updateResponse = await _client.PatchAsJsonAsync($"/api/v1/farms/{farmId}", updateRequest, CustomWebApplicationFactory.JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedResult = await updateResponse.Content.ReadFromJsonAsync<FarmMutationResultDto>(CustomWebApplicationFactory.JsonOptions);
        updatedResult.Should().NotBeNull();
        updatedResult!.Farm.Name.Should().Be("Anadolu Buğday Sahası - Güncellendi");
        updatedResult.Farm.SizeInHectares.Should().Be(15.0);
    }

    [Fact]
    public async Task CreateAndQueryFarm_WithoutLocation_ShouldPersistNullCoordinates()
    {
        var ownerId = Guid.NewGuid();
        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Konumsuz Tarla",
            Latitude: null,
            Longitude: null,
            SizeInHectares: 2.5,
            IrrigationMethod: null,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 15)
        );

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/farms",
            createRequest,
            CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(
            CustomWebApplicationFactory.JsonOptions);
        createResult.Should().NotBeNull();

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{createResult!["id"]}");
        getRequest.Headers.Add("X-User-Id", ownerId.ToString());
        getRequest.Headers.Add("X-User-Role", "Farmer");
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var farm = await getResponse.Content.ReadFromJsonAsync<FarmDto>(
            CustomWebApplicationFactory.JsonOptions);

        farm.Should().NotBeNull();
        farm!.Latitude.Should().BeNull();
        farm.Longitude.Should().BeNull();
    }

    [Fact]
    public async Task ListFarms_WhenFarmerQueries_ShouldOnlyReturnFarmsOwnedByFarmer()
    {
        // 1. Create two farms for farmer A, one for farmer B
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();

        await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Çiftçi A Tarlası 1",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 2.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Çiftçi A Tarlası 2",
            Latitude: 38.1, Longitude: 35.1, SizeInHectares: 3.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Corn,
            InitialPlantedAt: new DateOnly(2026, 4, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerB,
            Name: "Çiftçi B Tarlası",
            Latitude: 39.0, Longitude: 36.0, SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Sunflower,
            InitialPlantedAt: new DateOnly(2026, 5, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        // 2. Query as farmer A
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms?userId={farmerA}&role={UserRole.Farmer}");
        request.Headers.Add("X-User-Id", farmerA.ToString());
        request.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<FarmDto>>(CustomWebApplicationFactory.JsonOptions);
        list.Should().NotBeNull();
        list!.Should().OnlyContain(f => f.OwnerId == farmerA);
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFarmById_WhenFarmerRequestsAnotherFarmersFarm_ShouldReturnNotFound()
    {
        // 1. Create farm owned by Farmer A
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Gizli Tarla",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 5.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Attempt to query as Farmer B (unauthorized)
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        request.Headers.Add("X-User-Id", farmerB.ToString());
        request.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(request);

        // 3. Assert NotFound
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFarmById_WhenFarmerRequestsOwnFarm_ShouldReturnOk()
    {
        // 1. Create farm owned by Farmer A
        var farmerA = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Benim Tarlam",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 7.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Query as Farmer A (owner)
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        request.Headers.Add("X-User-Id", farmerA.ToString());
        request.Headers.Add("X-User-Role", "Farmer");

        var response = await _client.SendAsync(request);

        // 3. Assert OK and matching data
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var farm = await response.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farm.Should().NotBeNull();
        farm!.Id.Should().Be(farmId);
        farm.OwnerId.Should().Be(farmerA);
        farm.Name.Should().Be("Benim Tarlam");
    }

    [Fact]
    public async Task GetFarmById_WhenAgronomistRequestsAnyFarm_ShouldReturnOk()
    {
        // 1. Create farm owned by Farmer A
        var farmerA = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Mühendis İnceleme Tarlası",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 8.0,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Corn,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Query as Agronomist
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        request.Headers.Add("X-User-Id", agronomistId.ToString());
        request.Headers.Add("X-User-Role", "Agronomist");

        var response = await _client.SendAsync(request);

        // 3. Assert OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var farm = await response.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farm.Should().NotBeNull();
        farm!.Id.Should().Be(farmId);
        farm.OwnerId.Should().Be(farmerA);
    }

    [Fact]
    public async Task ArchiveFarm_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var farmId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/farms/{farmId}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ArchiveFarm_WhenFarmerTriesToArchiveAnotherFarmersFarm_ShouldReturnNotFoundAndNotArchive()
    {
        // 1. Farmer A creates farm
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Korunan Tarla",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 6.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Farmer B tries to archive Farmer A's farm
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/farms/{farmId}");
        deleteRequest.Headers.Add("X-User-Id", farmerB.ToString());
        deleteRequest.Headers.Add("X-User-Role", "Farmer");

        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Verify farm is STILL intact and accessible by Farmer A
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        getRequest.Headers.Add("X-User-Id", farmerA.ToString());
        getRequest.Headers.Add("X-User-Role", "Farmer");

        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ArchiveFarm_WhenFarmerArchivesOwnFarm_ShouldReturnNoContentAndHideFarm()
    {
        // 1. Farmer A creates farm
        var farmerA = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Silinecek Tarla",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 4.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Farmer A archives own farm
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/farms/{farmId}");
        deleteRequest.Headers.Add("X-User-Id", farmerA.ToString());
        deleteRequest.Headers.Add("X-User-Role", "Farmer");

        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Verify farm is no longer accessible
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}");
        getRequest.Headers.Add("X-User-Id", farmerA.ToString());
        getRequest.Headers.Add("X-User-Role", "Farmer");

        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveFarm_WhenAgronomistArchivesFarm_ShouldReturnNoContent()
    {
        // 1. Farmer A creates farm
        var farmerA = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();

        var createResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Mühendisin Sileceği Tarla",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 9.0,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Barley,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = createResult!["id"];

        // 2. Agronomist archives farm
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/farms/{farmId}");
        deleteRequest.Headers.Add("X-User-Id", agronomistId.ToString());
        deleteRequest.Headers.Add("X-User-Role", "Agronomist");

        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

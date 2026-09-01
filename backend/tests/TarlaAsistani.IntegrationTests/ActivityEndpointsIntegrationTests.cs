using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class ActivityEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ActivityEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateActivity_WhenFarmerCreatesActivityOnAnotherFarmersFarm_ShouldReturnForbiddenAndNotWriteToDb()
    {
        // 1. Farmer A creates a farm
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();

        var farmResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Çiftçi A Tarlası",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 5.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        farmResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await farmResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = farmResult!["id"];

        // 2. Farmer B attempts to create activity on Farmer A's farm
        using var createActivityRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities");
        createActivityRequest.Headers.Add("X-User-Id", farmerB.ToString());
        createActivityRequest.Headers.Add("X-User-Role", "Farmer");
        createActivityRequest.Content = JsonContent.Create(new CreateActivityApiRequest(
            UserId: farmerB,
            ActivityType: ActivityType.Irrigation,
            Description: "İzinsiz sulama denemesi",
            OccurredAt: DateTime.UtcNow,
            CropPeriodId: null,
            InputMethod: ActivitySource.Manual,
            DurationMinutes: 60,
            Amount: 100,
            Unit: "Litre",
            PhotoUrl: null,
            VoiceUrl: null,
            VoiceTranscript: null,
            PerformedBy: null,
            Cost: null,
            ClientOperationId: null
        ), options: CustomWebApplicationFactory.JsonOptions);

        var response = await _client.SendAsync(createActivityRequest);

        // 3. Assert Forbidden (403)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Verify no activities were recorded on Farmer A's farm
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}/activities");
        listRequest.Headers.Add("X-User-Id", farmerA.ToString());
        listRequest.Headers.Add("X-User-Role", "Farmer");

        var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.Content.ReadFromJsonAsync<ActivityListDto>(CustomWebApplicationFactory.JsonOptions);
        listResult.Should().NotBeNull();
        listResult!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateActivity_WhenFarmerCreatesActivityOnOwnFarm_ShouldReturnCreated()
    {
        // 1. Farmer A creates a farm
        var farmerA = Guid.NewGuid();

        var farmResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Kendi Tarlam Faaliyet Test",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 4.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        farmResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await farmResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = farmResult!["id"];

        // 2. Farmer A creates activity on own farm
        using var createActivityRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities");
        createActivityRequest.Headers.Add("X-User-Id", farmerA.ToString());
        createActivityRequest.Headers.Add("X-User-Role", "Farmer");
        createActivityRequest.Content = JsonContent.Create(new CreateActivityApiRequest(
            UserId: farmerA,
            ActivityType: ActivityType.Fertilization,
            Description: "Gübreleme yapıldı",
            OccurredAt: DateTime.UtcNow,
            CropPeriodId: null,
            InputMethod: ActivitySource.Manual,
            DurationMinutes: 45,
            Amount: 50,
            Unit: "kg",
            PhotoUrl: null,
            VoiceUrl: null,
            VoiceTranscript: null,
            PerformedBy: null,
            Cost: 200,
            ClientOperationId: null
        ), options: CustomWebApplicationFactory.JsonOptions);

        var response = await _client.SendAsync(createActivityRequest);

        // 3. Assert Created (201)
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ActivityDto>(CustomWebApplicationFactory.JsonOptions);
        created.Should().NotBeNull();
        created!.FarmId.Should().Be(farmId);
        created.Description.Should().Be("Gübreleme yapıldı");
    }

    [Fact]
    public async Task CreateActivity_WhenAgronomistCreatesActivityOnAnyFarm_ShouldReturnCreated()
    {
        // 1. Farmer A creates a farm
        var farmerA = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();

        var farmResponse = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmerA,
            Name: "Mühendis Faaliyet Test",
            Latitude: 38.0, Longitude: 35.0, SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Corn,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        farmResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await farmResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farmId = farmResult!["id"];

        // 2. Agronomist creates activity
        using var createActivityRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities");
        createActivityRequest.Headers.Add("X-User-Id", agronomistId.ToString());
        createActivityRequest.Headers.Add("X-User-Role", "Agronomist");
        createActivityRequest.Content = JsonContent.Create(new CreateActivityApiRequest(
            UserId: agronomistId,
            ActivityType: ActivityType.Spraying,
            Description: "Zirai mücadele ilacı uygulandı",
            OccurredAt: DateTime.UtcNow,
            CropPeriodId: null,
            InputMethod: ActivitySource.Manual,
            DurationMinutes: 90,
            Amount: 20,
            Unit: "Litre",
            PhotoUrl: null,
            VoiceUrl: null,
            VoiceTranscript: null,
            PerformedBy: null,
            Cost: 500,
            ClientOperationId: null
        ), options: CustomWebApplicationFactory.JsonOptions);

        var response = await _client.SendAsync(createActivityRequest);

        // 3. Assert Created (201)
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ActivityDto>(CustomWebApplicationFactory.JsonOptions);
        created.Should().NotBeNull();
        created!.FarmId.Should().Be(farmId);
    }
}

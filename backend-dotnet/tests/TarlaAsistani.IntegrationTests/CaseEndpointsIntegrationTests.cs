using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.IntegrationTests;

public class CaseEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CaseEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCaseAndSendMessage_ShouldSucceed()
    {
        // 1. Create a Farm
        var farmerId = Guid.NewGuid();
        var createFarmRequest = new CreateFarmRequest(
            OwnerId: farmerId,
            Name: "Vaka Test Tarlası",
            Latitude: 37.87,
            Longitude: 32.49,
            SizeInHectares: 5.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Tomato,
            InitialPlantedAt: new DateOnly(2026, 4, 15)
        );

        var createFarmResponse = await _client.PostAsJsonAsync("/api/v1/farms", createFarmRequest);
        createFarmResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await createFarmResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var farmId = farmResult!["id"];

        // 2. Create Support Case
        var createCaseRequest = new CreateCaseApiRequest(
            UserId: farmerId,
            FarmId: farmId,
            Category: CaseCategory.Disease,
            Title: "Domates Yaprak Kıvırcıklığı",
            Description: "Yapraklarda sararma ve kıvrılma başladı",
            MediaIds: null,
            ClientOperationId: null
        );

        var createCaseResponse = await _client.PostAsJsonAsync("/api/v1/cases", createCaseRequest);
        createCaseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdCase = await createCaseResponse.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        createdCase.Should().NotBeNull();
        createdCase!.Title.Should().Be("Domates Yaprak Kıvırcıklığı");
        createdCase.Status.Should().Be(CaseStatus.Open);

        // 3. Add a Message to Case
        var agronomistId = Guid.NewGuid();
        var addMessageRequest = new CreateCaseMessageApiRequest(
            UserId: agronomistId,
            Role: UserRole.Agronomist,
            MessageType: CaseMessageType.AdditionalInfoRequest,
            Body: "Yaprağın alt kısmındaki böcek varlığını kontrol edebilir misiniz?",
            MediaIds: null,
            ClientOperationId: null
        );

        var addMessageResponse = await _client.PostAsJsonAsync($"/api/v1/cases/{createdCase.Id}/messages", addMessageRequest);
        addMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var messageResult = await addMessageResponse.Content.ReadFromJsonAsync<CaseMessageDto>(CustomWebApplicationFactory.JsonOptions);
        messageResult.Should().NotBeNull();
        messageResult!.MessageType.Should().Be(CaseMessageType.AdditionalInfoRequest);

        // 4. Query Case by ID -> Status should be WaitingFarmer
        var getCaseResponse = await _client.GetAsync($"/api/v1/cases/{createdCase.Id}?userId={farmerId}&role={UserRole.Farmer}");
        getCaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedCase = await getCaseResponse.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(CaseStatus.WaitingFarmer);
    }
}

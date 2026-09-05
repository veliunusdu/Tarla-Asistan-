using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

public class CaseEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CaseEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCaseAndSendMessage_ShouldSucceed()
    {
        // 0. Seed Farmer & Agronomist Users in test database
        var farmerId = Guid.NewGuid();
        var agronomistId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(
                new User
                {
                    Id = farmerId,
                    PhoneNumber = "+905551112233",
                    Role = UserRole.Farmer,
                    AccountStatus = AccountStatus.Active,
                    Profile = new Profile
                    {
                        UserId = farmerId,
                        FullName = "Hasan Çiftçi",
                        NotificationsEnabled = true
                    }
                },
                new User
                {
                    Id = agronomistId,
                    PhoneNumber = "+905554445566",
                    Role = UserRole.Agronomist,
                    AccountStatus = AccountStatus.Active,
                    Profile = new Profile
                    {
                        UserId = agronomistId,
                        FullName = "Ayşe Uzman",
                        NotificationsEnabled = true
                    }
                }
            );
            await db.SaveChangesAsync();
        }

        // 1. Create a Farm
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

        var createFarmResponse = await _client.PostAsJsonAsync("/api/v1/farms", createFarmRequest, CustomWebApplicationFactory.JsonOptions);
        createFarmResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var farmResult = await createFarmResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
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

        var createCaseResponse = await _client.PostAsJsonAsync("/api/v1/cases", createCaseRequest, CustomWebApplicationFactory.JsonOptions);
        createCaseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdCase = await createCaseResponse.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        createdCase.Should().NotBeNull();
        createdCase!.Title.Should().Be("Domates Yaprak Kıvırcıklığı");
        createdCase.Status.Should().Be(CaseStatus.Open);
        createdCase.Context.Should().NotBeNull();
        createdCase.Context!.FarmName.Should().Be("Vaka Test Tarlası");
        createdCase.Context.CropName.Should().Be("Domates");

        // 3. Add a Message to Case
        var addMessageRequest = new CreateCaseMessageApiRequest(
            UserId: agronomistId,
            Role: UserRole.Agronomist,
            MessageType: CaseMessageType.AdditionalInfoRequest,
            Body: "Yaprağın alt kısmındaki böcek varlığını kontrol edebilir misiniz?",
            MediaIds: null,
            ClientOperationId: null
        );

        var addMessageResponse = await _client.PostAsJsonAsync($"/api/v1/cases/{createdCase.Id}/messages", addMessageRequest, CustomWebApplicationFactory.JsonOptions);
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

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.IntegrationTests;

public class ProactiveAdvisoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProactiveAdvisoryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ownerId, Guid farmId)> CreateFarmWithUserAsync()
    {
        var ownerId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Id = ownerId,
                PhoneNumber = $"+90555{Random.Shared.Next(1000000, 9999999)}",
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active
            };
            var profile = new Profile
            {
                UserId = ownerId,
                FullName = "Proaktif Çiftçi",
                NotificationsEnabled = true
            };
            db.Users.Add(user);
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
        }

        var createRequest = new CreateFarmRequest(
            OwnerId: ownerId,
            Name: "Proaktif Mısır Tarlası",
            Latitude: 39.92,
            Longitude: 32.85,
            SizeInHectares: 15.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Corn,
            InitialPlantedAt: new DateOnly(2026, 4, 15)
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/farms")
        {
            Content = JsonContent.Create(createRequest, options: CustomWebApplicationFactory.JsonOptions)
        };
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var farmDict = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        return (ownerId, farmDict!["id"]);
    }

    [Fact]
    public async Task EvaluateFarm_WhenHeavyRain_ShouldGenerateFertilizerDelayAdvisoryAndReturnViaApi()
    {
        var (ownerId, farmId) = await CreateFarmWithUserAsync();

        // 1. Create a planned fertilization task
        var now = DateTime.UtcNow;
        var tomorrow = DateOnly.FromDateTime(now).AddDays(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = new FarmTask
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                Title = "Azot Üst Gübreleme",
                Description = "200 kg üre atılacak",
                Reason = "Mısır gelişimi için azot takviyesi",
                DueDate = tomorrow,
                Status = TaskStatus.Planned,
                DedupeKey = $"task-fert-{Guid.NewGuid():N}"
            };
            db.FarmTasks.Add(task);
            await db.SaveChangesAsync();
        }

        // 2. Setup mock weather forecast with heavy rain
        var points = new List<WeatherPoint>
        {
            new(now.AddDays(1), TemperatureC: 19, PrecipitationProbability: 90, PrecipitationMm: 18.5, WindSpeedKmh: 12),
            new(now.AddDays(2), TemperatureC: 22, PrecipitationProbability: 5, PrecipitationMm: 0, WindSpeedKmh: 8)
        };

        _factory.MockWeatherProvider
            .Setup(w => w.ForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);
        _factory.MockWeatherProvider
            .Setup(w => w.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecastData(points));

        // 3. Trigger evaluation via POST /api/v1/ai/advisories/evaluate/{farmId}
        var evalRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/evaluate/{farmId}");
        evalRequest.Headers.Add("X-User-Id", ownerId.ToString());

        var evalResponse = await _client.SendAsync(evalRequest);
        var evalBody = await evalResponse.Content.ReadAsStringAsync();
        evalResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: $"API returned {evalResponse.StatusCode}: {evalBody}");

        var advisories = await evalResponse.Content.ReadFromJsonAsync<List<ProactiveAdvisoryDto>>(CustomWebApplicationFactory.JsonOptions);
        advisories.Should().NotBeNull();
        advisories!.Should().Contain(a => a.AdvisoryType == ProactiveAdvisoryType.FertilizerDelay);

        var delayAdv = advisories.First(a => a.AdvisoryType == ProactiveAdvisoryType.FertilizerDelay);
        delayAdv.Severity.Should().Be(AdvisorySeverity.Critical);
        delayAdv.ActionType.Should().Be(ProactiveActionType.PostponeTask);
        delayAdv.RecommendedDate.Should().Be(tomorrow.AddDays(1));

        // 4. Query active advisories via GET /api/v1/ai/advisories
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ai/advisories");
        getRequest.Headers.Add("X-User-Id", ownerId.ToString());

        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeList = await getResponse.Content.ReadFromJsonAsync<List<ProactiveAdvisoryDto>>(CustomWebApplicationFactory.JsonOptions);
        activeList!.Should().Contain(a => a.Id == delayAdv.Id);

        // 5. Apply the advisory via POST /api/v1/ai/advisories/{id}/apply
        var applyRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{delayAdv.Id}/apply");
        applyRequest.Headers.Add("X-User-Id", ownerId.ToString());

        var applyResponse = await _client.SendAsync(applyRequest);
        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that the task's due date in database was postponed!
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var updatedTask = await db.FarmTasks.FirstAsync(t => t.FarmId == farmId);
            updatedTask.DueDate.Should().Be(tomorrow.AddDays(1));
            updatedTask.Status.Should().Be(TaskStatus.Planned);

            var updatedAdvisory = await db.ProactiveAdvisories.FirstAsync(a => a.Id == delayAdv.Id);
            updatedAdvisory.IsApplied.Should().BeTrue();
        }

        // 6. Dismiss the advisory via POST /api/v1/ai/advisories/{id}/dismiss
        var dismissRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{delayAdv.Id}/dismiss");
        dismissRequest.Headers.Add("X-User-Id", ownerId.ToString());

        var dismissResponse = await _client.SendAsync(dismissRequest);
        dismissResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify active list no longer contains the dismissed advisory
        var getAfterDismiss = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ai/advisories");
        getAfterDismiss.Headers.Add("X-User-Id", ownerId.ToString());
        var getAfterDismissResponse = await _client.SendAsync(getAfterDismiss);
        var listAfterDismiss = await getAfterDismissResponse.Content.ReadFromJsonAsync<List<ProactiveAdvisoryDto>>(CustomWebApplicationFactory.JsonOptions);
        listAfterDismiss!.Should().NotContain(a => a.Id == delayAdv.Id);
    }
}

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

        var delayAdv = advisories!.First(a => a.AdvisoryType == ProactiveAdvisoryType.FertilizerDelay);
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

    [Fact]
    public async Task ApplyAdvisory_WhenNotFound_Returns404()
    {
        var (ownerId, _) = await CreateFarmWithUserAsync();
        var nonExistentId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{nonExistentId}/apply");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApplyAdvisory_ConsecutiveCalls_FirstReturns200_SecondReturns409WithoutMutatingTask()
    {
        var (ownerId, farmId) = await CreateFarmWithUserAsync();
        var advisoryId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var originalDueDate = new DateOnly(2026, 6, 10);
        var recommendedDate = new DateOnly(2026, 6, 15);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = new FarmTask
            {
                Id = taskId,
                FarmId = farmId,
                Title = "İlaçlama Görevi",
                Description = "İlaçlama açıklaması",
                Reason = "Hastalık önleme",
                DueDate = originalDueDate,
                Status = TaskStatus.New,
                DedupeKey = $"task-{taskId:N}"
            };
            var advisory = new ProactiveAdvisory
            {
                Id = advisoryId,
                FarmId = farmId,
                UserId = ownerId,
                RelatedTaskId = taskId,
                Title = "Rüzgar Riski - İlaçlama Ertele",
                Summary = "Yüksek rüzgar nedeniyle erteleme tavsiye ediliyor",
                AgronomicExplanation = "Yüksek rüzgar ilacın sürüklenmesine yol açar.",
                ActionRecommendation = "İlaçlamayı ertesi güne erteleyin.",
                RecommendedDate = recommendedDate,
                IsApplied = false,
                IsDismissed = false,
                DedupeKey = $"adv-{advisoryId:N}"
            };
            db.FarmTasks.Add(task);
            db.ProactiveAdvisories.Add(advisory);
            await db.SaveChangesAsync();
        }

        // Call 1: First call -> 200 OK
        var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{advisoryId}/apply");
        firstRequest.Headers.Add("X-User-Id", ownerId.ToString());
        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        DateTime firstTaskUpdatedAt;
        DateTime? firstAdvisoryAppliedAt;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.FarmTasks.FirstAsync(t => t.Id == taskId);
            task.DueDate.Should().Be(recommendedDate);
            task.Status.Should().Be(TaskStatus.Planned);
            firstTaskUpdatedAt = task.UpdatedAtUtc;

            var adv = await db.ProactiveAdvisories.FirstAsync(a => a.Id == advisoryId);
            adv.IsApplied.Should().BeTrue();
            firstAdvisoryAppliedAt = adv.AppliedAtUtc;
            firstAdvisoryAppliedAt.Should().NotBeNull();
        }

        // Call 2: Consecutive duplicate call -> 409 Conflict
        var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{advisoryId}/apply");
        secondRequest.Headers.Add("X-User-Id", ownerId.ToString());
        var secondResponse = await _client.SendAsync(secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify no second mutation: DueDate, UpdatedAtUtc, AppliedAtUtc unchanged
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.FarmTasks.FirstAsync(t => t.Id == taskId);
            task.DueDate.Should().Be(recommendedDate);
            task.UpdatedAtUtc.Should().Be(firstTaskUpdatedAt);

            var adv = await db.ProactiveAdvisories.FirstAsync(a => a.Id == advisoryId);
            adv.AppliedAtUtc.Should().Be(firstAdvisoryAppliedAt);
        }
    }

    [Fact]
    public async Task ApplyWeatherAdvisory_WhenExpired_Returns409()
    {
        var (ownerId, farmId) = await CreateFarmWithUserAsync();
        var advisoryId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var originalDueDate = new DateOnly(2026, 6, 10);
        var recommendedDate = new DateOnly(2026, 6, 15);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = new FarmTask
            {
                Id = taskId,
                FarmId = farmId,
                Title = "İlaçlama Görevi",
                Description = "İlaçlama açıklaması",
                Reason = "Zararlı kontrolü",
                DueDate = originalDueDate,
                Status = TaskStatus.New,
                DedupeKey = $"task-exp-{taskId:N}"
            };
            var advisory = new ProactiveAdvisory
            {
                Id = advisoryId,
                FarmId = farmId,
                UserId = ownerId,
                RelatedTaskId = taskId,
                Title = "Rüzgar Riski - İlaçlama Ertele",
                Summary = "Erteleme tavsiyesi",
                AgronomicExplanation = "Yüksek rüzgar.",
                ActionRecommendation = "Erteleyin.",
                RecommendedDate = recommendedDate,
                ValidUntilUtc = DateTime.UtcNow.AddMinutes(-15), // Expired 15 mins ago
                IsApplied = false,
                IsDismissed = false,
                DedupeKey = $"adv-exp-{advisoryId:N}"
            };
            db.FarmTasks.Add(task);
            db.ProactiveAdvisories.Add(advisory);
            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{advisoryId}/apply");
        request.Headers.Add("X-User-Id", ownerId.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify task not mutated and advisory not applied
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.FarmTasks.FirstAsync(t => t.Id == taskId);
            task.DueDate.Should().Be(originalDueDate);

            var adv = await db.ProactiveAdvisories.FirstAsync(a => a.Id == advisoryId);
            adv.IsApplied.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ApplyAdvisory_WhenDismissed_Returns409()
    {
        var (ownerId, farmId) = await CreateFarmWithUserAsync();
        var advisoryId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var originalDueDate = new DateOnly(2026, 6, 10);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = new FarmTask
            {
                Id = taskId,
                FarmId = farmId,
                Title = "Sulama Görevi",
                Description = "Sulama açıklaması",
                Reason = "Nem ihtiyacı",
                DueDate = originalDueDate,
                Status = TaskStatus.Planned,
                DedupeKey = $"task-{taskId:N}"
            };
            var advisory = new ProactiveAdvisory
            {
                Id = advisoryId,
                FarmId = farmId,
                UserId = ownerId,
                RelatedTaskId = taskId,
                Title = "Sulama Tavsiyesi",
                Summary = "Yağış bekleniyor",
                AgronomicExplanation = "Yağış yeterli su sağlayacaktır.",
                ActionRecommendation = "Sulamayı erteleyin.",
                RecommendedDate = new DateOnly(2026, 6, 13),
                IsApplied = false,
                IsDismissed = true,
                DismissedAtUtc = DateTime.UtcNow,
                DedupeKey = $"adv-{advisoryId:N}"
            };
            db.FarmTasks.Add(task);
            db.ProactiveAdvisories.Add(advisory);
            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{advisoryId}/apply");
        request.Headers.Add("X-User-Id", ownerId.ToString());
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify task DueDate was not modified
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.FarmTasks.FirstAsync(t => t.Id == taskId);
            task.DueDate.Should().Be(originalDueDate);
        }
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.NotApplied)]
    [InlineData(TaskStatus.Cancelled)]
    public async Task ApplyAdvisory_WhenRelatedTaskInTerminalState_Returns409(TaskStatus terminalStatus)
    {
        var (ownerId, farmId) = await CreateFarmWithUserAsync();
        var advisoryId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var originalDueDate = new DateOnly(2026, 6, 10);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = new FarmTask
            {
                Id = taskId,
                FarmId = farmId,
                Title = "Terminal Görev",
                Description = "Terminal açıklama",
                Reason = "Terminal sebep",
                DueDate = originalDueDate,
                Status = terminalStatus,
                DedupeKey = $"task-{taskId:N}"
            };
            var advisory = new ProactiveAdvisory
            {
                Id = advisoryId,
                FarmId = farmId,
                UserId = ownerId,
                RelatedTaskId = taskId,
                Title = "Tavsiye",
                Summary = "Açıklama",
                AgronomicExplanation = "Agronomik açıklama",
                ActionRecommendation = "Eylem tavsiyesi",
                RecommendedDate = new DateOnly(2026, 6, 14),
                IsApplied = false,
                IsDismissed = false,
                DedupeKey = $"adv-{advisoryId:N}"
            };
            db.FarmTasks.Add(task);
            db.ProactiveAdvisories.Add(advisory);
            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ai/advisories/{advisoryId}/apply");
        request.Headers.Add("X-User-Id", ownerId.ToString());
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify task status and DueDate were not changed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.FarmTasks.FirstAsync(t => t.Id == taskId);
            task.Status.Should().Be(terminalStatus);
            task.DueDate.Should().Be(originalDueDate);
        }
    }
}

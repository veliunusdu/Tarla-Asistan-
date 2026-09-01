using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Activities.DTOs;
using TarlaAsistani.Application.Features.Farms.DTOs;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "Security")]
public class MultiTenantCrossUserIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MultiTenantCrossUserIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedUserAsync(Guid userId, string phone, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                PhoneNumber = phone,
                Role = role,
                AccountStatus = AccountStatus.Active,
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task<Guid> CreateFarmAsUser(Guid userId, string farmName)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: userId,
            Name: farmName,
            Latitude: 38.5,
            Longitude: 35.5,
            SizeInHectares: 12.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        return result!["id"];
    }

    private async Task<Guid> CreateActivityAsUser(Guid farmId, Guid userId, UserRole role, string description)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities");
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-User-Role", role.ToString());
        req.Content = JsonContent.Create(new CreateActivityApiRequest(
            UserId: userId,
            ActivityType: ActivityType.Irrigation,
            Description: description,
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
            Cost: 150,
            ClientOperationId: null
        ), options: CustomWebApplicationFactory.JsonOptions);

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ActivityDto>(CustomWebApplicationFactory.JsonOptions);
        return created!.Id;
    }

    [Fact]
    public async Task CrossTenant_TarlaListeleme_UserBCannotSeeUserAFarms_AgronomistSeesAll()
    {
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();
        var agronomist = Guid.NewGuid();

        await SeedUserAsync(farmerA, "+905551112231", UserRole.Farmer);
        await SeedUserAsync(farmerB, "+905551112232", UserRole.Farmer);
        await SeedUserAsync(agronomist, "+905551112233", UserRole.Agronomist);

        var farmAId = await CreateFarmAsUser(farmerA, "Farmer A Özel Tarla");

        // 1. Farmer A lists farms -> Sees Farm A
        using var reqA = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms");
        reqA.Headers.Add("X-User-Id", farmerA.ToString());
        reqA.Headers.Add("X-User-Role", "Farmer");
        var resA = await _client.SendAsync(reqA);
        resA.StatusCode.Should().Be(HttpStatusCode.OK);
        var listA = await resA.Content.ReadFromJsonAsync<List<FarmDto>>(CustomWebApplicationFactory.JsonOptions);
        listA.Should().Contain(f => f.Id == farmAId);

        // 2. Farmer B lists farms -> Does NOT see Farm A
        using var reqB = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms");
        reqB.Headers.Add("X-User-Id", farmerB.ToString());
        reqB.Headers.Add("X-User-Role", "Farmer");
        var resB = await _client.SendAsync(reqB);
        resB.StatusCode.Should().Be(HttpStatusCode.OK);
        var listB = await resB.Content.ReadFromJsonAsync<List<FarmDto>>(CustomWebApplicationFactory.JsonOptions);
        listB.Should().NotContain(f => f.Id == farmAId);

        // 3. Agronomist lists farms -> Sees Farm A
        using var reqAgro = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms");
        reqAgro.Headers.Add("X-User-Id", agronomist.ToString());
        reqAgro.Headers.Add("X-User-Role", "Agronomist");
        var resAgro = await _client.SendAsync(reqAgro);
        resAgro.StatusCode.Should().Be(HttpStatusCode.OK);
        var listAgro = await resAgro.Content.ReadFromJsonAsync<List<FarmDto>>(CustomWebApplicationFactory.JsonOptions);
        listAgro.Should().Contain(f => f.Id == farmAId);
    }

    [Fact]
    public async Task CrossTenant_TarlaDetay_UserBCannotGetOrUpdateOrArchiveUserAFarm()
    {
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();
        var agronomist = Guid.NewGuid();

        await SeedUserAsync(farmerA, "+905551112234", UserRole.Farmer);
        await SeedUserAsync(farmerB, "+905551112235", UserRole.Farmer);
        await SeedUserAsync(agronomist, "+905551112236", UserRole.Agronomist);

        var farmAId = await CreateFarmAsUser(farmerA, "Farmer A Güvenli Tarla");

        // 1. Farmer B attempts to get Farm A -> 404
        using var getB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}");
        getB.Headers.Add("X-User-Id", farmerB.ToString());
        getB.Headers.Add("X-User-Role", "Farmer");
        var resGetB = await _client.SendAsync(getB);
        resGetB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Agronomist gets Farm A -> 200 OK
        using var getAgro = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}");
        getAgro.Headers.Add("X-User-Id", agronomist.ToString());
        getAgro.Headers.Add("X-User-Role", "Agronomist");
        var resGetAgro = await _client.SendAsync(getAgro);
        resGetAgro.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Farmer B attempts to update Farm A -> 404
        using var patchB = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/farms/{farmAId}");
        patchB.Headers.Add("X-User-Id", farmerB.ToString());
        patchB.Headers.Add("X-User-Role", "Farmer");
        patchB.Content = JsonContent.Create(new UpdateFarmRequest(
            UserId: farmerB,
            Name: "Değiştirilmeye Çalışılan İsim",
            Latitude: null, Longitude: null, SizeInHectares: null,
            IrrigationMethod: null, SoilType: null, Note: null
        ), options: CustomWebApplicationFactory.JsonOptions);
        var resPatchB = await _client.SendAsync(patchB);
        resPatchB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 4. Farmer B attempts to archive Farm A -> 404
        using var deleteB = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/farms/{farmAId}");
        deleteB.Headers.Add("X-User-Id", farmerB.ToString());
        deleteB.Headers.Add("X-User-Role", "Farmer");
        var resDeleteB = await _client.SendAsync(deleteB);
        resDeleteB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 5. Verify Farm A remains untouched and accessible by Farmer A
        using var getA = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}");
        getA.Headers.Add("X-User-Id", farmerA.ToString());
        getA.Headers.Add("X-User-Role", "Farmer");
        var resGetA = await _client.SendAsync(getA);
        resGetA.StatusCode.Should().Be(HttpStatusCode.OK);
        var farmDto = await resGetA.Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        farmDto!.Name.Should().Be("Farmer A Güvenli Tarla");
    }

    [Fact]
    public async Task CrossTenant_FaaliyetVeGunluk_UserBCannotReadOrAddOrModifyActivities()
    {
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();
        var agronomist = Guid.NewGuid();

        await SeedUserAsync(farmerA, "+905551112237", UserRole.Farmer);
        await SeedUserAsync(farmerB, "+905551112238", UserRole.Farmer);
        await SeedUserAsync(agronomist, "+905551112239", UserRole.Agronomist);

        var farmAId = await CreateFarmAsUser(farmerA, "Farmer A Faaliyet Tarlası");
        var activityAId = await CreateActivityAsUser(farmAId, farmerA, UserRole.Farmer, "A sulaması");

        // 1. Farmer B lists activities of Farm A -> 404 NotFound
        using var listB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/activities");
        listB.Headers.Add("X-User-Id", farmerB.ToString());
        listB.Headers.Add("X-User-Role", "Farmer");
        var resListB = await _client.SendAsync(listB);
        resListB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Farmer B gets Farm A journal -> 404 NotFound
        using var journalB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/journal");
        journalB.Headers.Add("X-User-Id", farmerB.ToString());
        journalB.Headers.Add("X-User-Role", "Farmer");
        var resJournalB = await _client.SendAsync(journalB);
        resJournalB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Farmer B attempts to add activity to Farm A -> 403 Forbidden
        using var addB = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmAId}/activities");
        addB.Headers.Add("X-User-Id", farmerB.ToString());
        addB.Headers.Add("X-User-Role", "Farmer");
        addB.Content = JsonContent.Create(new CreateActivityApiRequest(
            UserId: farmerB,
            ActivityType: ActivityType.Fertilization,
            Description: "İzinsiz gübreleme",
            OccurredAt: DateTime.UtcNow,
            CropPeriodId: null,
            InputMethod: ActivitySource.Manual,
            DurationMinutes: null, Amount: null, Unit: null,
            PhotoUrl: null, VoiceUrl: null, VoiceTranscript: null,
            PerformedBy: null, Cost: null, ClientOperationId: null
        ), options: CustomWebApplicationFactory.JsonOptions);
        var resAddB = await _client.SendAsync(addB);
        resAddB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Farmer B attempts to get Activity A directly -> 404 NotFound
        using var getActB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/activities/{activityAId}");
        getActB.Headers.Add("X-User-Id", farmerB.ToString());
        getActB.Headers.Add("X-User-Role", "Farmer");
        var resGetActB = await _client.SendAsync(getActB);
        resGetActB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 5. Farmer B attempts to patch Activity A -> 404 NotFound
        using var patchActB = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/activities/{activityAId}");
        patchActB.Headers.Add("X-User-Id", farmerB.ToString());
        patchActB.Headers.Add("X-User-Role", "Farmer");
        patchActB.Content = JsonContent.Create(new UpdateActivityApiRequest(
            UserId: farmerB,
            ActivityType: null,
            Description: "Hacked Description",
            OccurredAt: null, CropPeriodId: null, DurationMinutes: null,
            Amount: null, Unit: null, PhotoUrl: null, VoiceUrl: null,
            VoiceTranscript: null, PerformedBy: null, Cost: null
        ), options: CustomWebApplicationFactory.JsonOptions);
        var resPatchActB = await _client.SendAsync(patchActB);
        resPatchActB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 6. Farmer B attempts to delete Activity A -> 404 NotFound
        using var deleteActB = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/activities/{activityAId}");
        deleteActB.Headers.Add("X-User-Id", farmerB.ToString());
        deleteActB.Headers.Add("X-User-Role", "Farmer");
        var resDeleteActB = await _client.SendAsync(deleteActB);
        resDeleteActB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 7. Agronomist can view activities and journal
        using var listAgro = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/activities");
        listAgro.Headers.Add("X-User-Id", agronomist.ToString());
        listAgro.Headers.Add("X-User-Role", "Agronomist");
        var resListAgro = await _client.SendAsync(listAgro);
        resListAgro.StatusCode.Should().Be(HttpStatusCode.OK);

        using var journalAgro = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/journal");
        journalAgro.Headers.Add("X-User-Id", agronomist.ToString());
        journalAgro.Headers.Add("X-User-Role", "Agronomist");
        var resJournalAgro = await _client.SendAsync(journalAgro);
        resJournalAgro.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CrossTenant_GorevIslemleri_AgronomistCreatesTask_UserBBlockedFromInteracting()
    {
        var farmerA = Guid.NewGuid();
        var farmerB = Guid.NewGuid();
        var agronomist = Guid.NewGuid();

        await SeedUserAsync(farmerA, "+905551112233", UserRole.Farmer);
        await SeedUserAsync(farmerB, "+905551112234", UserRole.Farmer);
        await SeedUserAsync(agronomist, "+905551112235", UserRole.Agronomist);

        var farmAId = await CreateFarmAsUser(farmerA, "Farmer A Görev Tarlası");

        // 1. Agronomist creates expert task on Farm A -> 201 Created
        using var createTask = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmAId}/tasks");
        createTask.Headers.Add("X-User-Id", agronomist.ToString());
        createTask.Headers.Add("X-User-Role", "Agronomist");
        createTask.Content = JsonContent.Create(new CreateExpertTaskApiRequest(
            UserId: agronomist,
            Title: "Mühendis İlaçlama Görevi",
            Description: "Pas hastalığına karşı ilaçlama yapın.",
            Reason: "Nem oranı yüksek.",
            Priority: TaskPriority.High,
            Confidence: TaskConfidence.High,
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            CropPeriodId: null
        ), options: CustomWebApplicationFactory.JsonOptions);

        var resCreateTask = await _client.SendAsync(createTask);
        resCreateTask.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTask = await resCreateTask.Content.ReadFromJsonAsync<TaskDto>(CustomWebApplicationFactory.JsonOptions);
        var taskId = createdTask!.Id;

        // 2. Farmer A lists tasks -> 200 OK (sees task)
        using var listTasksA = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/tasks");
        listTasksA.Headers.Add("X-User-Id", farmerA.ToString());
        listTasksA.Headers.Add("X-User-Role", "Farmer");
        var resListTasksA = await _client.SendAsync(listTasksA);
        resListTasksA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Farmer B lists tasks on Farm A -> 404 NotFound
        using var listTasksB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmAId}/tasks");
        listTasksB.Headers.Add("X-User-Id", farmerB.ToString());
        listTasksB.Headers.Add("X-User-Role", "Farmer");
        var resListTasksB = await _client.SendAsync(listTasksB);
        resListTasksB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 4. Farmer B attempts to get task by ID -> 404 NotFound
        using var getTaskB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tasks/{taskId}");
        getTaskB.Headers.Add("X-User-Id", farmerB.ToString());
        getTaskB.Headers.Add("X-User-Role", "Farmer");
        var resGetTaskB = await _client.SendAsync(getTaskB);
        resGetTaskB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 5. Farmer B attempts to complete task -> 404 NotFound / 403 Forbidden
        using var completeTaskB = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tasks/{taskId}/complete");
        completeTaskB.Headers.Add("X-User-Id", farmerB.ToString());
        completeTaskB.Headers.Add("X-User-Role", "Farmer");
        completeTaskB.Content = JsonContent.Create(new CompleteTaskApiRequest(
            UserId: farmerB,
            Role: UserRole.Farmer,
            Note: "B kullanıcısı tamamlamaya çalıştı",
            PhotoUrl: null
        ), options: CustomWebApplicationFactory.JsonOptions);
        var resCompleteB = await _client.SendAsync(completeTaskB);
        resCompleteB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 6. Farmer A completes the task -> 200 OK
        using var completeTaskA = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tasks/{taskId}/complete");
        completeTaskA.Headers.Add("X-User-Id", farmerA.ToString());
        completeTaskA.Headers.Add("X-User-Role", "Farmer");
        completeTaskA.Content = JsonContent.Create(new CompleteTaskApiRequest(
            UserId: farmerA,
            Role: UserRole.Farmer,
            Note: "A görevi başarıyla tamamladı.",
            PhotoUrl: null
        ), options: CustomWebApplicationFactory.JsonOptions);
        var resCompleteA = await _client.SendAsync(completeTaskA);
        resCompleteA.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await resCompleteA.Content.ReadFromJsonAsync<TaskDto>(CustomWebApplicationFactory.JsonOptions);
        completed!.Status.Should().Be(TaskStatus.Completed);
    }
}

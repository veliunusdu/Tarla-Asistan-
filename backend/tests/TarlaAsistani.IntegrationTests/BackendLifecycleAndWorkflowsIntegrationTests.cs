using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Application.Features.Tasks.Queries;
using TarlaAsistani.Application.Features.Users.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "LifecycleAndWorkflows")]
public class BackendLifecycleAndWorkflowsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public BackendLifecycleAndWorkflowsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedUserWithRoleAsync(Guid userId, string phone, string firebaseUid, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.Include(u => u.RoleAssignments).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                PhoneNumber = phone,
                FirebaseUid = firebaseUid,
                Role = role,
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    UserId = userId,
                    FullName = $"Test User {phone}",
                    Province = "Konya",
                    District = "Selçuklu",
                    TermsAccepted = true,
                    NotificationsEnabled = true
                }
            };
            db.Users.Add(user);
        }

        if (!user.RoleAssignments.Any(ra => ra.Role == role && ra.RevokedAtUtc == null))
        {
            user.RoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Role = role,
                GrantedAtUtc = DateTime.UtcNow,
                GrantReason = "Test seed"
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TwoFarmersAndTwoExperts_AccessMatrixAndClaimConflict_Enforced()
    {
        // Setup: Farmer 1, Farmer 2, Agronomist 1, Agronomist 2, Admin
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmer1Id, "+905550010001", "fb_farmer1", UserRole.Farmer);
        await SeedUserWithRoleAsync(farmer2Id, "+905550010002", "fb_farmer2", UserRole.Farmer);
        await SeedUserWithRoleAsync(expert1Id, "+905550010003", "fb_expert1", UserRole.Agronomist);
        await SeedUserWithRoleAsync(expert2Id, "+905550010004", "fb_expert2", UserRole.Agronomist);
        await SeedUserWithRoleAsync(adminId, "+905550010005", "fb_admin", UserRole.Admin);

        // 1. Create Farm 1 for Farmer 1
        var farm1Res = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmer1Id,
            Name: "Çiftçi 1 Buğday Tarlası",
            Latitude: 38.0,
            Longitude: 32.0,
            SizeInHectares: 10.0,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), CustomWebApplicationFactory.JsonOptions);
        farm1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var farm1Data = await farm1Res.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farm1Id = farm1Data!["id"];

        // 2. Create Farm 2 for Farmer 2
        var farm2Res = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(
            OwnerId: farmer2Id,
            Name: "Çiftçi 2 Mısır Tarlası",
            Latitude: 38.5,
            Longitude: 32.5,
            SizeInHectares: 20.0,
            IrrigationMethod: IrrigationMethod.Sprinkler,
            InitialCropType: CropType.Corn,
            InitialPlantedAt: new DateOnly(2026, 4, 1)
        ), CustomWebApplicationFactory.JsonOptions);
        farm2Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var farm2Data = await farm2Res.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions);
        var farm2Id = farm2Data!["id"];

        // 3. Farmer 1 creates Case 1 on Farm 1
        var case1Res = await _client.PostAsJsonAsync("/api/v1/cases", new CreateCaseApiRequest(
            UserId: farmer1Id,
            FarmId: farm1Id,
            Category: CaseCategory.Disease,
            Title: "Sarı Pas Teşhisi",
            Description: "Yapraklarda pas şeklinde sarı lekeler oluştu",
            MediaIds: null,
            ClientOperationId: null
        ), CustomWebApplicationFactory.JsonOptions);
        case1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var case1 = await case1Res.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        case1.Should().NotBeNull();

        // 4. Expert 1 claims Case 1 -> 200 OK
        var claim1Res = await _client.PostAsJsonAsync($"/api/v1/cases/{case1!.Id}/claim",
            new ClaimCaseApiRequest(expert1Id), CustomWebApplicationFactory.JsonOptions);
        claim1Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Expert 1 claims Case 1 again -> 200 OK idempotent
        var claim1RepeatRes = await _client.PostAsJsonAsync($"/api/v1/cases/{case1.Id}/claim",
            new ClaimCaseApiRequest(expert1Id), CustomWebApplicationFactory.JsonOptions);
        claim1RepeatRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Expert 2 tries to claim Case 1 (already claimed by Expert 1) -> 409 Conflict
        var claim2ConflictRes = await _client.PostAsJsonAsync($"/api/v1/cases/{case1.Id}/claim",
            new ClaimCaseApiRequest(expert2Id), CustomWebApplicationFactory.JsonOptions);
        claim2ConflictRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 7. Expert 2 queries Case 1 directly -> 403 Forbidden (not assigned to this expert)
        var expert2GetRes = await _client.GetAsync($"/api/v1/cases/{case1.Id}?userId={expert2Id}&role={UserRole.Agronomist}");
        expert2GetRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 8. Expert 2 tries to send message to Case 1 -> 403 Forbidden
        var expert2MsgRes = await _client.PostAsJsonAsync($"/api/v1/cases/{case1.Id}/messages",
            new CreateCaseMessageApiRequest(
                UserId: expert2Id,
                Role: UserRole.Agronomist,
                MessageType: CaseMessageType.AdditionalInfoRequest,
                Body: "Yetkisiz mesaj denemesi",
                MediaIds: null,
                ClientOperationId: null
            ), CustomWebApplicationFactory.JsonOptions);
        expert2MsgRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 9. Expert 2 tries to post Expert Response to Case 1 -> 403 Forbidden
        var expert2RespRes = await _client.PostAsJsonAsync($"/api/v1/cases/{case1.Id}/expert-response",
            new CreateExpertResponseApiRequest(
                UserId: expert2Id,
                Role: UserRole.Agronomist,
                Body: "Yetkisiz yanıt denemesi",
                CloseCase: false,
                MediaIds: null,
                ClientOperationId: null
            ), CustomWebApplicationFactory.JsonOptions);
        expert2RespRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 10. Expert 1 creates Expert Task linked to Case 1 -> 201 Created
        var taskRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farm1Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Pas İlacı Uygulaması",
                Description: "Tebuconazole etken maddeli fungisit uygulayın",
                Reason: "Vaka sarı pas teşhisi nedeniyle",
                Priority: TaskPriority.High,
                Confidence: TaskConfidence.High,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: case1.Id
            ), CustomWebApplicationFactory.JsonOptions);
        taskRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTask = await taskRes.Content.ReadFromJsonAsync<TaskDto>(CustomWebApplicationFactory.JsonOptions);
        createdTask.Should().NotBeNull();
        createdTask!.CaseId.Should().Be(case1.Id);

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedTask = await verifyDb.FarmTasks.FindAsync(createdTask.Id);
            persistedTask.Should().NotBeNull();
            persistedTask!.CaseId.Should().Be(case1.Id);
        }

        // 11. Cross-farm check: Expert 1 tries to link Case 1 (Farm 1) to Farm 2 tasks -> 409 Conflict
        var crossFarmTaskRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farm2Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Yanlış Tarla Görevi",
                Description: "Bu görev farm 2'de fakat vaka farm 1'de",
                Reason: "Çapraz tarla testi",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow),
                CropPeriodId: null,
                CaseId: case1.Id
            ), CustomWebApplicationFactory.JsonOptions);
        crossFarmTaskRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 12. Cancellation rule: Farmer 1 cannot cancel Expert 1's task -> 403 Forbidden
        var farmerCancelRes = await _client.PatchAsJsonAsync($"/api/v1/tasks/{createdTask.Id}/status",
            new UpdateTaskStatusApiRequest(
                UserId: farmer1Id,
                Role: UserRole.Farmer,
                Status: TaskStatus.Cancelled,
                NotAppliedReason: null,
                Note: "Çiftçi iptal etmeye çalışıyor",
                PhotoUrl: null
            ), CustomWebApplicationFactory.JsonOptions);
        farmerCancelRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 13. Cancellation rule: Expert 2 cannot cancel Expert 1's task -> 403 Forbidden
        var expert2CancelRes = await _client.PatchAsJsonAsync($"/api/v1/tasks/{createdTask.Id}/status",
            new UpdateTaskStatusApiRequest(
                UserId: expert2Id,
                Role: UserRole.Agronomist,
                Status: TaskStatus.Cancelled,
                NotAppliedReason: null,
                Note: "Başka uzman iptal etmeye çalışıyor",
                PhotoUrl: null
            ), CustomWebApplicationFactory.JsonOptions);
        expert2CancelRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 14. Cancellation rule: Expert 1 cancels their own task -> 200 OK
        var expert1CancelRes = await _client.PatchAsJsonAsync($"/api/v1/tasks/{createdTask.Id}/status",
            new UpdateTaskStatusApiRequest(
                UserId: expert1Id,
                Role: UserRole.Agronomist,
                Status: TaskStatus.Cancelled,
                NotAppliedReason: null,
                Note: "Hava durumu nedeniyle iptal edildi",
                PhotoUrl: null
            ), CustomWebApplicationFactory.JsonOptions);
        expert1CancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 15. Admin reassigns Case 1 from Expert 1 to Expert 2 -> 200 OK
        var reassignRes = await _client.PostAsJsonAsync($"/api/v1/admin/cases/{case1.Id}/reassign?userId={adminId}&role={UserRole.Admin}",
            new AdminReassignCaseRequest(expert2Id, "İş yükü dengelemesi"),
            CustomWebApplicationFactory.JsonOptions);
        reassignRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 16. Now Expert 2 CAN query and message Case 1
        var expert2GetAfterRes = await _client.GetAsync($"/api/v1/cases/{case1.Id}?userId={expert2Id}&role={UserRole.Agronomist}");
        expert2GetAfterRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var case1After = await expert2GetAfterRes.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        case1After!.AssignedExpertId.Should().Be(expert2Id);
    }

    [Fact]
    public async Task AccountDeletionLifecycle_LocksFirebaseLoginAndTokenRefresh()
    {
        var userId = Guid.NewGuid();
        const string phone = "+905550020001";
        const string firebaseUid = "fb_deletion_test";
        const string firebaseToken = "token_deletion_test";

        await SeedUserWithRoleAsync(userId, phone, firebaseUid, UserRole.Farmer);
        _factory.MockFirebaseAuthService
            .Setup(f => f.VerifyIdTokenAsync(firebaseToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(firebaseUid, phone, null, "Silme Testi"));

        // 1. First deletion request -> 202 Accepted
        var delRes1 = await _client.PostAsJsonAsync("/api/v1/users/me/deletion-request",
            new AccountDeletionApiRequest(userId, "HESABIMI SIL"),
            CustomWebApplicationFactory.JsonOptions);
        delRes1.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // 2. Duplicate deletion request -> 202 Accepted idempotent (must NOT produce 500)
        var delRes2 = await _client.PostAsJsonAsync("/api/v1/users/me/deletion-request",
            new AccountDeletionApiRequest(userId, "HESABIMI SIL"),
            CustomWebApplicationFactory.JsonOptions);
        delRes2.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // 3. Query deletion status -> 200 OK with Pending status
        var statusRes = await _client.GetAsync($"/api/v1/users/me/deletion-request?userId={userId}");
        statusRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobStatus = await statusRes.Content.ReadFromJsonAsync<Dictionary<string, object>>(CustomWebApplicationFactory.JsonOptions);
        jobStatus.Should().NotBeNull();
        jobStatus!["status"].ToString().Should().Be("PENDING");

        // 4. Firebase login while DeletionPending -> 423 Locked (MUST NOT implicitly reactivate account)
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        loginRes.StatusCode.Should().Be(HttpStatusCode.Locked);
        var loginErr = await loginRes.Content.ReadFromJsonAsync<Dictionary<string, object>>(CustomWebApplicationFactory.JsonOptions);
        loginErr!["error"].ToString().Should().Be("ACCOUNT_DELETION_PENDING");

        // 5. Verify database still has DeletionPending status
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userInDb = await db.Users.FirstAsync(u => u.Id == userId);
        userInDb.AccountStatus.Should().Be(AccountStatus.DeletionPending);
    }

    [Fact]
    public async Task AgronomistApplicationFlow_SubmitApproveAndRoleGrant()
    {
        var farmerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550030001", "fb_applicant", UserRole.Farmer);
        await SeedUserWithRoleAsync(adminId, "+905550030002", "fb_admin2", UserRole.Admin);

        // 1. Farmer submits application -> 201 Created
        var submitRes = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmerId}",
            new SubmitAgronomistApplicationRequest("Ziraat mühendisiyim, mezuniyet belgemi ekte sunuyorum."),
            CustomWebApplicationFactory.JsonOptions);
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var appDto = await submitRes.Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);
        appDto.Should().NotBeNull();
        appDto!.Status.Should().Be(AgronomistApplicationStatus.Pending);

        // 2. Farmer resubmits -> 200 OK idempotent with same application
        var resubmitRes = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmerId}",
            new SubmitAgronomistApplicationRequest("Yeniden başvuru"),
            CustomWebApplicationFactory.JsonOptions);
        resubmitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resubmittedDto = await resubmitRes.Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);
        resubmittedDto!.Id.Should().Be(appDto.Id);

        // 3. Farmer checks application status -> 200 OK
        var meRes = await _client.GetAsync($"/api/v1/agronomist-applications/me?userId={farmerId}");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Admin lists applications -> 200 OK
        var adminListRes = await _client.GetAsync($"/api/v1/admin/agronomist-applications?userId={adminId}&role={UserRole.Admin}&status={AgronomistApplicationStatus.Pending}");
        adminListRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Admin approves application -> 200 OK
        var approveRes = await _client.PostAsJsonAsync(
            $"/api/v1/admin/agronomist-applications/{appDto.Id}/approve?userId={adminId}&role={UserRole.Admin}",
            new ReviewAgronomistApplicationRequest("Belgeler incelendi, onaylandı."),
            CustomWebApplicationFactory.JsonOptions);
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Verify user has active Agronomist role assignment now
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasAgronomistRole = await db.UserRoleAssignments
            .AnyAsync(ra => ra.UserId == farmerId && ra.Role == UserRole.Agronomist && ra.RevokedAtUtc == null);
        hasAgronomistRole.Should().BeTrue();
    }

    [Fact]
    public async Task ExpertTasksInbox_ReturnsScopedAndSortedTasks()
    {
        var expertId = Guid.NewGuid();
        var otherExpertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        await SeedUserWithRoleAsync(expertId, "+905550040001", "fb_expert_inbox", UserRole.Agronomist);
        await SeedUserWithRoleAsync(otherExpertId, "+905550040002", "fb_other_expert", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var farm = new Farm
            {
                Id = farmId,
                OwnerId = expertId,
                Name = "Inbox Test Çiftliği",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 5.0
            };
            db.Farms.Add(farm);

            // Task 1 by expertId, due in 2 days
            db.FarmTasks.Add(new FarmTask
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                CreatedById = expertId,
                Title = "İlaçlama 1",
                Description = "Açıklama 1",
                Reason = "Neden 1",
                DedupeKey = "key-1",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                Status = TaskStatus.New,
                Source = TaskSource.Expert
            });

            // Task 2 by expertId, due in 1 day
            db.FarmTasks.Add(new FarmTask
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                CreatedById = expertId,
                Title = "İlaçlama 2",
                Description = "Açıklama 2",
                Reason = "Neden 2",
                DedupeKey = "key-2",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                Status = TaskStatus.New,
                Source = TaskSource.Expert
            });

            // Task 3 by otherExpertId
            db.FarmTasks.Add(new FarmTask
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                CreatedById = otherExpertId,
                Title = "Diğer Uzman Görevi",
                Description = "Açıklama 3",
                Reason = "Neden 3",
                DedupeKey = "key-3",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                Status = TaskStatus.New,
                Source = TaskSource.Expert
            });

            await db.SaveChangesAsync();
        }

        // Act: Query expert tasks for expertId
        var res = await _client.GetAsync($"/api/v1/expert/tasks?userId={expertId}&role={UserRole.Agronomist}&scope=mine");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await res.Content.ReadFromJsonAsync<ExpertTaskListDto>(CustomWebApplicationFactory.JsonOptions);
        data.Should().NotBeNull();
        data!.Items.Should().HaveCount(2);
        data.Items.Should().NotContain(t => t.Title == "Diğer Uzman Görevi");
        // Sorted by DueDate ASC: Task 2 (due in 1 day) must come before Task 1 (due in 2 days)
        data.Items[0].Title.Should().Be("İlaçlama 2");
        data.Items[1].Title.Should().Be("İlaçlama 1");
    }

    [Fact]
    public async Task CaseClaim_ConcurrentTwoExperts_ExactlyOneWinsAndOneGets409()
    {
        var farmerId = Guid.NewGuid();
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550050001", "fb_cc_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expert1Id, "+905550050002", "fb_cc_expert1", UserRole.Agronomist);
        await SeedUserWithRoleAsync(expert2Id, "+905550050003", "fb_cc_expert2", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Claim Concurrency Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.SupportCases.Add(new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = null,
                Category = CaseCategory.Pest,
                Priority = CasePriority.High,
                Status = CaseStatus.Open,
                Title = "Böcek İstilası",
                Description = "Tarlada yoğun böcek zararı gözlemlendi",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var claimTask1 = _client.PostAsJsonAsync(
            $"/api/v1/cases/{caseId}/claim",
            new ClaimCaseApiRequest(expert1Id),
            CustomWebApplicationFactory.JsonOptions);

        var claimTask2 = _client.PostAsJsonAsync(
            $"/api/v1/cases/{caseId}/claim",
            new ClaimCaseApiRequest(expert2Id),
            CustomWebApplicationFactory.JsonOptions);

        var responses = await Task.WhenAll(claimTask1, claimTask2);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sc = await db.SupportCases.FirstAsync(c => c.Id == caseId);
            sc.AssignedExpertId.Should().NotBeNull();
            new[] { expert1Id, expert2Id }.Should().Contain(sc.AssignedExpertId!.Value);
        }
    }

    [Fact]
    public async Task AccountDeletion_ConcurrentRequests_BothGet202WithSameJobId()
    {
        var userId = Guid.NewGuid();
        await SeedUserWithRoleAsync(userId, "+905550060001", "fb_cc_del", UserRole.Farmer);

        var task1 = _client.PostAsJsonAsync("/api/v1/users/me/deletion-request",
            new AccountDeletionApiRequest(userId, "HESABIMI SIL"),
            CustomWebApplicationFactory.JsonOptions);

        var task2 = _client.PostAsJsonAsync("/api/v1/users/me/deletion-request",
            new AccountDeletionApiRequest(userId, "HESABIMI SIL"),
            CustomWebApplicationFactory.JsonOptions);

        var responses = await Task.WhenAll(task1, task2);

        responses[0].StatusCode.Should().Be(HttpStatusCode.Accepted);
        responses[1].StatusCode.Should().Be(HttpStatusCode.Accepted);

        var res1 = await responses[0].Content.ReadFromJsonAsync<AccountDeletionResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var res2 = await responses[1].Content.ReadFromJsonAsync<AccountDeletionResponseDto>(CustomWebApplicationFactory.JsonOptions);

        res1.Should().NotBeNull();
        res2.Should().NotBeNull();
        res1!.RequestId.Should().Be(res2!.RequestId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jobs = await db.AccountDeletionJobs.Where(j => j.UserId == userId).ToListAsync();
            jobs.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task AgronomistApplication_ConcurrentSubmissions_BothGetSameApplicationId()
    {
        var farmerId = Guid.NewGuid();
        await SeedUserWithRoleAsync(farmerId, "+905550070001", "fb_cc_app", UserRole.Farmer);

        var task1 = _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmerId}",
            new SubmitAgronomistApplicationRequest("Başvuru 1"),
            CustomWebApplicationFactory.JsonOptions);

        var task2 = _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmerId}",
            new SubmitAgronomistApplicationRequest("Başvuru 2"),
            CustomWebApplicationFactory.JsonOptions);

        var responses = await Task.WhenAll(task1, task2);

        responses.All(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.OK).Should().BeTrue();

        var app1 = await responses[0].Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);
        var app2 = await responses[1].Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);

        app1.Should().NotBeNull();
        app2.Should().NotBeNull();
        app1!.Id.Should().Be(app2!.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var apps = await db.AgronomistApplications.Where(a => a.UserId == farmerId && a.Status == AgronomistApplicationStatus.Pending).ToListAsync();
            apps.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task AgronomistApplication_ApprovedCannotBeRejected_RejectedCannotBeApproved()
    {
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmer1Id, "+905550080001", "fb_app_trans1", UserRole.Farmer);
        await SeedUserWithRoleAsync(farmer2Id, "+905550080002", "fb_app_trans2", UserRole.Farmer);
        await SeedUserWithRoleAsync(adminId, "+905550080003", "fb_admin_trans", UserRole.Admin);

        var sub1 = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmer1Id}",
            new SubmitAgronomistApplicationRequest("Onaylanacak başvuru"),
            CustomWebApplicationFactory.JsonOptions);
        var app1 = await sub1.Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);

        var app1Approve = await _client.PostAsJsonAsync(
            $"/api/v1/admin/agronomist-applications/{app1!.Id}/approve?userId={adminId}&role={UserRole.Admin}",
            new ReviewAgronomistApplicationRequest("Onaylandı"),
            CustomWebApplicationFactory.JsonOptions);
        app1Approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var app1Reject = await _client.PostAsJsonAsync(
            $"/api/v1/admin/agronomist-applications/{app1.Id}/reject?userId={adminId}&role={UserRole.Admin}",
            new ReviewAgronomistApplicationRequest("Artık reddediyorum"),
            CustomWebApplicationFactory.JsonOptions);
        app1Reject.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var sub2 = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={farmer2Id}",
            new SubmitAgronomistApplicationRequest("Reddedilecek başvuru"),
            CustomWebApplicationFactory.JsonOptions);
        var app2 = await sub2.Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);

        var app2Reject = await _client.PostAsJsonAsync(
            $"/api/v1/admin/agronomist-applications/{app2!.Id}/reject?userId={adminId}&role={UserRole.Admin}",
            new ReviewAgronomistApplicationRequest("Yetersiz belge"),
            CustomWebApplicationFactory.JsonOptions);
        app2Reject.StatusCode.Should().Be(HttpStatusCode.OK);

        var app2Approve = await _client.PostAsJsonAsync(
            $"/api/v1/admin/agronomist-applications/{app2.Id}/approve?userId={adminId}&role={UserRole.Admin}",
            new ReviewAgronomistApplicationRequest("Vazgeçtim onayla"),
            CustomWebApplicationFactory.JsonOptions);
        app2Approve.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task FirebaseLogin_SamePhoneDifferentUid_ThrowsUnauthorized()
    {
        var userId = Guid.NewGuid();
        const string phone = "+905550090001";
        const string originalUid = "fb_orig_uid_123";
        const string attackerUid = "fb_attacker_uid_999";
        const string token = "token_attacker";

        await SeedUserWithRoleAsync(userId, phone, originalUid, UserRole.Farmer);

        _factory.MockFirebaseAuthService
            .Setup(f => f.VerifyIdTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(attackerUid, phone, null, "Attacker User"));

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(token, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);

        loginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PoolCases_Unassigned_JsonDoesNotContainFarmOrUserOrFreeText()
    {
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550100001", "fb_pool_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expertId, "+905550100002", "fb_pool_expert", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Çok Gizli Çiftlik Adı",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.SupportCases.Add(new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = null,
                Category = CaseCategory.Disease,
                Priority = CasePriority.High,
                Status = CaseStatus.Open,
                Title = "Çok Gizli Başlık",
                Description = "Çok Gizli Açıklama metni",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var poolRes = await _client.GetAsync($"/api/v1/cases?userId={expertId}&role={UserRole.Agronomist}&scope=pool");
        poolRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var poolJson = await poolRes.Content.ReadAsStringAsync();

        poolJson.Should().NotContain("Çok Gizli Çiftlik Adı");
        poolJson.Should().NotContain("Çok Gizli Başlık");
        poolJson.Should().NotContain("Çok Gizli Açıklama");
        poolJson.Should().NotContain("farm_id");
        poolJson.Should().NotContain("farm_name");
        poolJson.Should().NotContain("created_by_id");

        var detailRes = await _client.GetAsync($"/api/v1/cases/{caseId}?userId={expertId}&role={UserRole.Agronomist}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await detailRes.Content.ReadAsStringAsync();

        detailJson.Should().NotContain("Çok Gizli Çiftlik Adı");
        detailJson.Should().NotContain("Çok Gizli Başlık");
        detailJson.Should().NotContain("Çok Gizli Açıklama");
        detailJson.Should().NotContain("farm_id");
        detailJson.Should().NotContain("farm_name");
        detailJson.Should().NotContain("created_by_id");
    }

    [Fact]
    public async Task TaskIsolation_AgronomistCannotReadAnotherExpertTask_Returns404()
    {
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await SeedUserWithRoleAsync(expert1Id, "+905550110001", "fb_iso_exp1", UserRole.Agronomist);
        await SeedUserWithRoleAsync(expert2Id, "+905550110002", "fb_iso_exp2", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = expert1Id,
                Name = "Task Isolation Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.FarmTasks.Add(new FarmTask
            {
                Id = taskId,
                FarmId = farmId,
                CreatedById = expert1Id,
                Title = "Expert 1 Task",
                Description = "Desc",
                Reason = "Reason",
                DedupeKey = "iso-task-1",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = TaskStatus.New,
                Source = TaskSource.Expert
            });

            await db.SaveChangesAsync();
        }

        var getRes = await _client.GetAsync($"/api/v1/tasks/{taskId}?userId={expert2Id}&role={UserRole.Agronomist}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TaskIsolation_AgronomistCreateTaskWithAnotherExpertCase_ThrowsForbidden()
    {
        var farmerId = Guid.NewGuid();
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550120001", "fb_task_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expert1Id, "+905550120002", "fb_task_expert1", UserRole.Agronomist);
        await SeedUserWithRoleAsync(expert2Id, "+905550120003", "fb_task_expert2", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Case Task Isolation Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.SupportCases.Add(new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = expert1Id,
                Category = CaseCategory.Pest,
                Priority = CasePriority.Medium,
                Status = CaseStatus.InReview,
                Title = "Vaka 1",
                Description = "Açıklama",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var taskRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farmId}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert2Id,
                Title: "Başka Uzmanın Vakasına Görev",
                Description: "Açıklama",
                Reason: "Neden",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow),
                CropPeriodId: null,
                CaseId: caseId
            ), CustomWebApplicationFactory.JsonOptions);

        taskRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TaskIsolation_AgronomistDailyTasks_DoesNotExposeFarmerAlerts()
    {
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550130001", "fb_dt_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expertId, "+905550130002", "fb_dt_expert", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Daily Tasks Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.FarmTasks.Add(new FarmTask
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                CreatedById = farmerId,
                Title = "Don Uyarısı",
                Description = "Don riski",
                Reason = "Hava tahmini",
                DedupeKey = "weather-alert-1",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = TaskStatus.New,
                Priority = TaskPriority.Critical,
                Source = TaskSource.Weather
            });

            await db.SaveChangesAsync();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var res = await _client.GetAsync($"/api/v1/farms/{farmId}/tasks?userId={expertId}&role={UserRole.Agronomist}&date={today}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await res.Content.ReadFromJsonAsync<DailyTaskListDto>(CustomWebApplicationFactory.JsonOptions);
        data.Should().NotBeNull();
        data!.Items.Should().BeEmpty();
        data.CriticalWeatherAlerts.Should().BeEmpty();
        data.Overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task TaskAuthorization_TwoFarmersAndTwoExperts_FullMatrix_IsolationEnforced()
    {
        var farmer1Id = Guid.NewGuid();
        var farmer2Id = Guid.NewGuid();
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var farm1Id = Guid.NewGuid();
        var farm2Id = Guid.NewGuid();
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmer1Id, "+905550210001", "fb_matrix_farmer1", UserRole.Farmer);
        await SeedUserWithRoleAsync(farmer2Id, "+905550210002", "fb_matrix_farmer2", UserRole.Farmer);
        await SeedUserWithRoleAsync(expert1Id, "+905550210003", "fb_matrix_expert1", UserRole.Agronomist);
        await SeedUserWithRoleAsync(expert2Id, "+905550210004", "fb_matrix_expert2", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.AddRange(
                new Farm { Id = farm1Id, OwnerId = farmer1Id, Name = "Tarla 1", Latitude = 38.0, Longitude = 32.0, SizeInHectares = 5.0 },
                new Farm { Id = farm2Id, OwnerId = farmer2Id, Name = "Tarla 2", Latitude = 38.5, Longitude = 32.5, SizeInHectares = 8.0 }
            );

            db.SupportCases.AddRange(
                new SupportCase
                {
                    Id = case1Id,
                    FarmId = farm1Id,
                    CreatedById = farmer1Id,
                    AssignedExpertId = expert1Id,
                    Status = CaseStatus.InReview,
                    Category = CaseCategory.Disease,
                    Priority = CasePriority.High,
                    Title = "Vaka 1",
                    Description = "Vaka 1 Açıklama"
                },
                new SupportCase
                {
                    Id = case2Id,
                    FarmId = farm2Id,
                    CreatedById = farmer2Id,
                    AssignedExpertId = expert2Id,
                    Status = CaseStatus.Closed,
                    Category = CaseCategory.Pest,
                    Priority = CasePriority.Medium,
                    Title = "Vaka 2 Kapalı",
                    Description = "Vaka 2 Açıklama",
                    ClosedAtUtc = DateTime.UtcNow
                }
            );

            await db.SaveChangesAsync();
        }

        // 1. Expert1 creates task on Farm1 with Case1 -> 201 Created
        var createExp1Res = await _client.PostAsJsonAsync($"/api/v1/farms/{farm1Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Uzman 1 Görevi",
                Description: "Detaylı açıklama",
                Reason: "Önemli gerekçe",
                Priority: TaskPriority.High,
                Confidence: TaskConfidence.High,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: case1Id
            ), CustomWebApplicationFactory.JsonOptions);
        createExp1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTask = await createExp1Res.Content.ReadFromJsonAsync<TaskDto>(CustomWebApplicationFactory.JsonOptions);
        createdTask.Should().NotBeNull();
        var taskId = createdTask!.Id;

        // 2. Expert1 tries to create task on Farm1 with Case2 (which belongs to Farm2) -> Conflict/BadRequest
        var mismatchCaseRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farm1Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Uyuşmayan Vaka Görevi",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.High,
                Confidence: TaskConfidence.High,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: case2Id
            ), CustomWebApplicationFactory.JsonOptions);
        mismatchCaseRes.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);

        // 3. Expert1 creates unlinked task on Farm1 (allowed because Expert1 has active Case1 on Farm1) -> 201
        var unlinkedExp1Res = await _client.PostAsJsonAsync($"/api/v1/farms/{farm1Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Uzman 1 Bağlantısız Görev",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                CropPeriodId: null,
                CaseId: null
            ), CustomWebApplicationFactory.JsonOptions);
        unlinkedExp1Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Expert2 tries to create task on Farm1 without case ID -> 403 Forbidden (no active assigned case on Farm1)
        var exp2NoCaseFarm1Res = await _client.PostAsJsonAsync($"/api/v1/farms/{farm1Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert2Id,
                Title: "Yetkisiz Uzman 2 Görevi",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: null
            ), CustomWebApplicationFactory.JsonOptions);
        exp2NoCaseFarm1Res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Expert1 tries to create task on Farm2 without case ID -> 403 Forbidden (no assigned case on Farm2)
        var exp1NoCaseFarm2Res = await _client.PostAsJsonAsync($"/api/v1/farms/{farm2Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert1Id,
                Title: "Uzman 1 Farm2 Görevi",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: null
            ), CustomWebApplicationFactory.JsonOptions);
        exp1NoCaseFarm2Res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. Expert2 tries to create task on Farm2 with Case2 (Case2 is Closed) -> 409 Conflict
        var exp2ClosedCaseRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farm2Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: expert2Id,
                Title: "Kapalı Vaka Görevi",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: case2Id
            ), CustomWebApplicationFactory.JsonOptions);
        exp2ClosedCaseRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 7. Farmer1 tries to create task on Farm2 (owned by Farmer2) -> 403 Forbidden
        var farmer1WrongFarmRes = await _client.PostAsJsonAsync($"/api/v1/farms/{farm2Id}/tasks",
            new CreateExpertTaskApiRequest(
                UserId: farmer1Id,
                Title: "Çiftçi 1 Yetkisiz Tarla Görevi",
                Description: "Açıklama",
                Reason: "Gerekçe",
                Priority: TaskPriority.Medium,
                Confidence: TaskConfidence.Medium,
                DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                CropPeriodId: null,
                CaseId: null
            ), CustomWebApplicationFactory.JsonOptions);
        farmer1WrongFarmRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 8. Isolation: Expert2 tries to GET taskId -> 404 Not Found
        var getByExp2Res = await _client.GetAsync($"/api/v1/tasks/{taskId}?userId={expert2Id}&role={UserRole.Agronomist}");
        getByExp2Res.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 9. Isolation: Expert2 tries to cancel taskId -> 404 Not Found
        var cancelByExp2Res = await _client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new UpdateTaskStatusApiRequest(
                UserId: expert2Id,
                Role: UserRole.Agronomist,
                Status: TaskStatus.Cancelled,
                NotAppliedReason: null,
                Note: "Yetkisiz iptal denemesi",
                PhotoUrl: null
            ), CustomWebApplicationFactory.JsonOptions);
        cancelByExp2Res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 10. Isolation: Expert1 cancels taskId -> 200 OK
        var cancelByExp1Res = await _client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new UpdateTaskStatusApiRequest(
                UserId: expert1Id,
                Role: UserRole.Agronomist,
                Status: TaskStatus.Cancelled,
                NotAppliedReason: null,
                Note: "Uzman 1 görevi iptal etti",
                PhotoUrl: null
            ), CustomWebApplicationFactory.JsonOptions);
        cancelByExp1Res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AgronomistApplication_LengthBoundaries_Enforced()
    {
        var applicantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        await SeedUserWithRoleAsync(applicantId, "+905550220001", "fb_app_boundary_user", UserRole.Farmer);
        await SeedUserWithRoleAsync(adminId, "+905550220002", "fb_app_boundary_admin", UserRole.Admin);

        // 1. Note with 2001 chars -> 400 BadRequest
        var note2001 = new string('A', 2001);
        var res2001 = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={applicantId}",
            new SubmitAgronomistApplicationRequest(note2001),
            CustomWebApplicationFactory.JsonOptions);
        res2001.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Note with 2000 chars -> 201 Created
        var note2000 = new string('B', 2000);
        var res2000 = await _client.PostAsJsonAsync($"/api/v1/agronomist-applications?userId={applicantId}",
            new SubmitAgronomistApplicationRequest(note2000),
            CustomWebApplicationFactory.JsonOptions);
        res2000.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdApp = await res2000.Content.ReadFromJsonAsync<AgronomistApplicationDto>(CustomWebApplicationFactory.JsonOptions);
        createdApp.Should().NotBeNull();
        var appId = createdApp!.Id;

        // 3. Reason with 1001 chars on Reject -> 400 BadRequest
        var reason1001 = new string('R', 1001);
        var rej1001 = await _client.PostAsJsonAsync($"/api/v1/admin/agronomist-applications/{appId}/reject?userId={adminId}",
            new ReviewAgronomistApplicationRequest(reason1001),
            CustomWebApplicationFactory.JsonOptions);
        rej1001.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4. Reason with 1000 chars on Reject -> 200 OK
        var reason1000 = new string('R', 1000);
        var rej1000 = await _client.PostAsJsonAsync($"/api/v1/admin/agronomist-applications/{appId}/reject?userId={adminId}",
            new ReviewAgronomistApplicationRequest(reason1000),
            CustomWebApplicationFactory.JsonOptions);
        rej1000.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CaseDetailContract_RedactedForUnassignedExpert_AndFullAfterClaim()
    {
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550330001", "fb_redact_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expertId, "+905550330002", "fb_redact_expert", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Hassas Gizli Tarla Adı",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.SupportCases.Add(new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = null,
                Category = CaseCategory.Pest,
                Priority = CasePriority.High,
                Status = CaseStatus.Open,
                Title = "Hassas Gizli Başlık",
                Description = "Hassas Gizli Açıklama",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // 1. Unassigned expert queries case detail -> 200 OK with redacted contract
        var unassignedRes = await _client.GetAsync($"/api/v1/cases/{caseId}?userId={expertId}&role={UserRole.Agronomist}");
        unassignedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await unassignedRes.Content.ReadAsStringAsync();
        rawJson.Should().Contain("\"is_redacted\":true");
        rawJson.Should().NotContain("Hassas Gizli");
        rawJson.Should().NotContain("\"farm_name\"");
        rawJson.Should().NotContain("\"farm_id\"");
        rawJson.Should().NotContain("\"title\"");

        var redactedDto = await unassignedRes.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        redactedDto.Should().NotBeNull();
        redactedDto!.IsRedacted.Should().BeTrue();
        redactedDto.FarmId.Should().BeNull();
        redactedDto.FarmName.Should().BeNull();
        redactedDto.Title.Should().BeNull();
        redactedDto.Description.Should().BeNull();
        redactedDto.Media.Should().BeNull();
        redactedDto.Messages.Should().BeNull();
        redactedDto.SystemSummary.Should().Be("Pest - High");

        // 2. Expert claims the case -> 200 OK
        var claimRes = await _client.PostAsync($"/api/v1/cases/{caseId}/claim?userId={expertId}&role={UserRole.Agronomist}", null);
        claimRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Expert re-queries case detail -> 200 OK with full detail contract
        var fullRes = await _client.GetAsync($"/api/v1/cases/{caseId}?userId={expertId}&role={UserRole.Agronomist}");
        fullRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fullJson = await fullRes.Content.ReadAsStringAsync();
        fullJson.Should().Contain("\"is_redacted\":false");
        fullJson.Should().Contain("Hassas Gizli Tarla Adı");
        fullJson.Should().Contain("Hassas Gizli Başlık");

        var fullDto = await fullRes.Content.ReadFromJsonAsync<CaseDetailDto>(CustomWebApplicationFactory.JsonOptions);
        fullDto.Should().NotBeNull();
        fullDto!.IsRedacted.Should().BeFalse();
        fullDto.FarmId.Should().Be(farmId);
        fullDto.FarmName.Should().Be("Hassas Gizli Tarla Adı");
        fullDto.Title.Should().Be("Hassas Gizli Başlık");
        fullDto.Description.Should().Be("Hassas Gizli Açıklama");
    }

    [Fact]
    public async Task CaseClaim_ClosedCase_AlwaysReturns409CaseClosed_EvenIfAssignedToCaller()
    {
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserWithRoleAsync(farmerId, "+905550340001", "fb_closed_claim_farmer", UserRole.Farmer);
        await SeedUserWithRoleAsync(expertId, "+905550340002", "fb_closed_claim_expert", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Closed Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 10.0
            });

            db.SupportCases.Add(new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = expertId, // Already assigned to caller!
                Category = CaseCategory.Irrigation,
                Priority = CasePriority.Low,
                Status = CaseStatus.Closed, // But closed!
                ClosedAtUtc = DateTime.UtcNow,
                Title = "Kapalı Vaka",
                Description = "Açıklama",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Act: Caller claims their own already-assigned but closed case
        var claimRes = await _client.PostAsync($"/api/v1/cases/{caseId}/claim?userId={expertId}&role={UserRole.Agronomist}", null);

        // Assert: 409 Conflict with CASE_CLOSED
        claimRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await claimRes.Content.ReadFromJsonAsync<Dictionary<string, string>>(CustomWebApplicationFactory.JsonOptions);
        body.Should().ContainKey("error");
        body!["error"].Should().Be("CASE_CLOSED");
    }
}

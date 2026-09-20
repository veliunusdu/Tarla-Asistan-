using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Application.Features.Users.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "PostgreSQL")]
public class PostgresConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public PostgresConcurrencyIntegrationTests()
    {
        _factory = new PostgreSqlWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedUserAsync(Guid userId, string phone, string firebaseUid, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
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
                District = "Meram",
                TermsAccepted = true,
                NotificationsEnabled = true
            }
        };
        db.Users.Add(user);

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = role,
            GrantedAtUtc = DateTime.UtcNow,
            GrantReason = "Postgres test seed"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CaseClaim_ConcurrentClaimsFromTwoExperts_ExactlyOneSucceeds_AndOneAssignmentEventInDb()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var expert1Id = Guid.NewGuid();
        var expert2Id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserAsync(farmerId, "+905557770001", "fb_farmer_pg", UserRole.Farmer);
        await SeedUserAsync(expert1Id, "+905557770002", "fb_expert1_pg", UserRole.Agronomist);
        await SeedUserAsync(expert2Id, "+905557770003", "fb_expert2_pg", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var farm = new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Test Farm PG",
                Latitude = 38.5,
                Longitude = 32.5,
                SizeInHectares = 15.0
            };
            var supportCase = new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                Category = CaseCategory.Pest,
                Priority = CasePriority.High,
                Status = CaseStatus.Open,
                Title = "Sarı Pas Tehlikesi",
                Description = "Tarlada sarı pas belirtileri görülüyor.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Farms.Add(farm);
            db.SupportCases.Add(supportCase);
            await db.SaveChangesAsync();
        }

        // Act: Concurrently claim from both experts
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        request1.Headers.Add("X-User-Id", expert1Id.ToString());
        request1.Headers.Add("X-Active-Role", "Agronomist");

        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        request2.Headers.Add("X-User-Id", expert2Id.ToString());
        request2.Headers.Add("X-Active-Role", "Agronomist");

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        var task1 = client1.SendAsync(request1);
        var task2 = client2.SendAsync(request2);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: One 200 OK, one 409 Conflict
        var successResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.OK);
        var conflictResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.Conflict);

        successResponse.Should().NotBeNull("one expert must successfully claim the case");
        conflictResponse.Should().NotBeNull("the other expert must receive a 409 Conflict");

        var conflictBody = await conflictResponse!.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        conflictBody.Should().ContainKey("error");
        conflictBody!["error"].Should().Be("CASE_ALREADY_ASSIGNED");

        // Verify in PostgreSQL database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedCase = await db.SupportCases.FindAsync(caseId);
            persistedCase.Should().NotBeNull();
            persistedCase!.AssignedExpertId.Should().NotBeNull();
            persistedCase.Status.Should().Be(CaseStatus.InReview);

            var events = await db.CaseAssignmentEvents
                .Where(e => e.CaseId == caseId)
                .ToListAsync();

            events.Should().HaveCount(1, "exactly 1 CaseAssignmentEvent must be recorded in DB");
            events[0].NewExpertId.Should().Be(persistedCase.AssignedExpertId);
            events[0].ChangeType.Should().Be(CaseAssignmentChangeType.Claim);
        }
    }

    [Fact]
    public async Task AgronomistApplication_ConcurrentPendingApplications_EnforcedByPartialUniqueIndex()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "+905557770010", "fb_app_pg", UserRole.Farmer);

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agronomist-applications")
        {
            Content = JsonContent.Create(new SubmitAgronomistApplicationRequest(Note: "Ziraat mühendisiyim - başvuru 1"))
        };
        req1.Headers.Add("X-User-Id", userId.ToString());
        req1.Headers.Add("X-Active-Role", "Farmer");

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agronomist-applications")
        {
            Content = JsonContent.Create(new SubmitAgronomistApplicationRequest(Note: "Ziraat mühendisiyim - başvuru 2"))
        };
        req2.Headers.Add("X-User-Id", userId.ToString());
        req2.Headers.Add("X-Active-Role", "Farmer");

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Act
        var responses = await Task.WhenAll(client1.SendAsync(req1), client2.SendAsync(req2));

        // Assert: Both return success (either 201 Created or 200 OK)
        responses.Should().AllSatisfy(r => r.IsSuccessStatusCode.Should().BeTrue());

        // In PostgreSQL database, exactly 1 Pending record exists
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendingCount = await db.AgronomistApplications
            .CountAsync(a => a.UserId == userId && a.Status == AgronomistApplicationStatus.Pending);

        pendingCount.Should().Be(1, "partial unique index uq_agronomist_applications_one_pending_per_user allows exactly one Pending application");
    }

    [Fact]
    public async Task AccountDeletion_ConcurrentDeletionRequests_EnforcedByUniqueIndex()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "+905557770020", "fb_del_pg", UserRole.Farmer);

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/deletion-request")
        {
            Content = JsonContent.Create(new AccountDeletionApiRequest(userId, "HESABIMI SIL"))
        };
        req1.Headers.Add("X-User-Id", userId.ToString());
        req1.Headers.Add("X-Active-Role", "Farmer");

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/deletion-request")
        {
            Content = JsonContent.Create(new AccountDeletionApiRequest(userId, "HESABIMI SIL"))
        };
        req2.Headers.Add("X-User-Id", userId.ToString());
        req2.Headers.Add("X-Active-Role", "Farmer");

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Act
        var responses = await Task.WhenAll(client1.SendAsync(req1), client2.SendAsync(req2));

        // Assert
        responses.Should().AllSatisfy(r => r.IsSuccessStatusCode.Should().BeTrue());

        // In PostgreSQL database, exactly 1 active job exists
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeJobCount = await db.AccountDeletionJobs
            .CountAsync(j => j.UserId == userId &&
                           (j.Status == AccountDeletionStatus.Pending ||
                            j.Status == AccountDeletionStatus.Processing ||
                            j.Status == AccountDeletionStatus.RetryRequired));

        activeJobCount.Should().Be(1, "unique index on UserId allows exactly one active deletion job per user");
    }

    [Fact]
    public async Task CaseClaim_ReClaimBySameExpert_IsIdempotent200_WithoutDuplicateEvents()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserAsync(farmerId, "+905557770031", "fb_farmer_reclaim", UserRole.Farmer);
        await SeedUserAsync(expertId, "+905557770032", "fb_expert_reclaim", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var farm = new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Reclaim Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 5.0
            };
            var supportCase = new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                Category = CaseCategory.Irrigation,
                Priority = CasePriority.Medium,
                Status = CaseStatus.Open,
                Title = "Sulama Düzensizliği",
                Description = "Açıklama"
            };
            db.Farms.Add(farm);
            db.SupportCases.Add(supportCase);
            await db.SaveChangesAsync();
        }

        // Act 1: First claim
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        req1.Headers.Add("X-User-Id", expertId.ToString());
        req1.Headers.Add("X-Active-Role", "Agronomist");
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Re-claim by same expert
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        req2.Headers.Add("X-User-Id", expertId.ToString());
        req2.Headers.Add("X-Active-Role", "Agronomist");
        var res2 = await _client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Exactly 1 event in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await db.CaseAssignmentEvents
                .Where(e => e.CaseId == caseId)
                .ToListAsync();

            events.Should().HaveCount(1, "re-claiming same case must be idempotent and not create duplicate events");
        }
    }

    [Fact]
    public async Task CaseClaim_ClaimOnClosedCase_Returns409CaseClosed()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserAsync(farmerId, "+905557770041", "fb_farmer_closed", UserRole.Farmer);
        await SeedUserAsync(expertId, "+905557770042", "fb_expert_closed", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var farm = new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Closed Case Farm",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 5.0
            };
            var supportCase = new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                Category = CaseCategory.Disease,
                Priority = CasePriority.Low,
                Status = CaseStatus.Closed,
                ClosedAtUtc = DateTime.UtcNow,
                Title = "Kapatılmış Vaka",
                Description = "Açıklama"
            };
            db.Farms.Add(farm);
            db.SupportCases.Add(supportCase);
            await db.SaveChangesAsync();
        }

        // Act
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        req.Headers.Add("X-User-Id", expertId.ToString());
        req.Headers.Add("X-Active-Role", "Agronomist");
        var res = await _client.SendAsync(req);

        // Assert: 409 Conflict with CASE_CLOSED
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.Should().ContainKey("error");
        body!["error"].Should().Be("CASE_CLOSED");
    }

    [Fact]
    public async Task CaseClaim_ClaimOnClosedCase_AssignedToSameExpert_Returns409CaseClosed()
    {
        // Arrange
        var farmerId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await SeedUserAsync(farmerId, "+905557770051", "fb_farmer_closed_same", UserRole.Farmer);
        await SeedUserAsync(expertId, "+905557770052", "fb_expert_closed_same", UserRole.Agronomist);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var farm = new Farm
            {
                Id = farmId,
                OwnerId = farmerId,
                Name = "Closed Case Farm Same Expert",
                Latitude = 38.0,
                Longitude = 32.0,
                SizeInHectares = 5.0
            };
            var supportCase = new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = farmerId,
                AssignedExpertId = expertId, // Case already assigned to this expert
                Category = CaseCategory.Disease,
                Priority = CasePriority.Low,
                Status = CaseStatus.Closed,  // But the case is CLOSED
                ClosedAtUtc = DateTime.UtcNow,
                Title = "Kapatılmış Vaka (Aynı Uzman)",
                Description = "Açıklama"
            };
            db.Farms.Add(farm);
            db.SupportCases.Add(supportCase);
            await db.SaveChangesAsync();
        }

        // Act: Same expert tries to claim the closed case
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/claim");
        req.Headers.Add("X-User-Id", expertId.ToString());
        req.Headers.Add("X-Active-Role", "Agronomist");
        var res = await _client.SendAsync(req);

        // Assert: 409 Conflict with CASE_CLOSED (must NOT return 200 OK via idempotent bypass)
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.Should().ContainKey("error");
        body!["error"].Should().Be("CASE_CLOSED");
    }
}


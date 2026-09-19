using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.Persistence;

namespace TarlaAsistani.IntegrationTests;

[Trait("Category", "Security")]
public class MultiRoleAuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MultiRoleAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedUserWithRolesAsync(Guid userId, string phone, string firebaseUid, params UserRole[] roles)
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
                Role = UserRole.Farmer,
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    FullName = "Test MultiRole User",
                    Province = "Ankara",
                    District = "Polatlı",
                    TermsAccepted = true
                }
            };
            db.Users.Add(user);
        }

        foreach (var role in roles)
        {
            if (!user.RoleAssignments.Any(r => r.Role == role && r.RevokedAtUtc == null))
            {
                user.RoleAssignments.Add(new UserRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Role = role,
                    GrantedAtUtc = DateTime.UtcNow,
                    GrantReason = "Test setup seed"
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private void SetupFirebaseMock(string token, string uid, string phone, string email, string name)
    {
        _factory.MockFirebaseAuthService
            .Setup(f => f.VerifyIdTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirebaseTokenInfo(uid, phone, email, name));
    }

    [Fact]
    public async Task MultiRoleUser_CanLoginAsFarmerAndAgronomist_WithSameFirebaseAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string phone = "+905559991122";
        const string firebaseUid = "multi_role_firebase_uid";
        const string firebaseToken = "multi_role_token";

        await SeedUserWithRolesAsync(userId, phone, firebaseUid, UserRole.Farmer, UserRole.Agronomist);
        SetupFirebaseMock(firebaseToken, firebaseUid, phone, "multirole@test.com", "Çiftçi Uzman");

        // Act 1: Login as FARMER
        var farmerLoginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);

        // Assert 1
        farmerLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var farmerResult = await farmerLoginResponse.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        farmerResult.Should().NotBeNull();
        farmerResult!.User.ActiveRole.Should().Be(UserRole.Farmer);
        farmerResult.User.Roles.Should().Contain(new[] { UserRole.Farmer, UserRole.Agronomist });

        // Act 2: Login as AGRONOMIST
        var agroLoginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Agronomist),
            CustomWebApplicationFactory.JsonOptions);

        // Assert 2
        agroLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var agroResult = await agroLoginResponse.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        agroResult.Should().NotBeNull();
        agroResult!.User.ActiveRole.Should().Be(UserRole.Agronomist);
        agroResult.User.Roles.Should().Contain(new[] { UserRole.Farmer, UserRole.Agronomist });

        // Act 3: Login requesting unassigned role (ADMIN) -> 403 Forbidden
        var adminLoginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Admin),
            CustomWebApplicationFactory.JsonOptions);

        // Assert 3
        adminLoginResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActiveRole_ContextIsolation_EnforcesEndpointAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string phone = "+905559993344";
        const string firebaseUid = "context_iso_firebase_uid";
        const string firebaseToken = "context_iso_token";

        await SeedUserWithRolesAsync(userId, phone, firebaseUid, UserRole.Farmer, UserRole.Agronomist);
        SetupFirebaseMock(firebaseToken, firebaseUid, phone, "context@test.com", "İkili Rol");

        // Obtain Farmer token
        var farmerRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        var farmerTokens = await farmerRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var farmerJwt = farmerTokens!.AccessToken;

        // Obtain Agronomist token
        var agroRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Agronomist),
            CustomWebApplicationFactory.JsonOptions);
        var agroTokens = await agroRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var agroJwt = agroTokens!.AccessToken;

        // Farmer token accessing farmer-area -> 200 OK
        using var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/farmer-area");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Farmer token accessing agronomist-area -> 403 Forbidden
        using var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var res2 = await _client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Agronomist token accessing agronomist-area -> 200 OK
        using var req3 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area");
        req3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agroJwt);
        var res3 = await _client.SendAsync(req3);
        res3.StatusCode.Should().Be(HttpStatusCode.OK);

        // Agronomist token accessing farmer-area -> 403 Forbidden
        using var req4 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/farmer-area");
        req4.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agroJwt);
        var res4 = await _client.SendAsync(req4);
        res4.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MeEndpoint_ReturnsActiveRoleAndAllGrantedRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string phone = "+905559995566";
        const string firebaseUid = "me_test_firebase_uid";
        const string firebaseToken = "me_test_token";

        await SeedUserWithRolesAsync(userId, phone, firebaseUid, UserRole.Farmer, UserRole.Agronomist);
        SetupFirebaseMock(firebaseToken, firebaseUid, phone, "me@test.com", "Me Test Kullanıcı");

        // Login as Farmer
        var farmerRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        var farmerTokens = await farmerRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);

        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerTokens!.AccessToken);
        var meRes = await _client.SendAsync(meReq);

        meRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var meUser = await meRes.Content.ReadFromJsonAsync<UserDto>(CustomWebApplicationFactory.JsonOptions);
        meUser.Should().NotBeNull();
        meUser!.ActiveRole.Should().Be(UserRole.Farmer);
        meUser.Roles.Should().Contain(new[] { UserRole.Farmer, UserRole.Agronomist });
    }

    [Fact]
    public async Task RoleEscalation_AttemptViaQueryParam_IsBlockedByJwtClaim()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string phone = "+905559997788";
        const string firebaseUid = "escalation_firebase_uid";
        const string firebaseToken = "escalation_token";

        await SeedUserWithRolesAsync(userId, phone, firebaseUid, UserRole.Farmer);
        SetupFirebaseMock(firebaseToken, firebaseUid, phone, "escalate@test.com", "Escalation Test");

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        var tokens = await loginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);

        // Attempt to access agronomist-area adding ?role=Agronomist
        using var escalationReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area?role=Agronomist");
        escalationReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var escalationRes = await _client.SendAsync(escalationReq);

        // Must be rejected with 403 Forbidden
        escalationRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminContext_AccessControl_AndRoleManagementLifecycle()
    {
        // 1. Arrange Admin user and Regular Farmer user
        var adminUserId = Guid.NewGuid();
        const string adminPhone = "+905559998800";
        const string adminUid = "admin_user_uid";
        const string adminToken = "admin_firebase_token";

        var farmerUserId = Guid.NewGuid();
        const string farmerPhone = "+905559998811";
        const string farmerUid = "target_farmer_uid";
        const string farmerToken = "target_farmer_token";

        await SeedUserWithRolesAsync(adminUserId, adminPhone, adminUid, UserRole.Farmer, UserRole.Admin);
        await SeedUserWithRolesAsync(farmerUserId, farmerPhone, farmerUid, UserRole.Farmer);

        SetupFirebaseMock(adminToken, adminUid, adminPhone, "admin@test.com", "Sistem Yöneticisi");
        SetupFirebaseMock(farmerToken, farmerUid, farmerPhone, "farmer@test.com", "Hedef Çiftçi");

        // 2. Non-admin attempting to access admin endpoints -> 403 Forbidden
        var farmerLoginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(farmerToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        var farmerTokens = await farmerLoginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);

        using var nonAdminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        nonAdminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerTokens!.AccessToken);
        var nonAdminRes = await _client.SendAsync(nonAdminReq);
        nonAdminRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Admin logs in with active_role = ADMIN -> 200 OK
        var adminLoginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(adminToken, UserRole.Admin),
            CustomWebApplicationFactory.JsonOptions);
        adminLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminTokens = await adminLoginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var adminJwt = adminTokens!.AccessToken;

        // 4. Admin lists users -> 200 OK
        using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        var listRes = await _client.SendAsync(listReq);
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Admin attempts to assign ADMIN role via API -> 400 Bad Request
        using var assignAdminReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{farmerUserId}/role-assignments");
        assignAdminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        assignAdminReq.Content = JsonContent.Create(new AdminAssignRoleRequest(UserRole.Admin, "Deneme"));
        var assignAdminRes = await _client.SendAsync(assignAdminReq);
        assignAdminRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 6. Admin assigns AGRONOMIST role to farmer -> 201 Created
        using var assignAgroReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{farmerUserId}/role-assignments");
        assignAgroReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        assignAgroReq.Content = JsonContent.Create(new AdminAssignRoleRequest(UserRole.Agronomist, "Ziraat Odası Onayı"));
        var assignAgroRes = await _client.SendAsync(assignAgroReq);
        assignAgroRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 7. Admin assigns AGRONOMIST again -> 200 OK (idempotent)
        using var reassignAgroReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{farmerUserId}/role-assignments");
        reassignAgroReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        reassignAgroReq.Content = JsonContent.Create(new AdminAssignRoleRequest(UserRole.Agronomist, "Tekrar atama"));
        var reassignAgroRes = await _client.SendAsync(reassignAgroReq);
        reassignAgroRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. Farmer now logs in as AGRONOMIST -> 200 OK
        var agroLoginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(farmerToken, UserRole.Agronomist),
            CustomWebApplicationFactory.JsonOptions);
        agroLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var farmerAgroTokens = await agroLoginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        farmerAgroTokens.Should().NotBeNull();
        farmerAgroTokens!.User.ActiveRole.Should().Be(UserRole.Agronomist);
        var agroRefreshToken = farmerAgroTokens.RefreshToken;

        // 9. Admin revokes AGRONOMIST role -> 200 OK
        using var revokeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{farmerUserId}/role-assignments/AGRONOMIST/revoke");
        revokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        revokeReq.Content = JsonContent.Create(new AdminRevokeRoleRequest("Yetki geri çekildi"));
        var revokeRes = await _client.SendAsync(revokeReq);
        revokeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Admin checks role history -> 200 OK with grant & revoke records
        using var historyReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/users/{farmerUserId}/role-history");
        historyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        var historyRes = await _client.SendAsync(historyReq);
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyRes.Content.ReadFromJsonAsync<List<AdminRoleHistoryItemDto>>(CustomWebApplicationFactory.JsonOptions);
        history.Should().NotBeNull();
        history!.Should().Contain(h => h.Role == UserRole.Agronomist && h.RevokedAtUtc != null && h.RevokeReason == "Yetki geri çekildi");

        // 11. Farmer tries to use the previous AGRONOMIST refresh token -> 401 Unauthorized
        var refreshRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenApiRequest(agroRefreshToken),
            CustomWebApplicationFactory.JsonOptions);
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LegacyAgronomistUser_WhenAgronomistAssignmentRevoked_RejectsLoginRefreshAndEndpointAccess()
    {
        // Arrange: User has legacy users.Role = Agronomist in table, but active assignment was revoked!
        var userId = Guid.NewGuid();
        const string phone = "+905559990011";
        const string firebaseUid = "legacy_agro_revoked_uid";
        const string firebaseToken = "legacy_agro_revoked_token";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = new User
            {
                Id = userId,
                PhoneNumber = phone,
                FirebaseUid = firebaseUid,
                Role = UserRole.Agronomist, // Legacy role column still says Agronomist!
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    FullName = "Eski Ziraatçi",
                    Province = "İzmir",
                    District = "Ödemiş",
                    TermsAccepted = true
                }
            };

            // Active Farmer assignment
            user.RoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Role = UserRole.Farmer,
                GrantedAtUtc = DateTime.UtcNow.AddMonths(-3),
                GrantReason = "Baseline farmer role"
            });

            // Revoked Agronomist assignment
            user.RoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Role = UserRole.Agronomist,
                GrantedAtUtc = DateTime.UtcNow.AddMonths(-3),
                GrantReason = "Historical agronomist role",
                RevokedAtUtc = DateTime.UtcNow.AddDays(-1),
                RevokeReason = "Yetki yöneticisi tarafından iptal edildi"
            });

            // Pre-existing Agronomist refresh token in DB
            var rawRefreshToken = "legacy_agro_refresh_token_string";
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken))).ToLowerInvariant();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                ActiveRole = UserRole.Agronomist,
                FamilyId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(15),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            });

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Mint a pre-existing access token (bearing claim active_role=Agronomist)
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var mintedAgroJwt = jwtService.GenerateAccessToken(user, UserRole.Agronomist);

            SetupFirebaseMock(firebaseToken, firebaseUid, phone, "legacy_agro@test.com", "Eski Ziraatçi");

            // 1. Firebase login with active_role=AGRONOMIST must return 403 Forbidden
            var loginRes = await _client.PostAsJsonAsync(
                "/api/v1/auth/firebase",
                new FirebaseLoginApiRequest(firebaseToken, UserRole.Agronomist),
                CustomWebApplicationFactory.JsonOptions);
            loginRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // 2. Refresh existing Agronomist token must return 401 Unauthorized
            var refreshRes = await _client.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new RefreshTokenApiRequest(rawRefreshToken),
                CustomWebApplicationFactory.JsonOptions);
            refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 3. Using pre-existing Agronomist access token on agronomist endpoint must return 403 Forbidden
            using var agroAreaReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/agronomist-area");
            agroAreaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mintedAgroJwt);
            var agroAreaRes = await _client.SendAsync(agroAreaReq);
            agroAreaRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task AgronomistEndpoints_RejectFarmerContextToken_With403Forbidden()
    {
        // Arrange
        var farmerUserId = Guid.NewGuid();
        const string farmerPhone = "+905559990022";
        const string farmerUid = "pure_farmer_uid";
        const string farmerToken = "pure_farmer_token";

        await SeedUserWithRolesAsync(farmerUserId, farmerPhone, farmerUid, UserRole.Farmer);
        SetupFirebaseMock(farmerToken, farmerUid, farmerPhone, "purefarmer@test.com", "Sadece Çiftçi");

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(farmerToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        var tokens = await loginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var farmerJwt = tokens!.AccessToken;

        var dummyFarmId = Guid.NewGuid();
        var dummyCaseId = Guid.NewGuid();

        // 1. Expert task creation -> 403 Forbidden
        using var taskReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{dummyFarmId}/tasks");
        taskReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        taskReq.Content = JsonContent.Create(new { title = "Deneme", description = "Açıklama", reason = "Toprak durumu" });
        var taskRes = await _client.SendAsync(taskReq);
        taskRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Case status update -> 403 Forbidden
        using var statusReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{dummyCaseId}/status");
        statusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        statusReq.Content = JsonContent.Create(new { status = CaseStatus.Closed });
        var statusRes = await _client.SendAsync(statusReq);
        statusRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Expert response -> 403 Forbidden
        using var expertRespReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{dummyCaseId}/expert-response");
        expertRespReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        expertRespReq.Content = JsonContent.Create(new { body = "Uzman tavsiyesi" });
        var expertRespRes = await _client.SendAsync(expertRespReq);
        expertRespRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Pilot feedback list -> 403 Forbidden
        using var pilotFeedbacksReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pilot/feedback");
        pilotFeedbacksReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var pilotFeedbacksRes = await _client.SendAsync(pilotFeedbacksReq);
        pilotFeedbacksRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Pilot metrics -> 403 Forbidden
        using var pilotMetricsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pilot/metrics");
        pilotMetricsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var pilotMetricsRes = await _client.SendAsync(pilotMetricsReq);
        pilotMetricsRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRoleManagement_ValidatesReasonRequirement()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        const string adminPhone = "+905559990033";
        const string adminUid = "admin_val_uid";
        const string adminToken = "admin_val_token";

        var targetUserId = Guid.NewGuid();
        const string targetPhone = "+905559990044";
        const string targetUid = "target_val_uid";

        await SeedUserWithRolesAsync(adminUserId, adminPhone, adminUid, UserRole.Farmer, UserRole.Admin);
        await SeedUserWithRolesAsync(targetUserId, targetPhone, targetUid, UserRole.Farmer);

        SetupFirebaseMock(adminToken, adminUid, adminPhone, "admin_val@test.com", "Admin Doğrulama");

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(adminToken, UserRole.Admin),
            CustomWebApplicationFactory.JsonOptions);
        var tokens = await loginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var adminJwt = tokens!.AccessToken;

        // 1. Assign role with missing / empty reason -> 400 Bad Request
        using var emptyReasonAssignReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{targetUserId}/role-assignments");
        emptyReasonAssignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        emptyReasonAssignReq.Content = JsonContent.Create(new AdminAssignRoleRequest(UserRole.Agronomist, "  "));
        var emptyReasonAssignRes = await _client.SendAsync(emptyReasonAssignReq);
        emptyReasonAssignRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Revoke role with missing / whitespace reason -> 400 Bad Request
        using var emptyReasonRevokeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{targetUserId}/role-assignments/AGRONOMIST/revoke");
        emptyReasonRevokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
        emptyReasonRevokeReq.Content = JsonContent.Create(new AdminRevokeRoleRequest(""));
        var emptyReasonRevokeRes = await _client.SendAsync(emptyReasonRevokeReq);
        emptyReasonRevokeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RevokedAgronomist_StaleToken_IsRejectedOnRoleSensitiveEndpoints_WhileFarmerSessionRemainsActive()
    {
        var userId = Guid.NewGuid();
        var activeAgroId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        User legacyUser;
        User activeAgroUser;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            legacyUser = new User
            {
                Id = userId,
                PhoneNumber = "+905559998877",
                FirebaseUid = "legacy_agro_revoked_uid",
                Role = UserRole.Agronomist, // legacy column value to verify no fallback to users.Role
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    FullName = "Eski Ziraatci Ciftci",
                    Province = "Konya",
                    District = "Meram",
                    TermsAccepted = true
                },
                RoleAssignments = new List<UserRoleAssignment>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Role = UserRole.Farmer,
                        GrantedAtUtc = DateTime.UtcNow.AddMonths(-6),
                        GrantReason = "Initial farmer role"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Role = UserRole.Agronomist,
                        GrantedAtUtc = DateTime.UtcNow.AddMonths(-3),
                        GrantReason = "Promoted to agronomist",
                        RevokedAtUtc = DateTime.UtcNow.AddDays(-1),
                        RevokeReason = "Yetkisi alindi"
                    }
                }
            };

            activeAgroUser = new User
            {
                Id = activeAgroId,
                PhoneNumber = "+905559998899",
                FirebaseUid = "active_agro_uid",
                Role = UserRole.Agronomist,
                AccountStatus = AccountStatus.Active,
                Profile = new Profile
                {
                    FullName = "Aktif Ziraatci",
                    Province = "Ankara",
                    District = "Cankaya",
                    TermsAccepted = true
                },
                RoleAssignments = new List<UserRoleAssignment>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        UserId = activeAgroId,
                        Role = UserRole.Agronomist,
                        GrantedAtUtc = DateTime.UtcNow.AddMonths(-2),
                        GrantReason = "Active agronomist role"
                    }
                }
            };

            var farm = new Farm
            {
                Id = farmId,
                OwnerId = userId,
                Name = "Ciftcinin Tarlasi",
                SizeInHectares = 10,
                IrrigationMethod = IrrigationMethod.Drip,
                CreatedAtUtc = DateTime.UtcNow
            };

            var supportCase = new SupportCase
            {
                Id = caseId,
                FarmId = farmId,
                CreatedById = userId,
                Category = CaseCategory.Disease,
                Title = "Stale Token Test Vaka",
                Description = "Aciklama",
                Status = CaseStatus.Open,
                CreatedAtUtc = DateTime.UtcNow
            };

            var mediaAsset = new MediaAsset
            {
                Id = mediaId,
                OwnerId = userId,
                Kind = MediaKind.Image,
                OriginalName = "yaprak.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                StorageKey = "yaprak.jpg",
                ChecksumSha256 = "dummy_checksum",
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Users.AddRange(legacyUser, activeAgroUser);
            db.Farms.Add(farm);
            db.SupportCases.Add(supportCase);
            db.MediaAssets.Add(mediaAsset);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var staleAgroJwt = jwtService.GenerateAccessToken(legacyUser, UserRole.Agronomist);
            var activeFarmerJwt = jwtService.GenerateAccessToken(legacyUser, UserRole.Farmer);
            var activeAgroJwt = jwtService.GenerateAccessToken(activeAgroUser, UserRole.Agronomist);

            // 1. Stale agronomist token -> 403 on role-sensitive read & write endpoints
            var endpointsToTest403 = new (HttpMethod Method, string Url, HttpContent? Content)[]
            {
                (HttpMethod.Get, "/api/v1/cases", null),
                (HttpMethod.Get, $"/api/v1/cases/{caseId}", null),
                (HttpMethod.Get, $"/api/v1/media/{mediaId}/content", null),
                (HttpMethod.Get, "/api/v1/farms", null),
                (HttpMethod.Get, $"/api/v1/farms/{farmId}/tasks/all", null),
                (HttpMethod.Patch, $"/api/v1/farms/{farmId}", JsonContent.Create(new { name = "Guncel Tarla" })),
                (HttpMethod.Delete, $"/api/v1/farms/{farmId}", null),
                (HttpMethod.Post, $"/api/v1/cases/{caseId}/expert-response", JsonContent.Create(new { body = "Tavsiye" })),
                (HttpMethod.Patch, $"/api/v1/cases/{caseId}/status", JsonContent.Create(new { status = CaseStatus.Closed })),
                (HttpMethod.Post, $"/api/v1/farms/{farmId}/tasks", JsonContent.Create(new { title = "Gorev", description = "Aciklama", reason = "Sebep" })),
                (HttpMethod.Get, "/api/v1/pilot/feedback", null),
                (HttpMethod.Get, "/api/v1/pilot/metrics", null),
                (HttpMethod.Post, "/api/v1/pilot/feedback", JsonContent.Create(new { feedbackType = FeedbackType.Suggestion, comment = "Geri bildirim", rating = 5 })),
                (HttpMethod.Post, "/api/v1/farms", JsonContent.Create(new CreateFarmRequest(userId, "Stale Token Farm", 39.0, 32.0, 5.0, IrrigationMethod.Drip, CropType.Wheat, new DateOnly(2026, 3, 1)))),
                (HttpMethod.Get, "/api/v1/notifications", null),
                (HttpMethod.Get, "/api/v1/ai/advisories/", null),
                (HttpMethod.Get, "/api/v1/auth/me", null)
            };

            foreach (var (method, url, content) in endpointsToTest403)
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staleAgroJwt);
                if (content != null) request.Content = content;

                var response = await _client.SendAsync(request);
                response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"endpoint {method} {url} should return 403 Forbidden with stale revoked token");
            }

            // 2. Active AGRONOMIST token with active assignment continues to work for expert flows
            using (var activeAgroCasesReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases"))
            {
                activeAgroCasesReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeAgroJwt);
                var activeAgroCasesRes = await _client.SendAsync(activeAgroCasesReq);
                activeAgroCasesRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using (var activeAgroFarmsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms"))
            {
                activeAgroFarmsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeAgroJwt);
                var activeAgroFarmsRes = await _client.SendAsync(activeAgroFarmsReq);
                activeAgroFarmsRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using (var activeAgroTaskReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/farms/{farmId}/tasks"))
            {
                activeAgroTaskReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeAgroJwt);
                activeAgroTaskReq.Content = JsonContent.Create(new { title = "Aktif Uzman Gorevi", description = "Aciklama", reason = "Toprak analizi" });
                var activeAgroTaskRes = await _client.SendAsync(activeAgroTaskReq);
                activeAgroTaskRes.StatusCode.Should().Be(HttpStatusCode.Created);
            }

            // 3. Active FARMER token for the SAME user continues to work for own farm and data (revocation does not break farmer session)
            using (var farmerFarmsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/farms"))
            {
                farmerFarmsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeFarmerJwt);
                var farmerFarmsRes = await _client.SendAsync(farmerFarmsReq);
                farmerFarmsRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using (var farmerFarmDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/farms/{farmId}"))
            {
                farmerFarmDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeFarmerJwt);
                var farmerFarmDetailRes = await _client.SendAsync(farmerFarmDetailReq);
                farmerFarmDetailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using (var farmerCaseDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId}"))
            {
                farmerCaseDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeFarmerJwt);
                var farmerCaseDetailRes = await _client.SendAsync(farmerCaseDetailReq);
                farmerCaseDetailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using (var farmerAreaReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/farmer-area"))
            {
                farmerAreaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeFarmerJwt);
                var farmerAreaRes = await _client.SendAsync(farmerAreaReq);
                farmerAreaRes.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }
    }

    [Fact]
    public async Task TestHeaderAuth_UnassignedRole_Returns403_AndDoesNotWriteAssignmentToDatabase()
    {
        var unassignedUserId = Guid.NewGuid();

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        req.Headers.Add("X-User-Id", unassignedUserId.ToString());
        req.Headers.Add("X-User-Role", "Admin");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasAssignment = await db.UserRoleAssignments.AnyAsync(ura => ura.UserId == unassignedUserId);
        hasAssignment.Should().BeFalse("ActiveRoleAuthorizationHandler must remain read-only and never write role assignments to the database");
    }

    [Fact]
    public async Task ActiveFarmer_CanUseFarmCreation_Notifications_AndAdvisoryFlows()
    {
        var farmerUserId = Guid.NewGuid();
        const string phone = "+905559994455";
        const string firebaseUid = "active_farmer_flows_uid";
        const string firebaseToken = "active_farmer_flows_token";

        await SeedUserWithRolesAsync(farmerUserId, phone, firebaseUid, UserRole.Farmer);
        SetupFirebaseMock(firebaseToken, firebaseUid, phone, "farmer_flows@test.com", "Aktif Ciftci Akis");

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/firebase",
            new FirebaseLoginApiRequest(firebaseToken, UserRole.Farmer),
            CustomWebApplicationFactory.JsonOptions);
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginRes.Content.ReadFromJsonAsync<TokenResponseDto>(CustomWebApplicationFactory.JsonOptions);
        var farmerJwt = tokens!.AccessToken;

        // 1. POST /api/v1/farms -> 201 Created
        using var createFarmReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/farms");
        createFarmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        createFarmReq.Content = JsonContent.Create(new CreateFarmRequest(
            OwnerId: farmerUserId,
            Name: "Yetkili Ciftci Tarlasi",
            Latitude: 39.0,
            Longitude: 32.0,
            SizeInHectares: 10,
            IrrigationMethod: IrrigationMethod.Drip,
            InitialCropType: CropType.Wheat,
            InitialPlantedAt: new DateOnly(2026, 3, 1)
        ), options: CustomWebApplicationFactory.JsonOptions);

        var createFarmRes = await _client.SendAsync(createFarmReq);
        createFarmRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. GET /api/v1/notifications -> 200 OK
        using var notifReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        notifReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var notifRes = await _client.SendAsync(notifReq);
        notifRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. GET /api/v1/ai/advisories/ -> 200 OK
        using var advisoryReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ai/advisories/");
        advisoryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var advisoryRes = await _client.SendAsync(advisoryReq);
        advisoryRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. GET /api/v1/auth/me -> 200 OK
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", farmerJwt);
        var meRes = await _client.SendAsync(meReq);
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

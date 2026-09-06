using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TarlaAsistani.API.Endpoints;
using TarlaAsistani.Application.Features.Farms.DTOs;

namespace TarlaAsistani.IntegrationTests;

public sealed class FinancialEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FinancialEndpointsIntegrationTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task OwnerCanCreateAndReadFinancialRecords_ServerCalculatesSaleTotal()
    {
        var (owner, farmId, periodId) = await CreateFarmWithPeriod();
        var expense = new CreateExpenseRequest(Domain.Enums.ExpenseCategory.Fertilizer, 125m,
            DateTime.UtcNow, "Gübre");
        var expenseRequest = NewRequest(HttpMethod.Post, $"/api/v1/farms/{farmId}/production-periods/{periodId}/expenses", owner, expense);
        var expenseResponse = await _client.SendAsync(expenseRequest);
        expenseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sale = new CreateSaleRequest(100m, "kg", 8.75m, new DateOnly(2026, 8, 20), "Pazar");
        var saleResponse = await _client.SendAsync(NewRequest(HttpMethod.Post,
            $"/api/v1/farms/{farmId}/production-periods/{periodId}/sales", owner, sale));
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var saleJson = await saleResponse.Content.ReadAsStringAsync();
        saleJson.Should().Contain("\"total_amount\":875");

        var summary = await _client.SendAsync(NewRequest(HttpMethod.Get,
            $"/api/v1/farms/{farmId}/production-periods/{periodId}/financial-summary", owner));
        summary.StatusCode.Should().Be(HttpStatusCode.OK);
        (await summary.Content.ReadAsStringAsync()).Should().Contain("\"registered_difference\":750");
    }

    [Fact]
    public async Task CrossTenantAndFarmPeriodMismatchAreNotFound()
    {
        var (ownerA, farmA, periodA) = await CreateFarmWithPeriod();
        var (ownerB, farmB, periodB) = await CreateFarmWithPeriod();

        var crossTenant = await _client.SendAsync(NewRequest(HttpMethod.Post,
            $"/api/v1/farms/{farmB}/production-periods/{periodB}/expenses", ownerA,
            new CreateExpenseRequest(Domain.Enums.ExpenseCategory.Other, 10m, DateTime.UtcNow)));
        crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var mismatch = await _client.SendAsync(NewRequest(HttpMethod.Post,
            $"/api/v1/farms/{farmA}/production-periods/{periodB}/sales", ownerA,
            new CreateSaleRequest(1m, "kg", 2m, new DateOnly(2026, 8, 20))));
        mismatch.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var hidden = await _client.SendAsync(NewRequest(HttpMethod.Get,
            $"/api/v1/farms/{farmA}/production-periods/{periodA}/financial-summary", ownerB));
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DuplicateClientOperationDoesNotCreateSecondExpenseOrSale()
    {
        var (owner, farmId, periodId) = await CreateFarmWithPeriod();
        var operationId = Guid.NewGuid();
        var expense = new CreateExpenseRequest(Domain.Enums.ExpenseCategory.Other, 42m, DateTime.UtcNow,
            ClientOperationId: operationId);
        var path = $"/api/v1/farms/{farmId}/production-periods/{periodId}/expenses";
        (await _client.SendAsync(NewRequest(HttpMethod.Post, path, owner, expense))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.SendAsync(NewRequest(HttpMethod.Post, path, owner, expense))).StatusCode.Should().Be(HttpStatusCode.Created);
        var list = await _client.SendAsync(NewRequest(HttpMethod.Get, path, owner));
        var items = await list.Content.ReadFromJsonAsync<List<object>>(CustomWebApplicationFactory.JsonOptions);
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task InvalidFinancialPayloadReturnsFourHundredSeriesResponse()
    {
        var (owner, farmId, periodId) = await CreateFarmWithPeriod();
        var response = await _client.SendAsync(NewRequest(HttpMethod.Post,
            $"/api/v1/farms/{farmId}/production-periods/{periodId}/expenses", owner,
            new { category = "not_a_category", amount = -1, occurred_at_utc = "not-a-date" }));
        ((int)response.StatusCode).Should().BeInRange(400, 499);
    }

    [Fact]
    public async Task ActivityLinkedExpenseCannotBeMutatedThroughFinanceEndpoint()
    {
        var (owner, farmId, periodId) = await CreateFarmWithPeriod();
        var activityResponse = await _client.SendAsync(NewRequest(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities", owner,
            new { activity_name = "Sulama", activity_type = "IRRIGATION", cost = 100f, crop_period_id = periodId }));
        activityResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var list = await _client.SendAsync(NewRequest(HttpMethod.Get,
            $"/api/v1/farms/{farmId}/production-periods/{periodId}/expenses", owner));
        var raw = await list.Content.ReadAsStringAsync();
        raw.Should().Contain("activity_id");
        using var json = System.Text.Json.JsonDocument.Parse(raw);
        var expenseId = json.RootElement[0].GetProperty("id").GetGuid();

        var patch = await _client.SendAsync(NewRequest(HttpMethod.Patch, $"/api/v1/expenses/{expenseId}", owner,
            new { category = "IRRIGATION", amount = 200, occurred_at_utc = DateTime.UtcNow }));
        patch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var delete = await _client.SendAsync(NewRequest(HttpMethod.Delete, $"/api/v1/expenses/{expenseId}", owner));
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ActivityCostPatchDistinguishesOmittedNullAndPositiveValues()
    {
        var (owner, farmId, periodId) = await CreateFarmWithPeriod();
        var created = await _client.SendAsync(NewRequest(HttpMethod.Post, $"/api/v1/farms/{farmId}/activities", owner,
            new { activity_name = "Gübreleme", activity_type = "FERTILIZATION", cost = 100f, crop_period_id = periodId }));
        var activity = await created.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(CustomWebApplicationFactory.JsonOptions);
        var activityId = activity!["id"].GetGuid();

        var omitted = await _client.SendAsync(NewRequest(HttpMethod.Patch, $"/api/v1/activities/{activityId}", owner,
            new { description = "Not değişti" }));
        omitted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await omitted.Content.ReadAsStringAsync()).Should().Contain("\"cost\":100");

        var cleared = await _client.SendAsync(NewRequest(HttpMethod.Patch, $"/api/v1/activities/{activityId}", owner,
            new { cost = (float?)null }));
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cleared.Content.ReadAsStringAsync()).Should().Contain("\"cost\":null");

        var restored = await _client.SendAsync(NewRequest(HttpMethod.Patch, $"/api/v1/activities/{activityId}", owner,
            new { cost = 1250.75f }));
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await restored.Content.ReadAsStringAsync()).Should().Contain("\"cost\":1250.75");
    }

    private async Task<(Guid Owner, Guid FarmId, Guid PeriodId)> CreateFarmWithPeriod()
    {
        var owner = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync("/api/v1/farms", new CreateFarmRequest(owner, "Finans Tarlası",
            38.4, 27.1, 5, null, "Buğday", null, new DateOnly(2026, 1, 1)), CustomWebApplicationFactory.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>(CustomWebApplicationFactory.JsonOptions))!["id"];
        using var get = NewRequest(HttpMethod.Get, $"/api/v1/farms/{id}", owner, body: null, role: "Farmer");
        var farm = await (await _client.SendAsync(get)).Content.ReadFromJsonAsync<FarmDto>(CustomWebApplicationFactory.JsonOptions);
        return (owner, id, farm!.CurrentCropPeriod!.Id);
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string path, Guid userId, object? body = null, string? role = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-User-Id", userId.ToString());
        if (role is not null) request.Headers.Add("X-User-Role", role);
        if (body is not null) request.Content = JsonContent.Create(body, options: CustomWebApplicationFactory.JsonOptions);
        return request;
    }
}

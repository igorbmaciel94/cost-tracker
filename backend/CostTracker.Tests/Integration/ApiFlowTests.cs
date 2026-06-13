using System.Net;
using System.Net.Http.Json;
using CostTracker.Application.Contracts;
using CostTracker.Domain.Constants;

namespace CostTracker.Tests.Integration;

public class ApiFlowTests
{
    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = TestWebApplicationFactory.TestUsername,
            Password = TestWebApplicationFactory.TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_ShouldRequireAuthentication()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/months");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginLogoutAndSession_ShouldManageCookieAuthentication()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var session = await client.GetFromJsonAsync<AuthSessionDto>("/api/auth/session");
        Assert.NotNull(session);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(TestWebApplicationFactory.TestUsername, session.Username);

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var protectedResponse = await client.GetAsync("/api/months");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateNewMonth_ShouldCloseOldAndKeepOneOpen()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var initialMonths = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var initialOpen = Assert.Single(initialMonths!, x => x.Status == "OPEN");

        var response = await client.PostAsJsonAsync("/api/months/new", new CreateMonthRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var finalMonths = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        Assert.NotNull(finalMonths);
        Assert.Equal(2, finalMonths.Count);
        Assert.Single(finalMonths, x => x.Status == "OPEN");
        Assert.Single(finalMonths, x => x.Status == "CLOSED");
        Assert.Equal(initialOpen.Salary, finalMonths.Single(x => x.Status == "OPEN").Salary);
    }

    [Fact]
    public async Task ClosedMonth_ShouldRejectWrites()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        await client.PostAsJsonAsync("/api/months/new", new CreateMonthRequest());
        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var closed = Assert.Single(months!, x => x.Status == "CLOSED");

        var response = await client.PutAsJsonAsync($"/api/months/{closed.Id}/salary", new UpdateSalaryRequest { Salary = 3000m });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EntryCrud_ShouldWorkOnOpenMonth()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var openMonth = Assert.Single(months!, x => x.Status == "OPEN");

        var budget = await client.GetFromJsonAsync<BudgetResponseDto>($"/api/months/{openMonth.Id}/budget");
        var category = budget!.Lines.First();

        var createResponse = await client.PostAsJsonAsync($"/api/months/{openMonth.Id}/entries", new CreateEntryRequest
        {
            CategoryBudgetId = category.Id,
            EntryDate = new DateOnly(2026, 2, 28),
            Description = "Compra Teste",
            Amount = 20m
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var entriesResponse = await createResponse.Content.ReadFromJsonAsync<EntriesResponseDto>();
        var createdEntry = Assert.Single(entriesResponse!.Items, x => x.Description == "Compra Teste");

        var updateResponse = await client.PutAsJsonAsync($"/api/months/{openMonth.Id}/entries/{createdEntry.Id}", new UpdateEntryRequest
        {
            CategoryBudgetId = category.Id,
            EntryDate = new DateOnly(2026, 2, 28),
            Description = "Compra Teste Atualizada",
            Amount = 25m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/months/{openMonth.Id}/entries/{createdEntry.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var finalEntries = await client.GetFromJsonAsync<EntriesResponseDto>($"/api/months/{openMonth.Id}/entries");
        Assert.DoesNotContain(finalEntries!.Items, x => x.Id == createdEntry.Id);
    }

    [Fact]
    public async Task UpdateTargets_ShouldPersistValues()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var openMonth = Assert.Single(months!, x => x.Status == "OPEN");

        var response = await client.PutAsJsonAsync($"/api/months/{openMonth.Id}/targets", new UpdateTargetsRequest
        {
            Items =
            [
                new UpdateTargetGroupRequest { GroupName = GroupNames.CustosFixos, TargetPercent = 0.65m },
                new UpdateTargetGroupRequest { GroupName = GroupNames.Prazeres, TargetPercent = 0.25m }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var targets = await client.GetFromJsonAsync<TargetsResponseDto>($"/api/months/{openMonth.Id}/targets");
        Assert.Equal(0.65m, targets!.Items.Single(x => x.GroupName == GroupNames.CustosFixos).TargetPercent);
        Assert.Equal(0.25m, targets.Items.Single(x => x.GroupName == GroupNames.Prazeres).TargetPercent);
        Assert.Contains(targets.Items, x => x.GroupName == GroupNames.Conhecimento && x.TargetPercent == 0m);
        Assert.Contains(targets.Items, x => x.GroupName == GroupNames.LiberdadeFinanceira && x.TargetPercent == 0.1m);
        Assert.Contains(targets.Items, x => x.GroupName == GroupNames.Metas && x.TargetPercent == 0m);
        Assert.Contains(targets.Items, x => x.GroupName == GroupNames.Conforto && x.TargetPercent == 0m);
    }

    [Fact]
    public async Task Dashboard_ShouldExposeOverBudgetCategories()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var openMonth = Assert.Single(months!, x => x.Status == "OPEN");

        var budget = await client.GetFromJsonAsync<BudgetResponseDto>($"/api/months/{openMonth.Id}/budget");
        var category = budget!.Lines.First();

        await client.PostAsJsonAsync($"/api/months/{openMonth.Id}/entries", new CreateEntryRequest
        {
            CategoryBudgetId = category.Id,
            EntryDate = new DateOnly(2026, 3, 10),
            Description = "Estouro teste",
            Amount = category.Planned + 25m
        });

        var dashboard = await client.GetFromJsonAsync<DashboardDto>($"/api/months/{openMonth.Id}/dashboard");
        var overBudget = Assert.Single(dashboard!.OverBudgetCategories);

        Assert.Equal(category.Name, overBudget.Category);
        Assert.Equal(category.GroupName, overBudget.GroupName);
        Assert.Equal(category.Planned, overBudget.Planned);
        Assert.Equal(category.Planned + 25m, overBudget.Spent);
        Assert.Equal(25m, overBudget.ExceededBy);
    }

    [Fact]
    public async Task SeededData_ShouldExposeCanonicalGroupsAndCategories()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        await LoginAsync(client);

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var openMonth = Assert.Single(months!, x => x.Status == "OPEN");

        var budget = await client.GetFromJsonAsync<BudgetResponseDto>($"/api/months/{openMonth.Id}/budget");
        Assert.Contains(budget!.Lines, line => line.Name == "Investimento" && line.GroupName == GroupNames.Conhecimento);
        Assert.Contains(budget.Lines, line => line.Name == "Saving" && line.GroupName == GroupNames.LiberdadeFinanceira);

        var targets = await client.GetFromJsonAsync<TargetsResponseDto>($"/api/months/{openMonth.Id}/targets");
        Assert.Contains(targets!.Items, item => item.GroupName == GroupNames.Metas && item.TargetPercent == 0m);
        Assert.Contains(targets.Items, item => item.GroupName == GroupNames.Conforto && item.TargetPercent == 0m);
    }
}

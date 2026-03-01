using System.Net;
using System.Net.Http.Json;
using CostTracker.Api.Contracts;
using CostTracker.Domain.Constants;

namespace CostTracker.Tests.Integration;

public class ApiFlowTests
{
    [Fact]
    public async Task CreateNewMonth_ShouldCloseOldAndKeepOneOpen()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

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

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        var openMonth = Assert.Single(months!, x => x.Status == "OPEN");

        var response = await client.PutAsJsonAsync($"/api/months/{openMonth.Id}/targets", new UpdateTargetsRequest
        {
            Items =
            [
                new UpdateTargetGroupRequest { GroupName = GroupNames.Essenciais, TargetPercent = 0.65m },
                new UpdateTargetGroupRequest { GroupName = GroupNames.Desejos, TargetPercent = 0.25m }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var targets = await client.GetFromJsonAsync<TargetsResponseDto>($"/api/months/{openMonth.Id}/targets");
        Assert.Equal(0.65m, targets!.Items.Single(x => x.GroupName == GroupNames.Essenciais).TargetPercent);
        Assert.Equal(0.25m, targets.Items.Single(x => x.GroupName == GroupNames.Desejos).TargetPercent);
    }
}

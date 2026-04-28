using System.Net;
using System.Net.Http.Json;
using CostTracker.Application.Contracts;
using CostTracker.Application.Integrations.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CostTracker.Tests.Integration;

public class AiAnalysisTests
{
    private static async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var factory = new AiAnalysisTestFactory();
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = TestWebApplicationFactory.TestUsername,
            Password = TestWebApplicationFactory.TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return client;
    }

    [Fact]
    public async Task GenerateAnalysis_WithInvalidMonthId_ShouldReturn404()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/months/{Guid.NewGuid()}/ai-analysis", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateAnalysis_WithValidMonth_ShouldReturnPdf()
    {
        var client = await CreateAuthenticatedClientAsync();

        var months = await client.GetFromJsonAsync<List<MonthSummaryDto>>("/api/months");
        Assert.NotNull(months);
        Assert.NotEmpty(months);

        var monthId = months[0].Id;
        var response = await client.PostAsync($"/api/months/{monthId}/ai-analysis", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}

public class AiAnalysisTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiAnalysisClient>();
            services.AddSingleton<IAiAnalysisClient, FakeAiAnalysisClient>();
        });
    }
}

public class FakeAiAnalysisClient : IAiAnalysisClient
{
    public Task<string> GenerateAnalysisAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        return Task.FromResult("""
            # Resumo Executivo
            Análise financeira gerada pelo cliente de teste.

            ## Análise do Mês Atual
            Gastos dentro do previsto.

            ## Status das Metas por Grupo
            Todas as metas foram atingidas.

            ## Tendências Históricas
            Estabilidade financeira observada.

            ## Alertas e Pontos de Atenção
            - Nenhum alerta crítico.

            ## Recomendações de Planejamento
            - Manter o orçamento atual.

            ## Projeção para o Próximo Mês
            Tendência de estabilidade.
            """);
    }
}

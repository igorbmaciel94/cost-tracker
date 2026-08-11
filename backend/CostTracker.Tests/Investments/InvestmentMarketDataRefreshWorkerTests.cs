using CostTracker.Api.Services;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Application.Services;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Investments;

public sealed class InvestmentMarketDataRefreshWorkerTests
{
    [Fact]
    public async Task PortfolioChangeSignal_ShouldRefreshWithoutWaitingForDailySchedule()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var marketDataOptions = Options.Create(new MarketDataOptions
        {
            EnableBackgroundRefresh = true,
            RefreshOnStartup = false,
            RefreshTimeZone = "Europe/Lisbon",
            RefreshHour = 6
        });
        var provider = new NotifyingExchangeRateProvider(now);
        var services = new ServiceCollection();
        services.AddDbContext<CostTrackerDbContext>(options =>
            options.UseInMemoryDatabase($"refresh-worker-{Guid.NewGuid():N}"));
        services.AddScoped<ICostTrackerDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<CostTrackerDbContext>());
        services.AddScoped<PortfolioProjectionService>();
        services.AddScoped<InvestmentMarketDataService>();
        services.AddSingleton<IExchangeRateProvider>(provider);
        services.AddSingleton<IOptions<MarketDataOptions>>(marketDataOptions);
        services.AddSingleton<TimeProvider>(timeProvider);
        await using var serviceProvider = services.BuildServiceProvider();
        var signal = new InvestmentMarketDataRefreshSignal();
        var worker = new InvestmentMarketDataRefreshWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            marketDataOptions,
            timeProvider,
            signal,
            NullLogger<InvestmentMarketDataRefreshWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        signal.RequestRefresh();
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
    }

    private sealed class NotifyingExchangeRateProvider(DateTimeOffset fetchedAt) : IExchangeRateProvider
    {
        public string ProviderCode => "TEST_FX";
        public int CallCount { get; private set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProviderBatchResult<ExchangeRateResult>> GetLatestRatesAsync(
            IReadOnlyCollection<string> quoteCurrencies,
            DateOnly asOf,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Called.TrySetResult();
            IReadOnlyList<ExchangeRateResult> rates = quoteCurrencies.Select(currency => new ExchangeRateResult(
                ProviderCode,
                "EUR",
                currency,
                1m,
                "TEST",
                asOf,
                fetchedAt,
                false,
                new string('c', 64))).ToList();
            return Task.FromResult(new ProviderBatchResult<ExchangeRateResult>(rates, []));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

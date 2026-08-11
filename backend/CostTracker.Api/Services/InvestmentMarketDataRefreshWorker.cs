using CostTracker.Application.Options;
using CostTracker.Application.Services;
using Microsoft.Extensions.Options;

namespace CostTracker.Api.Services;

public sealed class InvestmentMarketDataRefreshWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider,
    ILogger<InvestmentMarketDataRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableBackgroundRefresh)
        {
            logger.LogInformation("Investment market-data background refresh is disabled.");
            return;
        }

        if (options.Value.RefreshOnStartup)
            await RefreshSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRefresh();
            logger.LogInformation("Next investment market-data refresh in {Delay}.", delay);
            await Task.Delay(delay, timeProvider, stoppingToken);
            await RefreshSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<InvestmentMarketDataService>();
            var status = await service.RefreshAsync(cancellationToken);
            logger.LogInformation(
                "Investment market-data refresh completed with status {Freshness}; missing={MissingCount}, stale={StaleCount}, providerFailures={FailureCount}.",
                status.Freshness,
                status.MissingInstrumentIds.Count,
                status.StaleInstrumentIds.Count,
                status.Failures.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Investment market-data refresh failed; the previous snapshots remain available.");
        }
    }

    private TimeSpan DelayUntilNextRefresh()
    {
        var zone = ResolveTimeZone(options.Value.RefreshTimeZone);
        var now = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var nextLocal = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            options.Value.RefreshHour,
            0,
            0,
            DateTimeKind.Unspecified);
        if (nextLocal <= localNow.DateTime)
            nextLocal = nextLocal.AddDays(1);

        var nextUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocal, zone), TimeSpan.Zero);
        var delay = nextUtc - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1);
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

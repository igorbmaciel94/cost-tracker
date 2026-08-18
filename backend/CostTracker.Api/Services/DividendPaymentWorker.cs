using CostTracker.Application.Options;
using CostTracker.Application.Services;
using Microsoft.Extensions.Options;

namespace CostTracker.Api.Services;

public sealed class DividendPaymentWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DividendOptions> options,
    TimeProvider timeProvider,
    DividendProcessingSignal processingSignal,
    ILogger<DividendPaymentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableBackgroundProcessing)
        {
            logger.LogInformation("Dividend background processing is disabled.");
            return;
        }

        if (options.Value.ProcessOnStartup)
            await ProcessSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextProcessing();
            logger.LogInformation("Next dividend processing in {Delay}.", delay);
            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var scheduled = Task.Delay(delay, timeProvider, waitCancellation.Token);
            var requested = processingSignal.WaitAsync(waitCancellation.Token).AsTask();
            var completed = await Task.WhenAny(scheduled, requested);
            waitCancellation.Cancel();

            try
            {
                await Task.WhenAll(scheduled, requested);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // The losing wait is cancelled after either the schedule or signal wins.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            logger.LogInformation(
                completed == requested
                    ? "Dividend processing requested after an event registration."
                    : "Scheduled dividend processing started.");
            await ProcessSafelyAsync(stoppingToken);
        }
    }

    private async Task ProcessSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<DividendService>();
            var result = await service.ProcessDueAsync(ProcessingCutoffDate(), cancellationToken);
            logger.LogInformation(
                "Dividend processing completed; processed={ProcessedCount}, noEntitlement={NoEntitlementCount}, missingFx={MissingFxCount}.",
                result.ProcessedCount,
                result.NoEntitlementCount,
                result.MissingFxCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Dividend processing failed; scheduled events remain pending for retry.");
        }
    }

    private DateOnly ProcessingCutoffDate()
    {
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTimeZone());
        var cutoff = DateOnly.FromDateTime(localNow.DateTime);
        return localNow.Hour < options.Value.ProcessingHour ? cutoff.AddDays(-1) : cutoff;
    }

    private TimeSpan DelayUntilNextProcessing()
    {
        var zone = ResolveTimeZone();
        var now = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var nextLocal = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            options.Value.ProcessingHour,
            0,
            0,
            DateTimeKind.Unspecified);
        if (nextLocal <= localNow.DateTime)
            nextLocal = nextLocal.AddDays(1);

        var nextUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocal, zone), TimeSpan.Zero);
        var delay = nextUtc - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1);
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.Value.ProcessingTimeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

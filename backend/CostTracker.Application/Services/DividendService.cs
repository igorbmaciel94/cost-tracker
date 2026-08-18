using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Investments.Dividends;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Application.Services;

public sealed class DividendService(
    ICostTrackerDbContext dbContext,
    IDividendProcessingScheduler processingScheduler,
    IOptions<DividendOptions> options,
    TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim ProcessingGate = new(1, 1);

    public async Task<IReadOnlyList<DividendEventDto>> GetEventsAsync(
        Guid? instrumentId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DividendEvents
            .AsNoTracking()
            .Include(item => item.Instrument)
            .AsQueryable();
        if (instrumentId.HasValue)
            query = query.Where(item => item.InstrumentId == instrumentId.Value);

        var events = await query
            .OrderByDescending(item => item.PaymentDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var today = LocalDate();
        return events.Select(item => ToDto(item, today)).ToList();
    }

    public async Task<DividendEventDto> CreateEventAsync(
        Guid instrumentId,
        CreateDividendEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        var existing = await dbContext.DividendEvents
            .AsNoTracking()
            .Include(item => item.Instrument)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
            return ToDto(existing, LocalDate());

        var instrument = await dbContext.InvestmentInstruments
            .SingleOrDefaultAsync(item => item.Id == instrumentId, cancellationToken)
            ?? throw new NotFoundException("Investment instrument not found.");
        if (instrument.IsArchived)
            throw new DomainValidationException("Dividend events cannot be added to an archived instrument.");
        if (instrument.ValuationMode != ValuationMode.MarketQuote)
            throw new DomainValidationException("Dividend events are only allowed for MARKET_QUOTE instruments.");

        if (request.GrossAmountPerUnit <= 0m)
            throw new DomainValidationException("grossAmountPerUnit must be greater than zero.");
        if (request.WithholdingTaxPercent is < 0m or >= 100m)
            throw new DomainValidationException("withholdingTaxPercent must be between zero and less than 100.");
        if (request.ExDate == default)
            throw new DomainValidationException("exDate is required.");
        if (request.PaymentDate == default || request.PaymentDate < request.ExDate)
            throw new DomainValidationException("paymentDate must be on or after exDate.");
        if (request.Notes?.Length > 512)
            throw new DomainValidationException("notes cannot exceed 512 characters.");

        CurrencyCode currency;
        try
        {
            currency = new CurrencyCode(request.Currency);
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }

        var now = timeProvider.GetUtcNow();
        var dividendEvent = new DividendEvent
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Instrument = instrument,
            GrossAmountPerUnit = decimal.Round(request.GrossAmountPerUnit, 12, MidpointRounding.ToEven),
            WithholdingTaxRate = decimal.Round(request.WithholdingTaxPercent / 100m, 8, MidpointRounding.ToEven),
            Currency = currency,
            ExDate = request.ExDate,
            PaymentDate = request.PaymentDate,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.DividendEvents.Add(dividendEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        processingScheduler.RequestProcessing();

        return ToDto(dividendEvent, LocalDate());
    }

    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var dividendEvent = await dbContext.DividendEvents
            .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new NotFoundException("Dividend event not found.");
        if (dividendEvent.ProcessedAt.HasValue)
            throw new ConflictException("A credited dividend cannot be deleted.");

        dbContext.DividendEvents.Remove(dividendEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DividendCashSummaryDto> GetCashSummaryAsync(CancellationToken cancellationToken = default)
    {
        var credits = await dbContext.DividendEvents
            .AsNoTracking()
            .Where(item => item.ProcessedAt.HasValue && item.NetAmount.HasValue && item.NetAmount > 0m)
            .ToListAsync(cancellationToken);
        if (credits.Count == 0)
            return new DividendCashSummaryDto(0m, false, []);

        var today = LocalDate();
        var currencies = credits.Select(item => item.Currency).Where(item => item != CurrencyCode.Eur).Distinct().ToList();
        var rates = await dbContext.FxRateSnapshots
            .AsNoTracking()
            .Where(item => item.BaseCurrency == CurrencyCode.Eur && currencies.Contains(item.QuoteCurrency) && item.AsOf <= today)
            .ToListAsync(cancellationToken);
        var latestRates = rates
            .GroupBy(item => item.QuoteCurrency.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AsOf).ThenByDescending(item => item.FetchedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        var partial = false;
        var balances = credits
            .GroupBy(item => item.Currency.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var amount = group.Sum(item => item.NetAmount!.Value);
                if (string.Equals(group.Key, CurrencyCode.Eur.Value, StringComparison.OrdinalIgnoreCase))
                {
                    var identity = new FxRateReferenceDto(today, null, "IDENTITY", DataFreshnessCodes.Fresh, false, 1m, "EUR", "EUR", "IDENTITY");
                    return new DividendCashBalanceDto(group.Key, amount, amount, group.Max(item => item.PaymentDate), identity);
                }

                if (!latestRates.TryGetValue(group.Key, out var rate))
                {
                    partial = true;
                    return new DividendCashBalanceDto(group.Key, amount, null, group.Max(item => item.PaymentDate), null);
                }

                var reference = new FxRateReferenceDto(
                    rate.AsOf,
                    rate.FetchedAt,
                    rate.ProviderCode,
                    DataFreshnessCodes.Fresh,
                    rate.IsFallback,
                    rate.Rate,
                    rate.BaseCurrency.Value,
                    rate.QuoteCurrency.Value,
                    rate.RateKind);
                return new DividendCashBalanceDto(
                    group.Key,
                    amount,
                    decimal.Round(amount / rate.Rate, 8, MidpointRounding.ToEven),
                    group.Max(item => item.PaymentDate),
                    reference);
            })
            .ToList();

        var knownAmounts = balances.Where(item => item.AmountEur.HasValue).Sum(item => item.AmountEur!.Value);
        return new DividendCashSummaryDto(partial ? null : knownAmounts, partial, balances);
    }

    public async Task<DividendProcessingResult> ProcessDueAsync(
        DateOnly cutoffDate,
        CancellationToken cancellationToken = default)
    {
        await ProcessingGate.WaitAsync(cancellationToken);
        try
        {
            var dueEvents = await dbContext.DividendEvents
                .Include(item => item.Instrument)
                .ThenInclude(item => item.Transactions)
                .Where(item => !item.ProcessedAt.HasValue && item.PaymentDate <= cutoffDate)
                .OrderBy(item => item.PaymentDate)
                .ToListAsync(cancellationToken);
            if (dueEvents.Count == 0)
                return new DividendProcessingResult(0, 0, 0);

            var currencies = dueEvents.Select(item => item.Currency).Where(item => item != CurrencyCode.Eur).Distinct().ToList();
            var rates = await dbContext.FxRateSnapshots
                .AsNoTracking()
                .Where(item => item.BaseCurrency == CurrencyCode.Eur && currencies.Contains(item.QuoteCurrency) && item.AsOf <= cutoffDate)
                .ToListAsync(cancellationToken);

            var processed = 0;
            var noEntitlement = 0;
            var missingFx = 0;
            var now = timeProvider.GetUtcNow();
            foreach (var dividendEvent in dueEvents)
            {
                FxRateSnapshot? fxRate = null;
                if (dividendEvent.Currency != CurrencyCode.Eur)
                {
                    fxRate = rates
                        .Where(item => item.QuoteCurrency == dividendEvent.Currency && item.AsOf <= dividendEvent.PaymentDate)
                        .OrderByDescending(item => item.AsOf)
                        .ThenByDescending(item => item.FetchedAt)
                        .FirstOrDefault();
                    if (fxRate is null)
                        missingFx++;
                }

                var eligibleQuantity = CalculateEligibleQuantity(dividendEvent.Instrument.Transactions, dividendEvent.ExDate);
                var grossAmount = decimal.Round(eligibleQuantity * dividendEvent.GrossAmountPerUnit, 12, MidpointRounding.ToEven);
                var taxAmount = decimal.Round(grossAmount * dividendEvent.WithholdingTaxRate, 12, MidpointRounding.ToEven);
                var netAmount = grossAmount - taxAmount;
                var currencyPerEur = dividendEvent.Currency == CurrencyCode.Eur ? 1m : fxRate?.Rate;

                dividendEvent.EligibleQuantity = eligibleQuantity;
                dividendEvent.GrossAmount = grossAmount;
                dividendEvent.WithholdingTaxAmount = taxAmount;
                dividendEvent.NetAmount = netAmount;
                dividendEvent.CurrencyPerEurRate = currencyPerEur;
                dividendEvent.NetAmountEur = currencyPerEur.HasValue
                    ? decimal.Round(netAmount / currencyPerEur.Value, 12, MidpointRounding.ToEven)
                    : null;
                dividendEvent.FxAsOf = fxRate?.AsOf ?? (dividendEvent.Currency == CurrencyCode.Eur ? dividendEvent.PaymentDate : null);
                dividendEvent.FxProviderCode = fxRate?.ProviderCode ?? (dividendEvent.Currency == CurrencyCode.Eur ? "IDENTITY" : null);
                dividendEvent.ProcessedAt = now;
                dividendEvent.UpdatedAt = now;
                processed++;
                if (eligibleQuantity == 0m)
                    noEntitlement++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new DividendProcessingResult(processed, noEntitlement, missingFx);
        }
        finally
        {
            ProcessingGate.Release();
        }
    }

    private static decimal CalculateEligibleQuantity(
        IEnumerable<InvestmentTransaction> transactions,
        DateOnly exDate)
    {
        var quantity = 0m;
        foreach (var transaction in transactions
                     .Where(item => item.TransactionDate < exDate)
                     .OrderBy(item => item.TransactionDate)
                     .ThenBy(item => item.CreatedAt))
        {
            var transactionQuantity = transaction.Quantity ?? 0m;
            quantity += transaction.Type switch
            {
                InvestmentTransactionType.OpeningBalance or InvestmentTransactionType.Buy => transactionQuantity,
                InvestmentTransactionType.Sell => -transactionQuantity,
                InvestmentTransactionType.Adjustment => transactionQuantity,
                _ => 0m
            };
        }

        return decimal.Round(decimal.Max(0m, quantity), 12, MidpointRounding.ToEven);
    }

    private static DividendEventDto ToDto(DividendEvent dividendEvent, DateOnly today)
    {
        var status = dividendEvent.ProcessedAt.HasValue
            ? dividendEvent.EligibleQuantity is null or <= 0m
                ? DividendEventStatusCodes.NoEntitlement
                : DividendEventStatusCodes.Credited
            : dividendEvent.PaymentDate <= today
                ? DividendEventStatusCodes.Due
                : DividendEventStatusCodes.Scheduled;
        return new DividendEventDto(
            dividendEvent.Id,
            dividendEvent.InstrumentId,
            dividendEvent.Instrument.Name,
            dividendEvent.Instrument.Ticker,
            dividendEvent.GrossAmountPerUnit,
            dividendEvent.WithholdingTaxRate * 100m,
            dividendEvent.Currency.Value,
            dividendEvent.ExDate,
            dividendEvent.PaymentDate,
            dividendEvent.Notes,
            status,
            dividendEvent.EligibleQuantity,
            dividendEvent.GrossAmount,
            dividendEvent.WithholdingTaxAmount,
            dividendEvent.NetAmount,
            dividendEvent.CurrencyPerEurRate,
            dividendEvent.NetAmountEur,
            dividendEvent.FxAsOf,
            dividendEvent.FxProviderCode,
            dividendEvent.ProcessedAt,
            dividendEvent.CreatedAt,
            !dividendEvent.ProcessedAt.HasValue);
    }

    private static void ValidateIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
            throw new DomainValidationException("idempotencyKey is required and cannot exceed 128 characters.");
    }

    private DateOnly LocalDate()
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.ProcessingTimeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }
}

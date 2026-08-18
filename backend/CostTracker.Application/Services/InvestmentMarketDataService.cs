using CostTracker.Application.Exceptions;
using CostTracker.Application.Contracts;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Application.Services;

/// <summary>
/// Owns the market-data boundary for investments: provider orchestration, immutable daily
/// snapshots, freshness rules and the single EUR valuation projection used by the UI and planner.
/// </summary>
public sealed class InvestmentMarketDataService(
    ICostTrackerDbContext dbContext,
    IEnumerable<IMarketQuoteProvider> quoteProviders,
    IEnumerable<IExchangeRateProvider> exchangeRateProviders,
    PortfolioProjectionService portfolioProjection,
    IOptions<MarketDataOptions> options,
    TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private static readonly string[] BaselineFxCurrencies = ["BRL", "USD", "GBP", "GBX"];
    private readonly IReadOnlyList<IMarketQuoteProvider> _quoteProviders = quoteProviders.ToList();
    private readonly IReadOnlyList<IExchangeRateProvider> _exchangeRateProviders = exchangeRateProviders.ToList();

    public async Task<MarketDataStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        => await BuildStatusAsync([], cancellationToken);

    public async Task<MarketDataStatusDto> RefreshAsync(
        CancellationToken cancellationToken = default,
        bool retryStaleSources = false)
    {
        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var today = LocalDate(now);
            var failures = new List<ProviderFailure>();
            var instruments = await dbContext.InvestmentInstruments
                .Where(item => !item.IsArchived)
                .Include(item => item.Transactions.Where(transaction => transaction.TransactionDate <= today))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var instrumentsRequiringValuation = instruments
                .Where(instrument => RequiresMarketData(instrument))
                .ToList();

            await RefreshExchangeRatesAsync(
                instrumentsRequiringValuation,
                today,
                retryStaleSources,
                failures,
                cancellationToken);
            await RefreshQuotesAsync(
                instrumentsRequiringValuation
                    .Where(item => item.ValuationMode == ValuationMode.MarketQuote)
                    .ToList(),
                today,
                retryStaleSources,
                failures,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildStatusAsync(failures, cancellationToken);
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    public async Task<IReadOnlyList<MarketInstrumentMappingDto>> GetMappingsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.InvestmentInstruments.AnyAsync(item => item.Id == instrumentId, cancellationToken))
            throw new NotFoundException("Investment instrument not found.");

        return await dbContext.MarketInstrumentMappings
            .Where(item => item.InstrumentId == instrumentId)
            .OrderBy(item => item.ProviderCode)
            .Select(item => new MarketInstrumentMappingDto(
                item.Id,
                item.InstrumentId,
                item.ProviderCode,
                item.ProviderSymbol,
                item.Exchange,
                item.Mic,
                item.QuoteCurrency.Value,
                item.PriceMultiplier,
                item.IsEnabled,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MarketInstrumentMappingDto> UpsertMappingAsync(
        Guid instrumentId,
        UpsertMarketInstrumentMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var instrument = await dbContext.InvestmentInstruments
            .SingleOrDefaultAsync(item => item.Id == instrumentId, cancellationToken)
            ?? throw new NotFoundException("Investment instrument not found.");

        if (instrument.ValuationMode != ValuationMode.MarketQuote)
            throw new DomainValidationException("Market mappings are only allowed for MARKET_QUOTE instruments.");

        var providerCode = NormalizeProvider(request.ProviderCode);
        if (_quoteProviders.All(provider => !string.Equals(provider.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase)))
            throw new DomainValidationException("providerCode is not a configured market quote provider.");
        if (string.IsNullOrWhiteSpace(request.ProviderSymbol) || request.ProviderSymbol.Trim().Length > 128)
            throw new DomainValidationException("providerSymbol is required and cannot exceed 128 characters.");
        if (request.PriceMultiplier <= 0m)
            throw new DomainValidationException("priceMultiplier must be greater than zero.");

        CurrencyCode quoteCurrency;
        try
        {
            quoteCurrency = new CurrencyCode(request.QuoteCurrency);
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }

        if (quoteCurrency != instrument.NativeCurrency)
        {
            throw new DomainValidationException(
                "quoteCurrency must equal the instrument nativeCurrency after applying priceMultiplier.");
        }

        var now = timeProvider.GetUtcNow();
        var mapping = await dbContext.MarketInstrumentMappings.SingleOrDefaultAsync(
            item => item.InstrumentId == instrumentId && item.ProviderCode == providerCode,
            cancellationToken);
        if (mapping is null)
        {
            mapping = new MarketInstrumentMapping
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                ProviderCode = providerCode,
                CreatedAt = now
            };
            dbContext.MarketInstrumentMappings.Add(mapping);
        }

        mapping.ProviderSymbol = request.ProviderSymbol.Trim();
        mapping.Exchange = NormalizeOptional(request.Exchange, 64, "exchange");
        mapping.Mic = NormalizeOptional(request.Mic, 16, "mic")?.ToUpperInvariant();
        mapping.QuoteCurrency = quoteCurrency;
        mapping.PriceMultiplier = request.PriceMultiplier;
        mapping.IsEnabled = request.IsEnabled;
        mapping.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MarketInstrumentMappingDto(
            mapping.Id,
            mapping.InstrumentId,
            mapping.ProviderCode,
            mapping.ProviderSymbol,
            mapping.Exchange,
            mapping.Mic,
            mapping.QuoteCurrency.Value,
            mapping.PriceMultiplier,
            mapping.IsEnabled,
            mapping.UpdatedAt);
    }

    public async Task RecordManualQuoteAsync(
        Guid instrumentId,
        ManualMarketQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var instrument = await dbContext.InvestmentInstruments
            .SingleOrDefaultAsync(item => item.Id == instrumentId && !item.IsArchived, cancellationToken)
            ?? throw new NotFoundException("Investment instrument not found.");
        if (instrument.ValuationMode != ValuationMode.MarketQuote)
            throw new DomainValidationException("Manual market quotes are only allowed for MARKET_QUOTE instruments.");
        if (request.Price <= 0m)
            throw new DomainValidationException("price must be greater than zero.");
        if (request.AsOf == default || request.AsOf > LocalDate(timeProvider.GetUtcNow()))
            throw new DomainValidationException("asOf must be a valid date that is not in the future.");

        CurrencyCode currency;
        try
        {
            currency = new CurrencyCode(request.Currency);
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }

        if (currency != instrument.NativeCurrency)
            throw new DomainValidationException("currency must match the instrument nativeCurrency.");
        var requestedSymbol = NormalizeOptional(request.ProviderSymbol, 128, "providerSymbol");
        var symbol = requestedSymbol ??
            instrument.Ticker ?? instrument.PublicIdentifier ?? instrument.Isin ?? instrument.Id.ToString("N");
        var fetchedAt = timeProvider.GetUtcNow();
        dbContext.MarketQuoteSnapshots.Add(new MarketQuoteSnapshot
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            ProviderCode = MarketDataProviderCodes.Manual,
            ProviderSymbol = symbol,
            Exchange = NormalizeOptional(request.Exchange, 64, "exchange"),
            Mic = NormalizeOptional(request.Mic, 16, "mic")?.ToUpperInvariant(),
            Price = request.Price,
            Currency = currency,
            PriceKind = "MANUAL_CLOSE",
            AsOf = request.AsOf,
            FetchedAt = fetchedAt,
            IsFallback = true,
            RawPayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{instrumentId:N}|{request.AsOf:yyyy-MM-dd}|{request.Price}|{currency.Value}|{fetchedAt:O}")))
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ValuedPortfolioDto> GetPortfolioValuationAsync(CancellationToken cancellationToken = default)
    {
        var today = LocalDate(timeProvider.GetUtcNow());
        var portfolio = await dbContext.InvestmentPortfolios
            .Include(item => item.AllocationTargets)
            .Include(item => item.Instruments.Where(instrument => !instrument.IsArchived))
                .ThenInclude(item => item.Transactions.Where(transaction => transaction.TransactionDate <= today))
            .Include(item => item.Instruments.Where(instrument => !instrument.IsArchived))
                .ThenInclude(item => item.ManualValuations.Where(valuation => valuation.AsOf <= today))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Investment portfolio not found.");

        var instruments = portfolio.Instruments.Where(item => !item.IsArchived).ToList();
        var instrumentIds = instruments.Select(item => item.Id).ToList();
        var allQuotes = await dbContext.MarketQuoteSnapshots
            .Where(item => instrumentIds.Contains(item.InstrumentId) && item.AsOf <= today)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var quotes = allQuotes
            .GroupBy(item => item.InstrumentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AsOf).ThenByDescending(item => item.FetchedAt).First());

        var positions = instruments.ToDictionary(item => item.Id, portfolioProjection.CalculatePosition);
        var requiredCurrencies = instruments
            .Where(instrument => RequiresMarketData(instrument))
            .Select(item => item.NativeCurrency)
            .Where(currency => currency != CurrencyCode.Eur)
            .Distinct()
            .ToList();
        var allRates = await dbContext.FxRateSnapshots
            .Where(item => item.BaseCurrency == CurrencyCode.Eur &&
                           requiredCurrencies.Contains(item.QuoteCurrency) &&
                           item.AsOf <= today)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var rates = allRates
            .GroupBy(item => item.QuoteCurrency.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AsOf).ThenByDescending(item => item.FetchedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        var provisional = new List<ProvisionalValuation>(instruments.Count);
        foreach (var instrument in instruments)
        {
            var position = positions[instrument.Id];
            quotes.TryGetValue(instrument.Id, out var quote);
            rates.TryGetValue(instrument.NativeCurrency.Value, out var fxRate);
            var requiresMarketData = RequiresMarketData(instrument, position.Quantity);

            decimal? nativeValue;
            DateOnly? valueAsOf;
            string valueFreshness;
            DataReferenceDto? quoteReference = null;
            if (!requiresMarketData)
            {
                nativeValue = 0m;
                valueAsOf = null;
                valueFreshness = DataFreshnessCodes.Fresh;
            }
            else if (instrument.ValuationMode == ValuationMode.MarketQuote)
            {
                nativeValue = quote is null ? null : position.Quantity * quote.Price;
                valueAsOf = quote?.AsOf;
                valueFreshness = quote is null ? DataFreshnessCodes.Missing : ClassifyQuote(quote.AsOf, today);
                if (quote is not null)
                {
                    quoteReference = new DataReferenceDto(
                        quote.AsOf,
                        quote.FetchedAt,
                        quote.ProviderCode,
                        valueFreshness,
                        quote.IsFallback);
                }
            }
            else
            {
                nativeValue = position.CurrentManualValueNative;
                valueAsOf = position.CurrentManualValueAsOf;
                valueFreshness = valueAsOf.HasValue
                    ? ClassifyManualValuation(valueAsOf.Value, today)
                    : DataFreshnessCodes.Missing;
            }

            decimal? eurValue;
            string fxFreshness;
            DataReferenceDto? fxReference;
            if (!requiresMarketData)
            {
                eurValue = 0m;
                fxFreshness = DataFreshnessCodes.Fresh;
                fxReference = null;
            }
            else if (instrument.NativeCurrency == CurrencyCode.Eur)
            {
                eurValue = nativeValue;
                fxFreshness = DataFreshnessCodes.Fresh;
                fxReference = new DataReferenceDto(today, timeProvider.GetUtcNow(), "IDENTITY", DataFreshnessCodes.Fresh, false);
            }
            else if (fxRate is null)
            {
                eurValue = null;
                fxFreshness = DataFreshnessCodes.Missing;
                fxReference = null;
            }
            else
            {
                eurValue = nativeValue / fxRate.Rate;
                fxFreshness = ClassifyQuote(fxRate.AsOf, today);
                fxReference = new DataReferenceDto(
                    fxRate.AsOf,
                    fxRate.FetchedAt,
                    fxRate.ProviderCode,
                    fxFreshness,
                    fxRate.IsFallback);
            }

            provisional.Add(new ProvisionalValuation(
                instrument,
                position,
                quote,
                nativeValue,
                eurValue,
                Worst(valueFreshness, fxFreshness),
                quoteReference,
                fxReference,
                valueAsOf));
        }

        var total = provisional.Where(item => item.ValueEur.HasValue).Sum(item => item.ValueEur!.Value);
        var classTotals = provisional
            .GroupBy(item => item.Instrument.AssetClass)
            .ToDictionary(group => group.Key, group => group.Where(item => item.ValueEur.HasValue).Sum(item => item.ValueEur!.Value));
        var allValued = provisional.Count > 0 && provisional.All(item => item.ValueEur.HasValue);
        var allCostsKnown = provisional.All(item => item.Position.NetInvestedEur.HasValue);
        decimal? knownCost = allCostsKnown ? provisional.Sum(item => item.Position.NetInvestedEur!.Value) : null;

        var valuedPositions = provisional.Select(item =>
        {
            var classTotal = classTotals.GetValueOrDefault(item.Instrument.AssetClass);
            decimal? gainLoss = item.ValueEur.HasValue && item.Position.NetInvestedEur.HasValue
                ? item.ValueEur.Value - item.Position.NetInvestedEur.Value
                : null;
            return new ValuedInvestmentPositionDto(
                item.Instrument.Id,
                item.Instrument.Version,
                item.Instrument.Name,
                item.Instrument.Ticker,
                item.Instrument.Mic,
                item.Instrument.Isin,
                item.Instrument.AssetClass.ToCode(),
                item.Instrument.Kind.ToCode(),
                item.Instrument.ValuationMode.ToCode(),
                item.Instrument.NativeCurrency.Value,
                item.Instrument.AllocationScore,
                item.Instrument.ValuationMode == ValuationMode.MarketQuote ? item.Position.Quantity : null,
                item.Instrument.ValuationMode == ValuationMode.Manual ? item.Position.CurrentManualValueNative : null,
                item.Quote?.Price,
                item.Position.AverageCostNative,
                item.Position.NetInvestedEur,
                item.Position.NetInvestedEur,
                item.NativeValue,
                item.ValueEur,
                gainLoss,
                allValued && total > 0m && item.ValueEur.HasValue ? item.ValueEur.Value / total : null,
                allValued && classTotal > 0m && item.ValueEur.HasValue ? item.ValueEur.Value / classTotal : null,
                item.Instrument.QuantityStep ?? 0.000001m,
                item.Instrument.IsArchived,
                item.Freshness,
                item.QuoteReference,
                item.FxReference,
                item.ValueAsOf);
        }).ToList();

        var targets = portfolio.AllocationTargets
            .OrderBy(item => item.AssetClass)
            .Select(item => new ValuedAllocationTargetDto(
                item.AssetClass.ToCode(),
                item.Weight,
                allValued && total > 0m ? classTotals.GetValueOrDefault(item.AssetClass) / total : 0m,
                classTotals.GetValueOrDefault(item.AssetClass)))
            .ToList();
        var freshness = provisional.Count == 0
            ? DataFreshnessCodes.Missing
            : provisional.Select(item => item.Freshness).Aggregate(Worst);
        var asOfDates = provisional
            .SelectMany(item => new DateOnly?[] { item.ValueAsOf, item.FxReference?.AsOf })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        DateOnly? asOf = asOfDates.Count == 0 ? null : asOfDates.Min();

        return new ValuedPortfolioDto(
            portfolio.Id,
            portfolio.BaseCurrency.Value,
            portfolio.Version,
            portfolio.AllocationTargets.Count == Enum.GetValues<AssetClass>().Length &&
            portfolio.AllocationTargets.Select(item => item.AssetClass).Distinct().Count() == portfolio.AllocationTargets.Count &&
            portfolio.AllocationTargets.All(item => item.Weight * 100m == decimal.Truncate(item.Weight * 100m)) &&
            portfolio.AllocationTargets.Sum(item => item.Weight) == 1m,
            targets,
            new PortfolioValuationSummaryDto(
                total,
                knownCost,
                allValued && knownCost.HasValue ? total - knownCost.Value : null,
                asOf,
                freshness,
                !allValued),
            valuedPositions);
    }

    private async Task RefreshQuotesAsync(
        IReadOnlyList<InvestmentInstrument> instruments,
        DateOnly today,
        bool retryStaleSources,
        List<ProviderFailure> failures,
        CancellationToken cancellationToken)
    {
        if (instruments.Count == 0)
            return;

        var instrumentIds = instruments.Select(instrument => instrument.Id).ToList();
        var mappings = await dbContext.MarketInstrumentMappings
            .Where(item => instrumentIds.Contains(item.InstrumentId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var snapshotMetadata = await dbContext.MarketQuoteSnapshots
            .Where(item => instrumentIds.Contains(item.InstrumentId))
            .Select(item => new { item.InstrumentId, item.AsOf, item.FetchedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var refreshedToday = snapshotMetadata
            .GroupBy(item => item.InstrumentId)
            .Where(group => LocalDate(group.Max(item => item.FetchedAt)) == today)
            .Where(group => !retryStaleSources ||
                            ClassifyQuote(
                                group.OrderByDescending(item => item.AsOf)
                                    .ThenByDescending(item => item.FetchedAt)
                                    .First()
                                    .AsOf,
                                today) == DataFreshnessCodes.Fresh)
            .Select(group => group.Key)
            .ToHashSet();
        var unresolved = instruments
            .Where(item => !refreshedToday.Contains(item.Id))
            .ToDictionary(item => item.Id);

        foreach (var provider in _quoteProviders)
        {
            if (unresolved.Count == 0)
                break;
            if (provider.ProviderCode == MarketDataProviderCodes.YahooTest && !options.Value.EnablePublicTestQuotes)
                continue;

            var requests = new List<MarketQuoteRequest>();
            foreach (var instrument in unresolved.Values)
            {
                var explicitMapping = mappings.SingleOrDefault(item =>
                    item.InstrumentId == instrument.Id &&
                    string.Equals(item.ProviderCode, provider.ProviderCode, StringComparison.OrdinalIgnoreCase));
                if (explicitMapping is { IsEnabled: false })
                    continue;
                // Marketstack EOD rows do not identify the quote currency/unit.
                // Requiring an explicit mapping prevents an LSE pence quote from
                // being labelled as GBP and overstating the position by 100x.
                if (provider.ProviderCode == MarketDataProviderCodes.Marketstack && explicitMapping is null)
                    continue;
                var symbol = explicitMapping?.ProviderSymbol ?? ResolveProviderSymbol(
                    provider.ProviderCode,
                    instrument.Ticker,
                    instrument.Mic);
                if (string.IsNullOrWhiteSpace(symbol))
                    continue;

                requests.Add(new MarketQuoteRequest(
                    instrument.Id,
                    symbol,
                    explicitMapping?.Exchange,
                    explicitMapping?.Mic ?? instrument.Mic,
                    (explicitMapping?.QuoteCurrency ?? instrument.NativeCurrency).Value,
                    explicitMapping?.PriceMultiplier ?? 1m));
            }

            ProviderBatchResult<MarketQuoteResult> result;
            try
            {
                result = await provider.GetLatestQuotesAsync(requests, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.AddRange(requests.Select(request => new ProviderFailure(
                    provider.ProviderCode, request.ProviderSymbol, exception.Message, true)));
                continue;
            }

            failures.AddRange(result.Failures);
            foreach (var quote in result.Items.Where(item => unresolved.ContainsKey(item.InstrumentId)))
            {
                if (!string.Equals(
                        quote.Currency,
                        unresolved[quote.InstrumentId].NativeCurrency.Value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(new ProviderFailure(
                        quote.Provider,
                        quote.ProviderSymbol,
                        $"Currency mismatch for instrument {quote.InstrumentId:D}.",
                        false));
                    continue;
                }

                if (TryAddQuoteSnapshot(quote, today))
                    unresolved.Remove(quote.InstrumentId);
                else
                    failures.Add(new ProviderFailure(quote.Provider, quote.ProviderSymbol, "Provider quote failed validation.", false));
            }
        }

        failures.AddRange(unresolved.Values.Select(instrument => new ProviderFailure(
            "ALL",
            instrument.Ticker ?? instrument.Name,
            "No configured provider returned a quote for this instrument.",
            false)));
    }

    private static string? ResolveProviderSymbol(string providerCode, string? ticker, string? mic)
    {
        if (string.IsNullOrWhiteSpace(ticker) ||
            !string.Equals(providerCode, MarketDataProviderCodes.YahooTest, StringComparison.OrdinalIgnoreCase) ||
            ticker.Contains('.', StringComparison.Ordinal))
        {
            return ticker;
        }

        return mic?.Trim().ToUpperInvariant() switch
        {
            "XLON" or "LSE" => $"{ticker}.L",
            _ => ticker
        };
    }

    private async Task RefreshExchangeRatesAsync(
        IReadOnlyList<InvestmentInstrument> instruments,
        DateOnly today,
        bool retryStaleSources,
        List<ProviderFailure> failures,
        CancellationToken cancellationToken)
    {
        var unresolved = BaselineFxCurrencies
            .Concat(instruments.Select(item => item.NativeCurrency.Value))
            .Where(currency => currency != "EUR")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (unresolved.Count > 0)
        {
            var currencies = unresolved.Select(value => new CurrencyCode(value)).ToList();
            var snapshotMetadata = await dbContext.FxRateSnapshots
                .Where(item => item.BaseCurrency == CurrencyCode.Eur && currencies.Contains(item.QuoteCurrency))
                .Select(item => new { item.QuoteCurrency, item.AsOf, item.FetchedAt })
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            foreach (var group in snapshotMetadata.GroupBy(item => item.QuoteCurrency))
            {
                if (LocalDate(group.Max(item => item.FetchedAt)) != today)
                    continue;

                var current = group
                    .OrderByDescending(item => item.AsOf)
                    .ThenByDescending(item => item.FetchedAt)
                    .First();
                if (!retryStaleSources || ClassifyQuote(current.AsOf, today) == DataFreshnessCodes.Fresh)
                    unresolved.Remove(group.Key.Value);
            }
        }

        foreach (var provider in _exchangeRateProviders)
        {
            if (unresolved.Count == 0)
                break;

            ProviderBatchResult<ExchangeRateResult> result;
            try
            {
                var providerCurrencies = unresolved
                    .Select(currency => currency == "GBX" ? "GBP" : currency)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result = await provider.GetLatestRatesAsync(providerCurrencies, today, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.AddRange(unresolved.Select(currency => new ProviderFailure(
                    provider.ProviderCode, $"EUR/{currency}", exception.Message, true)));
                continue;
            }

            failures.AddRange(result.Failures);
            foreach (var rate in result.Items.Where(item =>
                         item.BaseCurrency == "EUR" && item.QuoteCurrency != "EUR"))
            {
                if (unresolved.Contains(rate.QuoteCurrency))
                {
                    if (TryAddRateSnapshot(rate, today))
                        unresolved.Remove(rate.QuoteCurrency);
                    else
                        failures.Add(new ProviderFailure(rate.Provider, $"{rate.BaseCurrency}/{rate.QuoteCurrency}", "Provider rate failed validation.", false));
                }

                if (rate.QuoteCurrency == "GBP" && unresolved.Contains("GBX"))
                {
                    var penceRate = rate with
                    {
                        QuoteCurrency = "GBX",
                        Rate = rate.Rate * 100m,
                        RateKind = $"{rate.RateKind}_PENCE_DERIVED"
                    };
                    if (TryAddRateSnapshot(penceRate, today))
                        unresolved.Remove("GBX");
                    else
                        failures.Add(new ProviderFailure(rate.Provider, "EUR/GBX", "Derived pence rate failed validation.", false));
                }
            }
        }

        failures.AddRange(unresolved.Select(currency => new ProviderFailure(
            "ALL", $"EUR/{currency}", "No configured provider returned this exchange rate.", false)));
    }

    private bool TryAddQuoteSnapshot(MarketQuoteResult quote, DateOnly today)
    {
        if (quote.Price <= 0m || quote.AsOf > today ||
            string.IsNullOrWhiteSpace(quote.Provider) || quote.Provider.Length > 32 ||
            string.IsNullOrWhiteSpace(quote.ProviderSymbol) || quote.ProviderSymbol.Length > 128 ||
            string.IsNullOrWhiteSpace(quote.PriceKind) || quote.PriceKind.Length > 32 ||
            string.IsNullOrWhiteSpace(quote.RawPayloadHash) || quote.RawPayloadHash.Length > 64)
            return false;

        CurrencyCode currency;
        try
        {
            currency = new CurrencyCode(quote.Currency);
        }
        catch (ArgumentException)
        {
            return false;
        }

        dbContext.MarketQuoteSnapshots.Add(new MarketQuoteSnapshot
        {
            Id = Guid.NewGuid(),
            InstrumentId = quote.InstrumentId,
            ProviderCode = quote.Provider,
            ProviderSymbol = quote.ProviderSymbol,
            Exchange = quote.Exchange,
            Mic = quote.Mic,
            Price = quote.Price,
            Currency = currency,
            PriceKind = quote.PriceKind,
            AsOf = quote.AsOf,
            FetchedAt = quote.FetchedAt,
            IsFallback = quote.IsFallback,
            RawPayloadHash = quote.RawPayloadHash
        });
        return true;
    }

    private bool TryAddRateSnapshot(ExchangeRateResult rate, DateOnly today)
    {
        CurrencyCode baseCurrency;
        CurrencyCode quoteCurrency;
        try
        {
            baseCurrency = new CurrencyCode(rate.BaseCurrency);
            quoteCurrency = new CurrencyCode(rate.QuoteCurrency);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (rate.Rate <= 0m || rate.AsOf > today || baseCurrency == quoteCurrency ||
            string.IsNullOrWhiteSpace(rate.Provider) || rate.Provider.Length > 32 ||
            string.IsNullOrWhiteSpace(rate.RateKind) || rate.RateKind.Length > 32 ||
            string.IsNullOrWhiteSpace(rate.RawPayloadHash) || rate.RawPayloadHash.Length > 64)
            return false;

        dbContext.FxRateSnapshots.Add(new FxRateSnapshot
        {
            Id = Guid.NewGuid(),
            ProviderCode = rate.Provider,
            BaseCurrency = baseCurrency,
            QuoteCurrency = quoteCurrency,
            Rate = rate.Rate,
            RateKind = rate.RateKind,
            AsOf = rate.AsOf,
            FetchedAt = rate.FetchedAt,
            IsFallback = rate.IsFallback,
            RawPayloadHash = rate.RawPayloadHash
        });
        return true;
    }

    private async Task<MarketDataStatusDto> BuildStatusAsync(
        IReadOnlyList<ProviderFailure> failures,
        CancellationToken cancellationToken)
    {
        var today = LocalDate(timeProvider.GetUtcNow());
        var instruments = await dbContext.InvestmentInstruments
            .Where(item => !item.IsArchived)
            .Include(item => item.Transactions.Where(transaction => transaction.TransactionDate <= today))
            .Include(item => item.ManualValuations.Where(valuation => valuation.AsOf <= today))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var ids = instruments.Select(item => item.Id).ToList();
        var quotes = (await dbContext.MarketQuoteSnapshots
                .Where(item => ids.Contains(item.InstrumentId) && item.AsOf <= today)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.InstrumentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.AsOf).ThenByDescending(item => item.FetchedAt).First());
        var neededCurrencies = instruments.Select(item => item.NativeCurrency)
            .Where(currency => currency != CurrencyCode.Eur)
            .Distinct()
            .ToList();
        var rates = (await dbContext.FxRateSnapshots
                .Where(item => item.BaseCurrency == CurrencyCode.Eur &&
                               neededCurrencies.Contains(item.QuoteCurrency) &&
                               item.AsOf <= today)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.QuoteCurrency.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.AsOf).ThenByDescending(item => item.FetchedAt).First(), StringComparer.OrdinalIgnoreCase);

        var staleIds = new HashSet<Guid>();
        var missingIds = new HashSet<Guid>();
        var statuses = new List<string>();
        var dates = new List<DateOnly>();
        var fetched = new List<DateTimeOffset>();
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var instrument in instruments)
        {
            if (!RequiresMarketData(instrument))
            {
                statuses.Add(DataFreshnessCodes.Fresh);
                continue;
            }

            string valueStatus;
            if (instrument.ValuationMode == ValuationMode.MarketQuote)
            {
                if (!quotes.TryGetValue(instrument.Id, out var quote))
                {
                    valueStatus = DataFreshnessCodes.Missing;
                }
                else
                {
                    valueStatus = ClassifyQuote(quote.AsOf, today);
                    dates.Add(quote.AsOf);
                    fetched.Add(quote.FetchedAt);
                    sources.Add(quote.ProviderCode);
                }
            }
            else
            {
                var valuation = instrument.ManualValuations
                    .OrderByDescending(item => item.AsOf)
                    .ThenByDescending(item => item.RecordedAt)
                    .FirstOrDefault();
                valueStatus = valuation is null
                    ? DataFreshnessCodes.Missing
                    : ClassifyManualValuation(valuation.AsOf, today);
                if (valuation is not null)
                {
                    dates.Add(valuation.AsOf);
                    fetched.Add(valuation.RecordedAt);
                    sources.Add("MANUAL_VALUATION");
                }
            }

            var fxStatus = DataFreshnessCodes.Fresh;
            if (instrument.NativeCurrency != CurrencyCode.Eur)
            {
                if (!rates.TryGetValue(instrument.NativeCurrency.Value, out var rate))
                {
                    fxStatus = DataFreshnessCodes.Missing;
                }
                else
                {
                    fxStatus = ClassifyQuote(rate.AsOf, today);
                    dates.Add(rate.AsOf);
                    fetched.Add(rate.FetchedAt);
                    sources.Add(rate.ProviderCode);
                }
            }

            var status = Worst(valueStatus, fxStatus);
            statuses.Add(status);
            if (status == DataFreshnessCodes.Missing)
                missingIds.Add(instrument.Id);
            else if (status is DataFreshnessCodes.Stale or DataFreshnessCodes.Blocked)
                staleIds.Add(instrument.Id);
        }

        var overall = statuses.Count == 0 ? DataFreshnessCodes.Missing : statuses.Aggregate(Worst);
        var message = overall switch
        {
            DataFreshnessCodes.Fresh => "Cotações, câmbio e saldos manuais estão dentro da janela configurada.",
            DataFreshnessCodes.Stale => "Existem dados desatualizados; um aporte exige confirmação explícita para usar estes valores.",
            DataFreshnessCodes.Blocked => "Existem dados antigos demais para calcular um aporte com segurança.",
            _ => instruments.Count == 0
                ? "Cadastre ao menos um ativo para valorar a carteira."
                : "Faltam cotações, câmbio ou saldos manuais para valorar toda a carteira."
        };

        return new MarketDataStatusDto(
            dates.Count == 0 ? null : dates.Min(),
            fetched.Count == 0 ? null : fetched.Max(),
            sources.Count == 0 ? null : string.Join(", ", sources.Order()),
            overall,
            message,
            staleIds.Order().ToList(),
            missingIds.Order().ToList(),
            failures);
    }

    private string ClassifyQuote(DateOnly asOf, DateOnly today)
    {
        var age = BusinessSessionsBetween(asOf, today);
        if (age <= options.Value.QuoteWarningSessions)
            return DataFreshnessCodes.Fresh;
        return age <= options.Value.QuoteBlockingSessions
            ? DataFreshnessCodes.Stale
            : DataFreshnessCodes.Blocked;
    }

    private string ClassifyManualValuation(DateOnly asOf, DateOnly today)
    {
        var age = Math.Max(0, today.DayNumber - asOf.DayNumber);
        if (age <= options.Value.ManualValuationWarningDays)
            return DataFreshnessCodes.Fresh;
        return age <= options.Value.ManualValuationBlockingDays
            ? DataFreshnessCodes.Stale
            : DataFreshnessCodes.Blocked;
    }

    private DateOnly LocalDate(DateTimeOffset instant)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.RefreshTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }

    private bool RequiresMarketData(InvestmentInstrument instrument)
        => RequiresMarketData(instrument, portfolioProjection.CalculatePosition(instrument).Quantity);

    private static bool RequiresMarketData(InvestmentInstrument instrument, decimal quantity)
        => instrument.ValuationMode != ValuationMode.MarketQuote ||
           quantity != 0m ||
           instrument.AllocationScore > 0;

    private static int BusinessSessionsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from)
            return 0;

        var sessions = 0;
        for (var date = from.AddDays(1); date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                sessions++;
        }

        return sessions;
    }

    private static string Worst(string left, string right)
        => Rank(left) >= Rank(right) ? left : right;

    private static int Rank(string freshness) => freshness switch
    {
        DataFreshnessCodes.Fresh => 0,
        DataFreshnessCodes.Stale => 1,
        DataFreshnessCodes.Blocked => 2,
        _ => 3
    };

    private static string NormalizeProvider(string? value)
        => value?.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant() ?? string.Empty;

    private static string? NormalizeOptional(string? value, int maximumLength, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
            throw new DomainValidationException($"{field} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    private sealed record ProvisionalValuation(
        InvestmentInstrument Instrument,
        InvestmentPositionDto Position,
        MarketQuoteSnapshot? Quote,
        decimal? NativeValue,
        decimal? ValueEur,
        string Freshness,
        DataReferenceDto? QuoteReference,
        DataReferenceDto? FxReference,
        DateOnly? ValueAsOf);
}

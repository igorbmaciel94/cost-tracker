using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Application.Services;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Investments;

public sealed class InvestmentMarketDataServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 11, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Refresh_ShouldSeedEverySupportedFxCurrencyWithoutInstruments()
    {
        var options = new DbContextOptionsBuilder<CostTrackerDbContext>()
            .UseInMemoryDatabase($"market-data-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new CostTrackerDbContext(options);
        var provider = new CapturingExchangeRateProvider(FixedNow);
        var service = new InvestmentMarketDataService(
            dbContext,
            [],
            [provider],
            new PortfolioProjectionService(),
            Options.Create(new MarketDataOptions { RefreshTimeZone = "Europe/Lisbon" }),
            new FixedTimeProvider(FixedNow));

        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Equal(["BRL", "GBP", "USD"], provider.RequestedCurrencies.Order().ToArray());
        Assert.Equal(1, provider.CallCount);
        var rates = (await dbContext.FxRateSnapshots.ToListAsync())
            .OrderBy(rate => rate.QuoteCurrency.Value)
            .ToList();
        Assert.Equal(["BRL", "GBP", "GBX", "USD"], rates.Select(rate => rate.QuoteCurrency.Value).ToArray());
        Assert.Equal(125m, rates.Single(rate => rate.QuoteCurrency.Value == "GBX").Rate);
    }

    [Fact]
    public async Task Refresh_ShouldResolveProviderSymbolFromExchangeMic()
    {
        var options = new DbContextOptionsBuilder<CostTrackerDbContext>()
            .UseInMemoryDatabase($"market-symbols-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new CostTrackerDbContext(options);
        var portfolio = InvestmentPortfolio.Create(FixedNow);
        portfolio.Instruments.Add(CreateQuotedInstrument(portfolio, "VWRA", "XLON"));
        portfolio.Instruments.Add(CreateQuotedInstrument(portfolio, "O", "XNYS"));
        dbContext.InvestmentPortfolios.Add(portfolio);
        await dbContext.SaveChangesAsync();

        var quoteProvider = new CapturingQuoteProvider(FixedNow);
        var service = new InvestmentMarketDataService(
            dbContext,
            [quoteProvider],
            [new CapturingExchangeRateProvider(FixedNow)],
            new PortfolioProjectionService(),
            Options.Create(new MarketDataOptions
            {
                RefreshTimeZone = "Europe/Lisbon",
                EnablePublicTestQuotes = true
            }),
            new FixedTimeProvider(FixedNow));

        await service.RefreshAsync();

        Assert.Equal(["O", "VWRA.L"], quoteProvider.Requests.Select(request => request.ProviderSymbol).Order().ToArray());
    }

    [Fact]
    public async Task Refresh_ShouldRetryAStaleQuoteOnlyWhenRequestedLaterOnTheSameDay()
    {
        var now = new DateTimeOffset(2026, 8, 18, 6, 44, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<CostTrackerDbContext>()
            .UseInMemoryDatabase($"market-stale-retry-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new CostTrackerDbContext(options);
        var portfolio = InvestmentPortfolio.Create(now);
        portfolio.Instruments.Add(CreateQuotedInstrument(portfolio, "BAC", "XNYS"));
        dbContext.InvestmentPortfolios.Add(portfolio);
        await dbContext.SaveChangesAsync();

        var quoteProvider = new SequencedQuoteProvider(
            now,
            new DateOnly(2026, 8, 14),
            new DateOnly(2026, 8, 17));
        var service = new InvestmentMarketDataService(
            dbContext,
            [quoteProvider],
            [new CapturingExchangeRateProvider(now)],
            new PortfolioProjectionService(),
            Options.Create(new MarketDataOptions
            {
                RefreshTimeZone = "Europe/Lisbon",
                EnablePublicTestQuotes = true
            }),
            new FixedTimeProvider(now));

        var first = await service.RefreshAsync();
        var deduplicated = await service.RefreshAsync();
        var retried = await service.RefreshAsync(retryStaleSources: true);

        Assert.Equal(DataFreshnessCodes.Stale, first.Freshness);
        Assert.Equal(DataFreshnessCodes.Stale, deduplicated.Freshness);
        Assert.Equal(2, quoteProvider.CallCount);
        Assert.Equal(DataFreshnessCodes.Fresh, retried.Freshness);
    }

    [Fact]
    public async Task Refresh_ShouldRetryStaleExchangeRatesOnlyWhenRequestedLaterOnTheSameDay()
    {
        var now = new DateTimeOffset(2026, 8, 18, 6, 44, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<CostTrackerDbContext>()
            .UseInMemoryDatabase($"fx-stale-retry-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new CostTrackerDbContext(options);
        var exchangeRateProvider = new SequencedExchangeRateProvider(
            now,
            new DateOnly(2026, 8, 14),
            new DateOnly(2026, 8, 17));
        var service = new InvestmentMarketDataService(
            dbContext,
            [],
            [exchangeRateProvider],
            new PortfolioProjectionService(),
            Options.Create(new MarketDataOptions { RefreshTimeZone = "Europe/Lisbon" }),
            new FixedTimeProvider(now));

        await service.RefreshAsync();
        await service.RefreshAsync();
        await service.RefreshAsync(retryStaleSources: true);

        Assert.Equal(2, exchangeRateProvider.CallCount);
        Assert.All(
            await dbContext.FxRateSnapshots
                .GroupBy(rate => rate.QuoteCurrency)
                .Select(group => group.Max(rate => rate.AsOf))
                .ToListAsync(),
            asOf => Assert.Equal(new DateOnly(2026, 8, 17), asOf));
    }

    private static InvestmentInstrument CreateQuotedInstrument(
        InvestmentPortfolio portfolio,
        string ticker,
        string mic)
        => new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            Portfolio = portfolio,
            AssetClass = ticker == "O" ? AssetClass.Reits : AssetClass.Stocks,
            Kind = ticker == "O" ? InstrumentKind.Reit : InstrumentKind.Etf,
            Name = ticker,
            Ticker = ticker,
            Mic = mic,
            IdentityKey = $"TICKER:{mic}:{ticker}",
            NativeCurrency = new CurrencyCode("USD"),
            ValuationMode = ValuationMode.MarketQuote,
            AllocationScore = 10,
            QuantityStep = 0.000001m,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow
        };

    private sealed class CapturingQuoteProvider(DateTimeOffset fetchedAt) : IMarketQuoteProvider
    {
        public string ProviderCode => MarketDataProviderCodes.YahooTest;
        public IReadOnlyList<MarketQuoteRequest> Requests { get; private set; } = [];

        public Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
            IReadOnlyList<MarketQuoteRequest> requests,
            CancellationToken cancellationToken)
        {
            Requests = requests;
            IReadOnlyList<MarketQuoteResult> quotes = requests.Select(request => new MarketQuoteResult(
                request.InstrumentId,
                ProviderCode,
                request.ProviderSymbol,
                request.Exchange,
                request.Mic,
                100m,
                request.ExpectedCurrency,
                "LATEST_AVAILABLE",
                DateOnly.FromDateTime(fetchedAt.Date),
                fetchedAt,
                true,
                new string('b', 64))).ToList();
            return Task.FromResult(new ProviderBatchResult<MarketQuoteResult>(quotes, []));
        }
    }

    private sealed class SequencedQuoteProvider(
        DateTimeOffset fetchedAt,
        params DateOnly[] dates) : IMarketQuoteProvider
    {
        public string ProviderCode => MarketDataProviderCodes.YahooTest;
        public int CallCount { get; private set; }

        public Task<ProviderBatchResult<MarketQuoteResult>> GetLatestQuotesAsync(
            IReadOnlyList<MarketQuoteRequest> requests,
            CancellationToken cancellationToken)
        {
            var asOf = dates[Math.Min(CallCount, dates.Length - 1)];
            CallCount++;
            IReadOnlyList<MarketQuoteResult> quotes = requests.Select(request => new MarketQuoteResult(
                request.InstrumentId,
                ProviderCode,
                request.ProviderSymbol,
                request.Exchange,
                request.Mic,
                100m,
                request.ExpectedCurrency,
                "LATEST_AVAILABLE",
                asOf,
                fetchedAt,
                true,
                new string('c', 64))).ToList();
            return Task.FromResult(new ProviderBatchResult<MarketQuoteResult>(quotes, []));
        }
    }

    private sealed class CapturingExchangeRateProvider(DateTimeOffset fetchedAt) : IExchangeRateProvider
    {
        public string ProviderCode => "TEST_FX";
        public IReadOnlyCollection<string> RequestedCurrencies { get; private set; } = [];
        public int CallCount { get; private set; }

        public Task<ProviderBatchResult<ExchangeRateResult>> GetLatestRatesAsync(
            IReadOnlyCollection<string> quoteCurrencies,
            DateOnly asOf,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestedCurrencies = quoteCurrencies.ToArray();
            IReadOnlyList<ExchangeRateResult> rates = quoteCurrencies
                .Select(currency => new ExchangeRateResult(
                    ProviderCode,
                    "EUR",
                    currency,
                    currency == "GBP" ? 1.25m : 2m,
                    "TEST",
                    asOf,
                    fetchedAt,
                    false,
                    new string('a', 64)))
                .ToList();
            return Task.FromResult(new ProviderBatchResult<ExchangeRateResult>(rates, []));
        }
    }

    private sealed class SequencedExchangeRateProvider(
        DateTimeOffset fetchedAt,
        params DateOnly[] dates) : IExchangeRateProvider
    {
        public string ProviderCode => "TEST_FX";
        public int CallCount { get; private set; }

        public Task<ProviderBatchResult<ExchangeRateResult>> GetLatestRatesAsync(
            IReadOnlyCollection<string> quoteCurrencies,
            DateOnly asOf,
            CancellationToken cancellationToken)
        {
            var resultAsOf = dates[Math.Min(CallCount, dates.Length - 1)];
            CallCount++;
            IReadOnlyList<ExchangeRateResult> rates = quoteCurrencies
                .Select(currency => new ExchangeRateResult(
                    ProviderCode,
                    "EUR",
                    currency,
                    2m,
                    "TEST",
                    resultAsOf,
                    fetchedAt,
                    false,
                    new string('d', 64)))
                .ToList();
            return Task.FromResult(new ProviderBatchResult<ExchangeRateResult>(rates, []));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

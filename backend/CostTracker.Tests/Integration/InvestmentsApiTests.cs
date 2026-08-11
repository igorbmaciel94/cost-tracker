using System.Net;
using System.Net.Http.Json;
using CostTracker.Application.Contracts;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Domain.Entities;
using CostTracker.Domain.ValueObjects;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Integration;

public class InvestmentsApiTests
{
    [Fact]
    public async Task Allocation_ShouldRequireAllFourClassesAndPersistAtomically()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);

        var initial = await client.GetFromJsonAsync<InvestmentPortfolioDto>("/api/investments/portfolio");
        Assert.NotNull(initial);
        Assert.Equal("EUR", initial.BaseCurrency);
        Assert.False(initial.IsConfigured);
        Assert.Empty(initial.AllocationTargets);

        var invalidResponse = await client.PutAsJsonAsync("/api/investments/allocation", new UpdateInvestmentAllocationRequest
        {
            ExpectedVersion = initial.Version,
            Items =
            [
                new() { AssetClass = "STOCKS", Weight = 0.4m },
                new() { AssetClass = "REITS", Weight = 0.1m },
                new() { AssetClass = "BRAZIL_FIXED_INCOME", Weight = 0.3m }
            ]
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var validResponse = await client.PutAsJsonAsync("/api/investments/allocation", AllocationRequest(initial.Version));
        Assert.True(
            validResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {validResponse.StatusCode}: {await validResponse.Content.ReadAsStringAsync()}");
        var configured = await validResponse.Content.ReadFromJsonAsync<InvestmentPortfolioDto>();

        Assert.NotNull(configured);
        Assert.True(configured.IsConfigured);
        Assert.Equal(1m, configured.AllocationTargets.Sum(item => item.Weight));
        Assert.Equal(initial.Version + 1, configured.Version);
    }

    [Fact]
    public async Task ManualInstrument_ShouldKeepValuationHistoryEstimateCashFlowsAndArchiveSoftly()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/investments/instruments", new CreateInvestmentInstrumentRequest
        {
            AssetClass = "BRAZIL_FIXED_INCOME",
            Kind = "BOND",
            Name = "Tesouro Selic",
            NativeCurrency = "BRL",
            ValuationMode = "MANUAL",
            AllocationScore = 0,
            ManualValuation = new CreateManualValuationRequest
            {
                Amount = 10_000m,
                Currency = "BRL",
                AsOf = new DateOnly(2026, 1, 31),
                IdempotencyKey = "tesouro-opening-value"
            }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(10_000m, created.Instrument.Position.CurrentManualValueNative);

        var deposit = new CreateInvestmentTransactionRequest
        {
            TransactionType = "DEPOSIT",
            TransactionDate = new DateOnly(2026, 2, 1),
            Amount = 500m,
            Currency = "BRL",
            CurrencyPerEurRate = 6m,
            IdempotencyKey = "tesouro-deposit-1"
        };
        var depositResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/transactions",
            deposit);
        Assert.Equal(HttpStatusCode.OK, depositResponse.StatusCode);
        var afterDeposit = await depositResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.Equal(10_500m, afterDeposit!.Instrument.Position.CurrentManualValueNative);
        Assert.True(afterDeposit.Instrument.Position.IsManualValueEstimated);

        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/transactions",
            deposit);
        var afterDuplicate = await duplicateResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.Single(afterDuplicate!.Transactions);

        var valuationResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/manual-valuations",
            new CreateManualValuationRequest
            {
                Amount = 10_650m,
                Currency = "BRL",
                AsOf = new DateOnly(2026, 2, 28),
                IdempotencyKey = "tesouro-value-feb"
            });
        var afterValuation = await valuationResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.Equal(10_650m, afterValuation!.Instrument.Position.CurrentManualValueNative);
        Assert.False(afterValuation.Instrument.Position.IsManualValueEstimated);
        Assert.Equal(2, afterValuation.ManualValuations.Count);

        var archiveResponse = await client.PostAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/archive?expectedVersion={afterValuation.Instrument.Version}",
            null);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        var active = await client.GetFromJsonAsync<List<InvestmentInstrumentDto>>("/api/investments/instruments");
        Assert.Empty(active!);
        var all = await client.GetFromJsonAsync<List<InvestmentInstrumentDto>>("/api/investments/instruments?includeArchived=true");
        var archived = Assert.Single(all!);
        Assert.True(archived.IsArchived);
    }

    [Fact]
    public async Task MarketTransactions_ShouldDeriveQuantityCostAndEnforceIdempotencyAndConcurrency()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/investments/instruments", new CreateInvestmentInstrumentRequest
        {
            AssetClass = "STOCKS",
            Kind = "STOCK",
            Name = "Coca-Cola",
            Ticker = "KO",
            Mic = "XNYS",
            NativeCurrency = "USD",
            ValuationMode = "MARKET_QUOTE",
            AllocationScore = 10,
            QuantityStep = 0.000001m,
            OpeningTransaction = new CreateInvestmentTransactionRequest
            {
                TransactionType = "OPENING_BALANCE",
                TransactionDate = new DateOnly(2026, 1, 1),
                Quantity = 1.5m,
                UnitPrice = 100m,
                Currency = "USD",
                CurrencyPerEurRate = 1.2m,
                IdempotencyKey = "ko-opening"
            }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        var buy = new CreateInvestmentTransactionRequest
        {
            TransactionType = "BUY",
            TransactionDate = new DateOnly(2026, 2, 1),
            Quantity = 0.5m,
            UnitPrice = 120m,
            Currency = "USD",
            CurrencyPerEurRate = 1.2m,
            IdempotencyKey = "ko-buy-1"
        };
        var buyResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/transactions",
            buy);
        var afterBuy = await buyResponse.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>();
        Assert.Equal(2m, afterBuy!.Instrument.Position.Quantity);
        Assert.Equal(210m, afterBuy.Instrument.Position.CostBasisNative);
        Assert.Equal(105m, afterBuy.Instrument.Position.AverageCostNative);

        await client.PostAsJsonAsync($"/api/investments/instruments/{created.Instrument.Id}/transactions", buy);
        var history = await client.GetFromJsonAsync<List<InvestmentTransactionDto>>(
            $"/api/investments/instruments/{created.Instrument.Id}/transactions");
        Assert.Equal(2, history!.Count);

        var invalidSellResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}/transactions",
            new CreateInvestmentTransactionRequest
            {
                TransactionType = "SELL",
                TransactionDate = new DateOnly(2026, 3, 1),
                Quantity = 3m,
                UnitPrice = 130m,
                Currency = "USD",
                CurrencyPerEurRate = 1.25m,
                IdempotencyKey = "ko-invalid-sell"
            });
        Assert.Equal(HttpStatusCode.Conflict, invalidSellResponse.StatusCode);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/investments/instruments/{created.Instrument.Id}",
            new UpdateInvestmentInstrumentRequest
            {
                ExpectedVersion = created.Instrument.Version,
                AssetClass = "STOCKS",
                Kind = "STOCK",
                Name = "Coca-Cola",
                Ticker = "KO",
                Mic = "XNYS",
                NativeCurrency = "USD",
                ValuationMode = "MARKET_QUOTE",
                AllocationScore = 11,
                QuantityStep = 0.000001m
            });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    [Fact]
    public async Task ManualQuoteCorrection_ShouldAppendSnapshotAndUseLatestQuoteFromSameDay()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);
        var today = MarketToday(factory);

        var instrument = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "STOCKS",
            name: "Correction test stock",
            ticker: "FIX",
            currency: "USD",
            quantity: 2m);
        await SeedFxRatesAsync(factory, today, ("USD", 1.25m));

        var firstResponse = await RecordManualQuoteAsync(client, instrument.Instrument.Id, 100m, "USD", today);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        await Task.Delay(2);
        var correctionResponse = await RecordManualQuoteAsync(client, instrument.Instrument.Id, 125m, "USD", today);
        Assert.Equal(HttpStatusCode.NoContent, correctionResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CostTrackerDbContext>();
            var immutableHistory = await dbContext.MarketQuoteSnapshots
                .Where(item => item.InstrumentId == instrument.Instrument.Id)
                .OrderBy(item => item.FetchedAt)
                .ToListAsync();

            Assert.Equal(2, immutableHistory.Count);
            Assert.Equal([100m, 125m], immutableHistory.Select(item => item.Price));
        }

        var valuation = await client.GetFromJsonAsync<ValuedPortfolioDto>("/api/investments/portfolio/valuation");
        var position = Assert.Single(valuation!.Positions);
        Assert.Equal(125m, position.CurrentPrice);
        Assert.Equal(250m, position.NativeValue);
        Assert.Equal(200m, position.ValueEur);
        Assert.Equal(MarketDataProviderCodes.Manual, position.MarketData!.Source);
        Assert.True(position.MarketData.IsFallback);
        Assert.False(valuation.Summary.IsPartial);
    }

    [Fact]
    public async Task Valuation_ShouldRemainPartialAndExposeKnownTotalWhenQuoteOrFxIsMissing()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);
        var today = MarketToday(factory);

        var quotedWithoutFx = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "STOCKS",
            name: "Quoted without FX",
            ticker: "NOFX",
            currency: "USD",
            quantity: 1m);
        var missingQuote = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "REITS",
            name: "Missing quote",
            ticker: "NOQUOTE",
            currency: "USD",
            quantity: 1m);
        await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "INTERNATIONAL_FIXED_INCOME",
            name: "Known EUR balance",
            currency: "EUR",
            amount: 300m);
        var quoteResponse = await RecordManualQuoteAsync(
            client,
            quotedWithoutFx.Instrument.Id,
            100m,
            "USD",
            today);
        Assert.Equal(HttpStatusCode.NoContent, quoteResponse.StatusCode);

        var valuation = await client.GetFromJsonAsync<ValuedPortfolioDto>("/api/investments/portfolio/valuation");
        Assert.NotNull(valuation);
        Assert.True(valuation.Summary.IsPartial);
        Assert.Equal(300m, valuation.Summary.TotalValueEur);
        Assert.Equal(DataFreshnessCodes.Missing, valuation.Summary.Freshness);

        var noFxPosition = Assert.Single(valuation.Positions, item => item.InstrumentId == quotedWithoutFx.Instrument.Id);
        Assert.Equal(100m, noFxPosition.CurrentPrice);
        Assert.Equal(100m, noFxPosition.NativeValue);
        Assert.Null(noFxPosition.ValueEur);
        Assert.Null(noFxPosition.FxData);
        Assert.Null(noFxPosition.PortfolioWeight);

        var noQuotePosition = Assert.Single(valuation.Positions, item => item.InstrumentId == missingQuote.Instrument.Id);
        Assert.Null(noQuotePosition.CurrentPrice);
        Assert.Null(noQuotePosition.NativeValue);
        Assert.Null(noQuotePosition.ValueEur);
        Assert.Null(noQuotePosition.PortfolioWeight);
    }

    [Fact]
    public async Task Valuation_ShouldConvertEurUsdBrlAndGbxUsingCurrencyPerEurRates()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);
        var today = MarketToday(factory);

        await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "INTERNATIONAL_FIXED_INCOME",
            name: "EUR account",
            currency: "EUR",
            amount: 300m);
        await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "BRAZIL_FIXED_INCOME",
            name: "BRL bond",
            currency: "BRL",
            amount: 600m);
        var usdStock = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "STOCKS",
            name: "USD stock",
            ticker: "USD.TEST",
            currency: "USD",
            quantity: 2m);
        var gbxReit = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "REITS",
            name: "London REIT",
            ticker: "GBX.L",
            currency: "GBX",
            quantity: 10m);

        await SeedFxRatesAsync(factory, today, ("USD", 1.2m), ("BRL", 6m), ("GBX", 85m));
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await RecordManualQuoteAsync(client, usdStock.Instrument.Id, 120m, "USD", today)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await RecordManualQuoteAsync(client, gbxReit.Instrument.Id, 1_050m, "GBX", today)).StatusCode);

        var valuation = await client.GetFromJsonAsync<ValuedPortfolioDto>("/api/investments/portfolio/valuation");
        Assert.NotNull(valuation);
        Assert.False(valuation.Summary.IsPartial);
        Assert.Equal(today, valuation.Summary.AsOf);

        Assert.Equal(300m, Assert.Single(valuation.Positions, item => item.NativeCurrency == "EUR").ValueEur);
        Assert.Equal(100m, Assert.Single(valuation.Positions, item => item.NativeCurrency == "BRL").ValueEur);
        Assert.Equal(200m, Assert.Single(valuation.Positions, item => item.NativeCurrency == "USD").ValueEur);
        Assert.Equal(10_500m / 85m, Assert.Single(valuation.Positions, item => item.NativeCurrency == "GBX").ValueEur);
        Assert.Equal(600m + (10_500m / 85m), valuation.Summary.TotalValueEur);
    }

    [Fact]
    public async Task FutureDatedInputs_ShouldBeRejectedAndPersistedFutureSnapshotsIgnored()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);
        var today = MarketToday(factory);
        var future = today.AddDays(1);

        var stock = await CreateMarketInstrumentAsync(
            client,
            today,
            assetClass: "STOCKS",
            name: "Future guard stock",
            ticker: "FUTURE",
            currency: "USD",
            quantity: 1m);
        var manual = await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "BRAZIL_FIXED_INCOME",
            name: "Future guard balance",
            currency: "BRL",
            amount: 600m);

        var futureQuoteResponse = await RecordManualQuoteAsync(
            client,
            stock.Instrument.Id,
            999m,
            "USD",
            future);
        Assert.Equal(HttpStatusCode.BadRequest, futureQuoteResponse.StatusCode);

        var futureTransactionResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{stock.Instrument.Id}/transactions",
            new CreateInvestmentTransactionRequest
            {
                TransactionType = "BUY",
                TransactionDate = future,
                Quantity = 1m,
                UnitPrice = 999m,
                Currency = "USD",
                CurrencyPerEurRate = 9m,
                IdempotencyKey = "future-buy"
            });
        Assert.Equal(HttpStatusCode.BadRequest, futureTransactionResponse.StatusCode);

        var futureManualValuationResponse = await client.PostAsJsonAsync(
            $"/api/investments/instruments/{manual.Instrument.Id}/manual-valuations",
            new CreateManualValuationRequest
            {
                Amount = 9_999m,
                Currency = "BRL",
                AsOf = future,
                IdempotencyKey = "future-manual-value"
            });
        Assert.Equal(HttpStatusCode.BadRequest, futureManualValuationResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await RecordManualQuoteAsync(client, stock.Instrument.Id, 100m, "USD", today)).StatusCode);
        await SeedFxRatesAsync(factory, today, ("USD", 1.25m), ("BRL", 6m));
        await SeedFutureSnapshotsAsync(factory, stock.Instrument.Id, future);

        var valuation = await client.GetFromJsonAsync<ValuedPortfolioDto>("/api/investments/portfolio/valuation");
        Assert.NotNull(valuation);
        Assert.False(valuation.Summary.IsPartial);
        var stockPosition = Assert.Single(valuation.Positions, item => item.InstrumentId == stock.Instrument.Id);
        Assert.Equal(100m, stockPosition.CurrentPrice);
        Assert.Equal(80m, stockPosition.ValueEur);
        Assert.Equal(today, stockPosition.MarketData!.AsOf);
        Assert.Equal(today, stockPosition.FxData!.AsOf);
        Assert.Equal(180m, valuation.Summary.TotalValueEur);
    }

    [Fact]
    public async Task EmptyMarketInstrumentWithZeroScore_ShouldNotRequireQuoteOrFxOrBlockPlanning()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await LoginAndConfigureAsync(client);
        var today = MarketToday(factory);

        var disabledResponse = await client.PostAsJsonAsync(
            "/api/investments/instruments",
            new CreateInvestmentInstrumentRequest
            {
                AssetClass = "STOCKS",
                Kind = "STOCK",
                Name = "Disabled empty stock",
                Ticker = "DISABLED",
                NativeCurrency = "USD",
                ValuationMode = "MARKET_QUOTE",
                AllocationScore = 0,
                QuantityStep = 0.000001m
            });
        Assert.Equal(HttpStatusCode.Created, disabledResponse.StatusCode);

        await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "BRAZIL_FIXED_INCOME",
            name: "BRL destination",
            currency: "BRL",
            amount: 600m);
        await CreateManualInstrumentAsync(
            client,
            today,
            assetClass: "INTERNATIONAL_FIXED_INCOME",
            name: "EUR destination",
            currency: "EUR",
            amount: 100m);
        await SeedFxRatesAsync(factory, today, ("BRL", 6m));

        var status = await client.GetFromJsonAsync<MarketDataStatusDto>(
            "/api/investments/market-data/status");
        Assert.Equal(DataFreshnessCodes.Fresh, status!.Freshness);
        Assert.Empty(status.MissingInstrumentIds);

        var valuation = await client.GetFromJsonAsync<ValuedPortfolioDto>(
            "/api/investments/portfolio/valuation");
        Assert.NotNull(valuation);
        Assert.False(valuation.Summary.IsPartial);
        var disabled = Assert.Single(valuation.Positions, item => item.Ticker == "DISABLED");
        Assert.Equal(0m, disabled.NativeValue);
        Assert.Equal(0m, disabled.ValueEur);
        Assert.Null(disabled.MarketData);
        Assert.Null(disabled.FxData);

        var planResponse = await client.PostAsJsonAsync(
            "/api/investments/contribution-plans",
            new CreateContributionPlanRequest
            {
                ContributionAmountEur = 100m,
                AllowStaleData = false
            });
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
        var plan = await planResponse.Content.ReadFromJsonAsync<ContributionPlanDto>();
        Assert.NotNull(plan);
        Assert.DoesNotContain(plan.Lines, line => line.InstrumentId == disabled.InstrumentId);
    }

    private static async Task<InvestmentInstrumentDetailDto> CreateMarketInstrumentAsync(
        HttpClient client,
        DateOnly today,
        string assetClass,
        string name,
        string ticker,
        string currency,
        decimal quantity)
    {
        var response = await client.PostAsJsonAsync("/api/investments/instruments", new CreateInvestmentInstrumentRequest
        {
            AssetClass = assetClass,
            Kind = assetClass == "REITS" ? "REIT" : "STOCK",
            Name = name,
            Ticker = ticker,
            NativeCurrency = currency,
            ValuationMode = "MARKET_QUOTE",
            AllocationScore = 10,
            QuantityStep = 0.000001m,
            OpeningTransaction = new CreateInvestmentTransactionRequest
            {
                TransactionType = "OPENING_BALANCE",
                TransactionDate = today,
                Quantity = quantity,
                UnitPrice = 1m,
                Currency = currency,
                CurrencyPerEurRate = currency == "EUR" ? 1m : 1.2m,
                IdempotencyKey = $"opening-{ticker}"
            }
        });
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>())!;
    }

    private static async Task<InvestmentInstrumentDetailDto> CreateManualInstrumentAsync(
        HttpClient client,
        DateOnly today,
        string assetClass,
        string name,
        string currency,
        decimal amount)
    {
        var response = await client.PostAsJsonAsync("/api/investments/instruments", new CreateInvestmentInstrumentRequest
        {
            AssetClass = assetClass,
            Kind = "ACCOUNT",
            Name = name,
            NativeCurrency = currency,
            ValuationMode = "MANUAL",
            AllocationScore = 0,
            ManualValuation = new CreateManualValuationRequest
            {
                Amount = amount,
                Currency = currency,
                AsOf = today,
                IdempotencyKey = $"opening-{name.Replace(' ', '-').ToLowerInvariant()}"
            }
        });
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<InvestmentInstrumentDetailDto>())!;
    }

    private static Task<HttpResponseMessage> RecordManualQuoteAsync(
        HttpClient client,
        Guid instrumentId,
        decimal price,
        string currency,
        DateOnly asOf)
        => client.PostAsJsonAsync(
            $"/api/investments/market-data/instruments/{instrumentId}/manual-quote",
            new ManualMarketQuoteRequest(price, currency, asOf, null, null, null));

    private static async Task SeedFxRatesAsync(
        TestWebApplicationFactory factory,
        DateOnly asOf,
        params (string Currency, decimal Rate)[] rates)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CostTrackerDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        foreach (var (currency, rate) in rates)
        {
            dbContext.FxRateSnapshots.Add(new FxRateSnapshot
            {
                Id = Guid.NewGuid(),
                ProviderCode = "TEST_FX",
                BaseCurrency = CurrencyCode.Eur,
                QuoteCurrency = new CurrencyCode(currency),
                Rate = rate,
                RateKind = "REFERENCE",
                AsOf = asOf,
                FetchedAt = now,
                RawPayloadHash = Guid.NewGuid().ToString("N")
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedFutureSnapshotsAsync(
        TestWebApplicationFactory factory,
        Guid instrumentId,
        DateOnly future)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CostTrackerDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        dbContext.MarketQuoteSnapshots.Add(new MarketQuoteSnapshot
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            ProviderCode = "TEST_FUTURE",
            ProviderSymbol = "FUTURE",
            Price = 999m,
            Currency = new CurrencyCode("USD"),
            PriceKind = "EOD_CLOSE",
            AsOf = future,
            FetchedAt = now.AddMinutes(1),
            RawPayloadHash = Guid.NewGuid().ToString("N")
        });
        dbContext.FxRateSnapshots.Add(new FxRateSnapshot
        {
            Id = Guid.NewGuid(),
            ProviderCode = "TEST_FUTURE",
            BaseCurrency = CurrencyCode.Eur,
            QuoteCurrency = new CurrencyCode("USD"),
            Rate = 9m,
            RateKind = "REFERENCE",
            AsOf = future,
            FetchedAt = now.AddMinutes(1),
            RawPayloadHash = Guid.NewGuid().ToString("N")
        });
        await dbContext.SaveChangesAsync();
    }

    private static DateOnly MarketToday(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<MarketDataOptions>>().Value;
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.RefreshTimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    }

    private static UpdateInvestmentAllocationRequest AllocationRequest(long? expectedVersion = null)
        => new()
        {
            ExpectedVersion = expectedVersion,
            Items =
            [
                new() { AssetClass = "STOCKS", Weight = 0.40m },
                new() { AssetClass = "REITS", Weight = 0.10m },
                new() { AssetClass = "BRAZIL_FIXED_INCOME", Weight = 0.30m },
                new() { AssetClass = "INTERNATIONAL_FIXED_INCOME", Weight = 0.20m }
            ]
        };

    private static async Task LoginAndConfigureAsync(HttpClient client)
    {
        await LoginAsync(client);
        var portfolio = await client.GetFromJsonAsync<InvestmentPortfolioDto>("/api/investments/portfolio");
        var response = await client.PutAsJsonAsync("/api/investments/allocation", AllocationRequest(portfolio!.Version));
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = TestWebApplicationFactory.TestUsername,
            Password = TestWebApplicationFactory.TestPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

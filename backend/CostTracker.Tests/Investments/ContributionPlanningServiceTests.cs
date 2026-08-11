using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Investments.Contributions;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Application.Services;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using CostTracker.Infrastructure.Persistence.Configurations.Investments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Investments;

public sealed class ContributionPlanningServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_and_confirm_market_plan_is_auditable_and_idempotent()
    {
        await using var db = CreateDbContext();
        var portfolio = CreateConfiguredPortfolio(AssetClass.Stocks);
        var stock = CreateMarketInstrument(portfolio);
        stock.Transactions.Add(new InvestmentTransaction
        {
            Id = Guid.NewGuid(),
            InstrumentId = stock.Id,
            Type = InvestmentTransactionType.OpeningBalance,
            TransactionDate = new DateOnly(2026, 8, 1),
            Quantity = 1m,
            UnitPrice = 90m,
            Currency = new CurrencyCode("USD"),
            CurrencyPerEurRate = 1.2m,
            IdempotencyKey = "opening",
            CreatedAt = Now.AddDays(-10)
        });
        portfolio.Instruments.Add(stock);
        db.InvestmentPortfolios.Add(portfolio);
        db.MarketQuoteSnapshots.Add(new MarketQuoteSnapshot
        {
            Id = Guid.NewGuid(),
            InstrumentId = stock.Id,
            ProviderCode = "TEST",
            ProviderSymbol = "KO",
            Mic = "XNYS",
            Price = 100m,
            Currency = new CurrencyCode("USD"),
            AsOf = new DateOnly(2026, 8, 11),
            FetchedAt = Now,
            RawPayloadHash = "quote"
        });
        db.FxRateSnapshots.Add(new FxRateSnapshot
        {
            Id = Guid.NewGuid(),
            ProviderCode = "TEST",
            BaseCurrency = CurrencyCode.Eur,
            QuoteCurrency = new CurrencyCode("USD"),
            Rate = 1.2m,
            RateKind = "REFERENCE",
            AsOf = new DateOnly(2026, 8, 11),
            FetchedAt = Now,
            RawPayloadHash = "fx"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var preview = await service.CreatePlanAsync(
            new CreateContributionPlanRequest { ContributionAmountEur = 100m },
            CancellationToken.None);

        Assert.Equal("DRAFT", preview.Status);
        Assert.Equal(100m, preview.TotalSuggestedEur);
        Assert.Equal(0m, preview.ResidualAmountEur);
        var line = Assert.Single(preview.Lines);
        Assert.Equal(stock.Id, line.InstrumentId);
        Assert.Equal(1.2m, line.SuggestedQuantity);
        Assert.Equal(new DateOnly(2026, 8, 11), line.QuoteAsOf);

        var request = new ConfirmContributionPlanRequest
        {
            IdempotencyKey = "confirm-once",
            Executions =
            [
                new ContributionExecutionLineRequest
                {
                    PlanLineId = line.Id,
                    InstrumentId = stock.Id,
                    OccurredOn = new DateOnly(2026, 8, 11),
                    ActualAmountEur = 100m,
                    ActualQuantity = 1.2m,
                    ActualUnitPrice = 100m,
                    Currency = "USD"
                }
            ]
        };

        var confirmed = await service.ConfirmPlanAsync(preview.Id, request, CancellationToken.None);
        var retried = await service.ConfirmPlanAsync(preview.Id, request, CancellationToken.None);

        Assert.Equal("CONFIRMED", confirmed.Status);
        Assert.Equal(confirmed.Id, retried.Id);
        Assert.Equal("CONFIRMED", retried.Status);
        Assert.Equal(confirmed.Lines.Select(item => item.Id), retried.Lines.Select(item => item.Id));
        Assert.Equal(2, await db.InvestmentTransactions.CountAsync());
        var purchase = await db.InvestmentTransactions.SingleAsync(item => item.Type == InvestmentTransactionType.Buy);
        Assert.Equal(1.2m, purchase.CurrencyPerEurRate);
        Assert.Equal(preview.PortfolioVersion + 1, (await db.InvestmentPortfolios.SingleAsync()).Version);
    }

    [Fact]
    public async Task Confirm_fixed_income_plan_creates_deposit_and_new_manual_balance_snapshot()
    {
        await using var db = CreateDbContext();
        var portfolio = CreateConfiguredPortfolio(AssetClass.InternationalFixedIncome);
        var account = CreateManualInstrument(portfolio);
        account.ManualValuations.Add(new ManualValuation
        {
            Id = Guid.NewGuid(),
            InstrumentId = account.Id,
            Amount = 500m,
            Currency = CurrencyCode.Eur,
            AsOf = new DateOnly(2026, 8, 11),
            RecordedAt = Now,
            IdempotencyKey = "balance"
        });
        portfolio.Instruments.Add(account);
        db.InvestmentPortfolios.Add(portfolio);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var preview = await service.CreatePlanAsync(
            new CreateContributionPlanRequest { ContributionAmountEur = 100m },
            CancellationToken.None);
        var line = Assert.Single(preview.Lines);

        var confirmed = await service.ConfirmPlanAsync(
            preview.Id,
            new ConfirmContributionPlanRequest
            {
                IdempotencyKey = "fixed-confirm",
                Executions =
                [
                    new ContributionExecutionLineRequest
                    {
                        PlanLineId = line.Id,
                        OccurredOn = new DateOnly(2026, 8, 11),
                        ActualAmountEur = 100m,
                        ActualNativeAmount = 100m,
                        Currency = "EUR"
                    }
                ]
            },
            CancellationToken.None);

        Assert.Equal("CONFIRMED", confirmed.Status);
        var deposit = await db.InvestmentTransactions.SingleAsync();
        Assert.Equal(InvestmentTransactionType.Deposit, deposit.Type);
        Assert.Equal(100m, deposit.Amount);
        var balances = await db.ManualValuations.OrderBy(item => item.RecordedAt).ToListAsync();
        Assert.Equal(2, balances.Count);
        Assert.Equal(600m, balances[^1].Amount);
    }

    [Fact]
    public async Task Cryptocurrency_target_stays_as_residual_without_requiring_a_destination_or_plan_line()
    {
        await using var db = CreateDbContext();
        var portfolio = CreateConfiguredPortfolio(AssetClass.Cryptocurrencies);
        var account = CreateManualInstrument(portfolio);
        account.ManualValuations.Add(new ManualValuation
        {
            Id = Guid.NewGuid(),
            InstrumentId = account.Id,
            Amount = 500m,
            Currency = CurrencyCode.Eur,
            AsOf = new DateOnly(2026, 8, 11),
            RecordedAt = Now,
            IdempotencyKey = "crypto-residual-balance"
        });
        portfolio.Instruments.Add(account);
        db.InvestmentPortfolios.Add(portfolio);
        await db.SaveChangesAsync();

        var preview = await CreateService(db).CreatePlanAsync(
            new CreateContributionPlanRequest { ContributionAmountEur = 100m },
            CancellationToken.None);

        Assert.Empty(preview.Lines);
        Assert.Equal(0m, preview.TotalSuggestedEur);
        Assert.Equal(100m, preview.ResidualAmountEur);
    }

    [Fact]
    public async Task Stale_data_requires_an_explicit_audited_override()
    {
        await using var db = CreateDbContext();
        var portfolio = CreateConfiguredPortfolio(AssetClass.Stocks);
        var stock = CreateMarketInstrument(portfolio);
        portfolio.Instruments.Add(stock);
        db.InvestmentPortfolios.Add(portfolio);
        db.MarketQuoteSnapshots.Add(new MarketQuoteSnapshot
        {
            Id = Guid.NewGuid(),
            InstrumentId = stock.Id,
            ProviderCode = "TEST",
            ProviderSymbol = "KO",
            Price = 100m,
            Currency = new CurrencyCode("USD"),
            AsOf = new DateOnly(2026, 8, 7),
            FetchedAt = Now.AddDays(-4),
            RawPayloadHash = "quote"
        });
        db.FxRateSnapshots.Add(new FxRateSnapshot
        {
            Id = Guid.NewGuid(),
            ProviderCode = "TEST",
            BaseCurrency = CurrencyCode.Eur,
            QuoteCurrency = new CurrencyCode("USD"),
            Rate = 1.2m,
            RateKind = "REFERENCE",
            AsOf = new DateOnly(2026, 8, 7),
            FetchedAt = Now.AddDays(-4),
            RawPayloadHash = "fx"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, quoteBlockingSessions: 5);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreatePlanAsync(
            new CreateContributionPlanRequest { ContributionAmountEur = 100m },
            CancellationToken.None));

        var preview = await service.CreatePlanAsync(
            new CreateContributionPlanRequest
            {
                ContributionAmountEur = 100m,
                AllowStaleData = true
            },
            CancellationToken.None);

        Assert.All(preview.Lines, item => Assert.Equal("STALE", item.Freshness));
        Assert.True((await db.ContributionPlans.SingleAsync()).AllowedStaleData);
    }

    private static ContributionPlanningService CreateService(
        ContributionTestDbContext db,
        int quoteBlockingSessions = 2)
        => new(
            db,
            new PortfolioProjectionService(),
            new FixedTimeProvider(Now),
            Options.Create(new MarketDataOptions
            {
                RefreshTimeZone = "Europe/Lisbon",
                QuoteWarningSessions = 1,
                QuoteBlockingSessions = quoteBlockingSessions,
                ManualValuationWarningDays = 7,
                ManualValuationBlockingDays = 31
            }));

    private static ContributionTestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ContributionTestDbContext>()
            .UseInMemoryDatabase($"contribution-tests-{Guid.NewGuid():N}")
            .Options;
        return new ContributionTestDbContext(options);
    }

    private static InvestmentPortfolio CreateConfiguredPortfolio(AssetClass fullyTargetedClass)
    {
        var portfolio = new InvestmentPortfolio
        {
            Id = Guid.NewGuid(),
            SingletonKey = 1,
            BaseCurrency = CurrencyCode.Eur,
            Version = 1,
            CreatedAt = Now.AddDays(-30),
            UpdatedAt = Now.AddDays(-1)
        };

        foreach (var assetClass in Enum.GetValues<AssetClass>())
        {
            portfolio.AllocationTargets.Add(new AllocationTarget
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                AssetClass = assetClass,
                Weight = assetClass == fullyTargetedClass ? 1m : 0m
            });
        }

        return portfolio;
    }

    private static InvestmentInstrument CreateMarketInstrument(InvestmentPortfolio portfolio)
        => new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            Portfolio = portfolio,
            AssetClass = AssetClass.Stocks,
            Kind = InstrumentKind.Stock,
            Name = "Coca-Cola",
            Ticker = "KO",
            Mic = "XNYS",
            IdentityKey = "TICKER:KO@XNYS",
            NativeCurrency = new CurrencyCode("USD"),
            ValuationMode = ValuationMode.MarketQuote,
            AllocationScore = 10,
            QuantityStep = 0.1m,
            Version = 1,
            CreatedAt = Now.AddDays(-30),
            UpdatedAt = Now.AddDays(-1)
        };

    private static InvestmentInstrument CreateManualInstrument(InvestmentPortfolio portfolio)
        => new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            Portfolio = portfolio,
            AssetClass = AssetClass.InternationalFixedIncome,
            Kind = InstrumentKind.Account,
            Name = "EUR savings",
            IdentityKey = "MANUAL:EUR-SAVINGS",
            NativeCurrency = CurrencyCode.Eur,
            ValuationMode = ValuationMode.Manual,
            AllocationScore = 0,
            Version = 1,
            CreatedAt = Now.AddDays(-30),
            UpdatedAt = Now.AddDays(-1)
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ContributionTestDbContext(DbContextOptions<ContributionTestDbContext> options)
        : DbContext(options), IContributionPlanningDbContext
    {
        public DbSet<InvestmentPortfolio> InvestmentPortfolios => Set<InvestmentPortfolio>();
        public DbSet<InvestmentInstrument> InvestmentInstruments => Set<InvestmentInstrument>();
        public DbSet<InvestmentTransaction> InvestmentTransactions => Set<InvestmentTransaction>();
        public DbSet<ManualValuation> ManualValuations => Set<ManualValuation>();
        public DbSet<MarketQuoteSnapshot> MarketQuoteSnapshots => Set<MarketQuoteSnapshot>();
        public DbSet<FxRateSnapshot> FxRateSnapshots => Set<FxRateSnapshot>();
        public DbSet<ContributionPlan> ContributionPlans => Set<ContributionPlan>();
        public DbSet<ContributionPlanLine> ContributionPlanLines => Set<ContributionPlanLine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContributionPlanConfiguration).Assembly);
        }
    }
}

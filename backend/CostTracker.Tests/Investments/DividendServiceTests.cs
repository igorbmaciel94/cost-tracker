using CostTracker.Application.Investments.Dividends;
using CostTracker.Application.Options;
using CostTracker.Application.Services;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using CostTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Investments;

public sealed class DividendServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ExDate = new(2026, 8, 14);
    private static readonly DateOnly PaymentDate = new(2026, 8, 18);

    [Fact]
    public async Task ProcessDue_ShouldCreditOnlyQuantityOwnedBeforeExDate_Once()
    {
        await using var db = CreateDbContext();
        var instrument = SeedInstrument(db);
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.OpeningBalance, new DateOnly(2026, 8, 1), 10m));
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Buy, new DateOnly(2026, 8, 10), 5m));
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Sell, new DateOnly(2026, 8, 12), 2m));
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Buy, ExDate, 100m));
        db.FxRateSnapshots.Add(CreateUsdRate(PaymentDate, 1.2m));
        await db.SaveChangesAsync();
        var scheduler = new CapturingScheduler();
        var service = CreateService(db, scheduler);

        var created = await service.CreateEventAsync(instrument.Id, new CreateDividendEventRequest
        {
            GrossAmountPerUnit = 1.2m,
            WithholdingTaxPercent = 15m,
            Currency = "USD",
            ExDate = ExDate,
            PaymentDate = PaymentDate,
            IdempotencyKey = "dividend-once"
        });
        var first = await service.ProcessDueAsync(PaymentDate);
        var second = await service.ProcessDueAsync(PaymentDate);
        var credited = Assert.Single(await service.GetEventsAsync(instrument.Id));

        Assert.True(scheduler.WasRequested);
        Assert.Equal(DividendEventStatusCodes.Due, created.Status);
        Assert.Equal(1, first.ProcessedCount);
        Assert.Equal(0, second.ProcessedCount);
        Assert.Equal(DividendEventStatusCodes.Credited, credited.Status);
        Assert.Equal(13m, credited.EligibleQuantity);
        Assert.Equal(15.6m, credited.GrossAmount);
        Assert.Equal(2.34m, credited.WithholdingTaxAmount);
        Assert.Equal(13.26m, credited.NetAmount);
        Assert.Equal(11.05m, credited.NetAmountEur);
    }

    [Fact]
    public async Task ProcessDue_ShouldLeaveFuturePaymentScheduled()
    {
        await using var db = CreateDbContext();
        var instrument = SeedInstrument(db);
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Buy, new DateOnly(2026, 8, 1), 3m));
        await db.SaveChangesAsync();
        var service = CreateService(db, new CapturingScheduler());
        await service.CreateEventAsync(instrument.Id, new CreateDividendEventRequest
        {
            GrossAmountPerUnit = 2m,
            Currency = "EUR",
            ExDate = ExDate,
            PaymentDate = PaymentDate.AddDays(1),
            IdempotencyKey = "future-dividend"
        });

        var result = await service.ProcessDueAsync(PaymentDate);
        var dividend = Assert.Single(await service.GetEventsAsync(instrument.Id));

        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(DividendEventStatusCodes.Scheduled, dividend.Status);
        Assert.Null(dividend.ProcessedAt);
    }

    [Fact]
    public async Task CashSummary_ShouldGroupCreditedAmountsByCurrency()
    {
        await using var db = CreateDbContext();
        var instrument = SeedInstrument(db);
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Buy, new DateOnly(2026, 8, 1), 2m));
        db.FxRateSnapshots.Add(CreateUsdRate(PaymentDate, 1.25m));
        await db.SaveChangesAsync();
        var service = CreateService(db, new CapturingScheduler());
        await service.CreateEventAsync(instrument.Id, new CreateDividendEventRequest
        {
            GrossAmountPerUnit = 5m,
            Currency = "USD",
            ExDate = ExDate,
            PaymentDate = PaymentDate,
            IdempotencyKey = "cash-usd"
        });
        await service.CreateEventAsync(instrument.Id, new CreateDividendEventRequest
        {
            GrossAmountPerUnit = 3m,
            Currency = "EUR",
            ExDate = ExDate,
            PaymentDate = PaymentDate,
            IdempotencyKey = "cash-eur"
        });
        await service.ProcessDueAsync(PaymentDate);

        var cash = await service.GetCashSummaryAsync();

        Assert.False(cash.IsPartial);
        Assert.Equal(14m, cash.TotalEur);
        Assert.Equal(2, cash.Balances.Count);
        Assert.Equal(6m, cash.Balances.Single(item => item.Currency == "EUR").Amount);
        var usd = cash.Balances.Single(item => item.Currency == "USD");
        Assert.Equal(10m, usd.Amount);
        Assert.Equal(8m, usd.AmountEur);
        Assert.Equal("TEST_FX", usd.FxData?.Source);
    }

    [Fact]
    public async Task ProcessDue_ShouldCreditNativeCashWhenExchangeRateIsMissing()
    {
        await using var db = CreateDbContext();
        var instrument = SeedInstrument(db);
        instrument.Transactions.Add(CreateTransaction(instrument, InvestmentTransactionType.Buy, new DateOnly(2026, 8, 1), 4m));
        await db.SaveChangesAsync();
        var service = CreateService(db, new CapturingScheduler());
        await service.CreateEventAsync(instrument.Id, new CreateDividendEventRequest
        {
            GrossAmountPerUnit = 2m,
            Currency = "USD",
            ExDate = ExDate,
            PaymentDate = PaymentDate,
            IdempotencyKey = "cash-without-fx"
        });

        var result = await service.ProcessDueAsync(PaymentDate);
        var credited = Assert.Single(await service.GetEventsAsync(instrument.Id));
        var cash = await service.GetCashSummaryAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.MissingFxCount);
        Assert.Equal(DividendEventStatusCodes.Credited, credited.Status);
        Assert.Equal(8m, credited.NetAmount);
        Assert.Null(credited.NetAmountEur);
        Assert.True(cash.IsPartial);
        Assert.Equal(8m, Assert.Single(cash.Balances).Amount);
    }

    private static CostTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CostTrackerDbContext>()
            .UseInMemoryDatabase($"dividends-{Guid.NewGuid():N}")
            .Options;
        return new CostTrackerDbContext(options);
    }

    private static DividendService CreateService(CostTrackerDbContext db, IDividendProcessingScheduler scheduler)
        => new(
            db,
            scheduler,
            Options.Create(new DividendOptions { ProcessingTimeZone = "Europe/Lisbon" }),
            new FixedTimeProvider(Now));

    private static InvestmentInstrument SeedInstrument(CostTrackerDbContext db)
    {
        var portfolio = InvestmentPortfolio.Create(Now);
        var instrument = new InvestmentInstrument
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            Portfolio = portfolio,
            AssetClass = AssetClass.Stocks,
            Kind = InstrumentKind.Stock,
            Name = "Example Inc.",
            Ticker = "EXM",
            Mic = "XNYS",
            IdentityKey = "TICKER:XNYS:EXM",
            NativeCurrency = new CurrencyCode("USD"),
            ValuationMode = ValuationMode.MarketQuote,
            AllocationScore = 10,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        portfolio.Instruments.Add(instrument);
        db.InvestmentPortfolios.Add(portfolio);
        return instrument;
    }

    private static InvestmentTransaction CreateTransaction(
        InvestmentInstrument instrument,
        InvestmentTransactionType type,
        DateOnly date,
        decimal quantity)
        => new()
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Instrument = instrument,
            Type = type,
            TransactionDate = date,
            Quantity = quantity,
            Currency = instrument.NativeCurrency,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAt = Now
        };

    private static FxRateSnapshot CreateUsdRate(DateOnly asOf, decimal rate)
        => new()
        {
            Id = Guid.NewGuid(),
            ProviderCode = "TEST_FX",
            BaseCurrency = CurrencyCode.Eur,
            QuoteCurrency = new CurrencyCode("USD"),
            Rate = rate,
            RateKind = "REFERENCE",
            AsOf = asOf,
            FetchedAt = Now,
            RawPayloadHash = new string('a', 64)
        };

    private sealed class CapturingScheduler : IDividendProcessingScheduler
    {
        public bool WasRequested { get; private set; }
        public void RequestProcessing() => WasRequested = true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

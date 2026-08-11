using CostTracker.Application.Projections;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;

namespace CostTracker.Tests.Investments;

public class PortfolioProjectionServiceTests
{
    private readonly PortfolioProjectionService _service = new();

    [Fact]
    public void CalculatePosition_ShouldDeriveQuantityAndAverageCostFromHistory()
    {
        var instrument = MarketInstrument();
        instrument.Transactions =
        [
            MarketTransaction(InvestmentTransactionType.OpeningBalance, new DateOnly(2026, 1, 1), 1.5m, 100m, 1.2m),
            MarketTransaction(InvestmentTransactionType.Buy, new DateOnly(2026, 2, 1), 0.5m, 120m, 1.2m),
            MarketTransaction(InvestmentTransactionType.Sell, new DateOnly(2026, 3, 1), 0.25m, 130m, 1.25m)
        ];

        var result = _service.CalculatePosition(instrument);

        Assert.Equal(1.75m, result.Quantity);
        Assert.True(result.IsCostKnown);
        Assert.Equal(183.75m, result.CostBasisNative);
        Assert.Equal(105m, result.AverageCostNative);
        Assert.Equal(177.5m, result.NetInvestedNative);
        Assert.Equal(149m, result.NetInvestedEur);
    }

    [Fact]
    public void CalculatePosition_ShouldExposeUnknownCostWhenOpeningCostIsMissing()
    {
        var instrument = MarketInstrument();
        instrument.Transactions =
        [
            new InvestmentTransaction
            {
                Type = InvestmentTransactionType.OpeningBalance,
                TransactionDate = new DateOnly(2026, 1, 1),
                Quantity = 2m,
                Currency = new CurrencyCode("USD")
            }
        ];

        var result = _service.CalculatePosition(instrument);

        Assert.Equal(2m, result.Quantity);
        Assert.False(result.IsCostKnown);
        Assert.Null(result.CostBasisNative);
        Assert.Null(result.AverageCostNative);
        Assert.Null(result.NetInvestedNative);
        Assert.Null(result.NetInvestedEur);
    }

    [Fact]
    public void CalculatePosition_ShouldEstimateLatestManualValueWithLaterCashFlows()
    {
        var instrument = new InvestmentInstrument
        {
            NativeCurrency = new CurrencyCode("BRL"),
            ValuationMode = ValuationMode.Manual,
            ManualValuations =
            [
                new ManualValuation
                {
                    Amount = 10_000m,
                    Currency = new CurrencyCode("BRL"),
                    AsOf = new DateOnly(2026, 1, 31)
                }
            ],
            Transactions =
            [
                new InvestmentTransaction
                {
                    Type = InvestmentTransactionType.Deposit,
                    TransactionDate = new DateOnly(2026, 2, 1),
                    Amount = 500m,
                    Currency = new CurrencyCode("BRL"),
                    CurrencyPerEurRate = 6m
                },
                new InvestmentTransaction
                {
                    Type = InvestmentTransactionType.Withdrawal,
                    TransactionDate = new DateOnly(2026, 2, 2),
                    Amount = 100m,
                    Currency = new CurrencyCode("BRL"),
                    CurrencyPerEurRate = 6m
                }
            ]
        };

        var result = _service.CalculatePosition(instrument);

        Assert.Equal(10_400m, result.CurrentManualValueNative);
        Assert.Equal(new DateOnly(2026, 1, 31), result.CurrentManualValueAsOf);
        Assert.True(result.IsManualValueEstimated);
        Assert.Null(result.NetInvestedNative);
        Assert.Null(result.NetInvestedEur);
    }

    private static InvestmentInstrument MarketInstrument()
        => new()
        {
            Id = Guid.NewGuid(),
            NativeCurrency = new CurrencyCode("USD"),
            ValuationMode = ValuationMode.MarketQuote
        };

    private static InvestmentTransaction MarketTransaction(
        InvestmentTransactionType type,
        DateOnly date,
        decimal quantity,
        decimal price,
        decimal rate)
        => new()
        {
            Type = type,
            TransactionDate = date,
            Quantity = quantity,
            UnitPrice = price,
            Currency = new CurrencyCode("USD"),
            CurrencyPerEurRate = rate
        };
}

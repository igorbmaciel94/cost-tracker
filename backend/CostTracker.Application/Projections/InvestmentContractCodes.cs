using CostTracker.Application.Exceptions;
using CostTracker.Domain.Enums;

namespace CostTracker.Application.Projections;

internal static class InvestmentContractCodes
{
    public static string ToCode(this AssetClass value) => value switch
    {
        AssetClass.Stocks => "STOCKS",
        AssetClass.Reits => "REITS",
        AssetClass.BrazilFixedIncome => "BRAZIL_FIXED_INCOME",
        AssetClass.InternationalFixedIncome => "INTERNATIONAL_FIXED_INCOME",
        AssetClass.Cryptocurrencies => "CRYPTOCURRENCIES",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static AssetClass ParseAssetClass(string? value) => Normalize(value) switch
    {
        "STOCKS" => AssetClass.Stocks,
        "REITS" => AssetClass.Reits,
        "BRAZIL_FIXED_INCOME" => AssetClass.BrazilFixedIncome,
        "INTERNATIONAL_FIXED_INCOME" => AssetClass.InternationalFixedIncome,
        "CRYPTOCURRENCIES" => AssetClass.Cryptocurrencies,
        _ => throw new DomainValidationException("assetClass must be STOCKS, REITS, BRAZIL_FIXED_INCOME, INTERNATIONAL_FIXED_INCOME or CRYPTOCURRENCIES.")
    };

    public static string ToCode(this InstrumentKind value) => value switch
    {
        InstrumentKind.Stock => "STOCK",
        InstrumentKind.Etf => "ETF",
        InstrumentKind.Adr => "ADR",
        InstrumentKind.Reit => "REIT",
        InstrumentKind.Bond => "BOND",
        InstrumentKind.Account => "ACCOUNT",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static InstrumentKind ParseInstrumentKind(string? value) => Normalize(value) switch
    {
        "STOCK" => InstrumentKind.Stock,
        "ETF" => InstrumentKind.Etf,
        "ADR" => InstrumentKind.Adr,
        "REIT" => InstrumentKind.Reit,
        "BOND" => InstrumentKind.Bond,
        "ACCOUNT" => InstrumentKind.Account,
        _ => throw new DomainValidationException("kind must be STOCK, ETF, ADR, REIT, BOND or ACCOUNT.")
    };

    public static string ToCode(this ValuationMode value) => value switch
    {
        ValuationMode.MarketQuote => "MARKET_QUOTE",
        ValuationMode.Manual => "MANUAL",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static ValuationMode ParseValuationMode(string? value) => Normalize(value) switch
    {
        "MARKET_QUOTE" => ValuationMode.MarketQuote,
        "MANUAL" => ValuationMode.Manual,
        _ => throw new DomainValidationException("valuationMode must be MARKET_QUOTE or MANUAL.")
    };

    public static string ToCode(this InvestmentTransactionType value) => value switch
    {
        InvestmentTransactionType.OpeningBalance => "OPENING_BALANCE",
        InvestmentTransactionType.Buy => "BUY",
        InvestmentTransactionType.Sell => "SELL",
        InvestmentTransactionType.Deposit => "DEPOSIT",
        InvestmentTransactionType.Withdrawal => "WITHDRAWAL",
        InvestmentTransactionType.Adjustment => "ADJUSTMENT",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static InvestmentTransactionType ParseTransactionType(string? value) => Normalize(value) switch
    {
        "OPENING_BALANCE" => InvestmentTransactionType.OpeningBalance,
        "BUY" => InvestmentTransactionType.Buy,
        "SELL" => InvestmentTransactionType.Sell,
        "DEPOSIT" => InvestmentTransactionType.Deposit,
        "WITHDRAWAL" => InvestmentTransactionType.Withdrawal,
        "ADJUSTMENT" => InvestmentTransactionType.Adjustment,
        _ => throw new DomainValidationException("transactionType must be OPENING_BALANCE, BUY, SELL, DEPOSIT, WITHDRAWAL or ADJUSTMENT.")
    };

    private static string Normalize(string? value)
        => value?.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant() ?? string.Empty;
}

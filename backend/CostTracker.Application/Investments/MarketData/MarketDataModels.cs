namespace CostTracker.Application.Investments.MarketData;

public static class MarketDataProviderCodes
{
    public const string TwelveData = "TWELVE_DATA";
    public const string Marketstack = "MARKETSTACK";
    public const string AlphaVantage = "ALPHA_VANTAGE";
    public const string YahooTest = "YAHOO_TEST";
    public const string Ecb = "ECB";
    public const string BcbPtax = "BCB_PTAX";
    public const string Manual = "MANUAL";
}

public sealed record MarketQuoteRequest(
    Guid InstrumentId,
    string ProviderSymbol,
    string? Exchange,
    string? Mic,
    string ExpectedCurrency,
    decimal PriceMultiplier = 1m);

public sealed record MarketQuoteResult(
    Guid InstrumentId,
    string Provider,
    string ProviderSymbol,
    string? Exchange,
    string? Mic,
    decimal Price,
    string Currency,
    string PriceKind,
    DateOnly AsOf,
    DateTimeOffset FetchedAt,
    bool IsFallback,
    string RawPayloadHash);

public sealed record ExchangeRateResult(
    string Provider,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    string RateKind,
    DateOnly AsOf,
    DateTimeOffset FetchedAt,
    bool IsFallback,
    string RawPayloadHash);

public sealed record ProviderFailure(
    string Provider,
    string Subject,
    string Message,
    bool IsTransient);

public sealed record ProviderBatchResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<ProviderFailure> Failures)
{
    public static ProviderBatchResult<T> Empty { get; } = new([], []);
}

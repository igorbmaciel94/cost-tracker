using CostTracker.Application.Investments.MarketData;

namespace CostTracker.Application.Investments.Dividends;

public static class DividendEventStatusCodes
{
    public const string Scheduled = "SCHEDULED";
    public const string Due = "DUE";
    public const string Credited = "CREDITED";
    public const string NoEntitlement = "NO_ENTITLEMENT";
}

public sealed class CreateDividendEventRequest
{
    public decimal GrossAmountPerUnit { get; set; }
    public decimal WithholdingTaxPercent { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly ExDate { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed record DividendEventDto(
    Guid Id,
    Guid InstrumentId,
    string InstrumentName,
    string? Ticker,
    decimal GrossAmountPerUnit,
    decimal WithholdingTaxPercent,
    string Currency,
    DateOnly ExDate,
    DateOnly PaymentDate,
    string? Notes,
    string Status,
    decimal? EligibleQuantity,
    decimal? GrossAmount,
    decimal? WithholdingTaxAmount,
    decimal? NetAmount,
    decimal? CurrencyPerEurRate,
    decimal? NetAmountEur,
    DateOnly? FxAsOf,
    string? FxSource,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt,
    bool CanDelete);

public sealed record DividendCashBalanceDto(
    string Currency,
    decimal Amount,
    decimal? AmountEur,
    DateOnly? LastPaymentDate,
    FxRateReferenceDto? FxData);

public sealed record DividendCashSummaryDto(
    decimal? TotalEur,
    bool IsPartial,
    IReadOnlyList<DividendCashBalanceDto> Balances);

public sealed record DividendProcessingResult(
    int ProcessedCount,
    int NoEntitlementCount,
    int MissingFxCount);

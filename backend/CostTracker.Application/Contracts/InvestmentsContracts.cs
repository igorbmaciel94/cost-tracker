namespace CostTracker.Application.Contracts;

public sealed record AllocationTargetDto(string AssetClass, decimal Weight);

public sealed record InvestmentPortfolioDto(
    Guid Id,
    string BaseCurrency,
    long Version,
    DateTimeOffset UpdatedAt,
    bool IsConfigured,
    IReadOnlyList<AllocationTargetDto> AllocationTargets);

public sealed class UpdateInvestmentAllocationRequest
{
    public long? ExpectedVersion { get; set; }
    public IReadOnlyList<UpdateInvestmentAllocationItemRequest> Items { get; set; } = [];
}

public sealed class UpdateInvestmentAllocationItemRequest
{
    public string AssetClass { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public sealed record InvestmentPositionDto(
    decimal Quantity,
    bool IsCostKnown,
    decimal? CostBasisNative,
    decimal? AverageCostNative,
    decimal? NetInvestedNative,
    decimal? NetInvestedEur,
    decimal? CurrentManualValueNative,
    DateOnly? CurrentManualValueAsOf,
    bool IsManualValueEstimated);

public sealed record InvestmentInstrumentDto(
    Guid Id,
    Guid PortfolioId,
    string AssetClass,
    string Kind,
    string Name,
    string? Identifier,
    string? Ticker,
    string? Mic,
    string? Isin,
    string NativeCurrency,
    string ValuationMode,
    int AllocationScore,
    decimal? QuantityStep,
    bool IsArchived,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    InvestmentPositionDto Position,
    ManualValuationDto? LatestManualValuation);

public sealed record InvestmentInstrumentDetailDto(
    InvestmentInstrumentDto Instrument,
    IReadOnlyList<InvestmentTransactionDto> Transactions,
    IReadOnlyList<ManualValuationDto> ManualValuations);

public sealed class CreateInvestmentInstrumentRequest
{
    public string AssetClass { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Identifier { get; set; }
    public string? Ticker { get; set; }
    public string? Mic { get; set; }
    public string? Isin { get; set; }
    public string NativeCurrency { get; set; } = string.Empty;
    public string ValuationMode { get; set; } = string.Empty;
    public int AllocationScore { get; set; }
    public decimal? QuantityStep { get; set; }
    public CreateInvestmentTransactionRequest? OpeningTransaction { get; set; }
    public CreateManualValuationRequest? ManualValuation { get; set; }
}

public sealed class UpdateInvestmentInstrumentRequest
{
    public long? ExpectedVersion { get; set; }
    public string AssetClass { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Identifier { get; set; }
    public string? Ticker { get; set; }
    public string? Mic { get; set; }
    public string? Isin { get; set; }
    public string NativeCurrency { get; set; } = string.Empty;
    public string ValuationMode { get; set; } = string.Empty;
    public int AllocationScore { get; set; }
    public decimal? QuantityStep { get; set; }
}

public sealed record InvestmentTransactionDto(
    Guid Id,
    Guid InstrumentId,
    string TransactionType,
    DateOnly TransactionDate,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    string? Currency,
    decimal FeeAmount,
    decimal? CurrencyPerEurRate,
    string? Notes,
    string IdempotencyKey,
    DateTimeOffset CreatedAt);

public sealed class CreateInvestmentTransactionRequest
{
    public string TransactionType { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal? CurrencyPerEurRate { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed record ManualValuationDto(
    Guid Id,
    Guid InstrumentId,
    decimal Amount,
    string Currency,
    DateOnly AsOf,
    DateTimeOffset RecordedAt,
    string IdempotencyKey);

public sealed class CreateManualValuationRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly AsOf { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

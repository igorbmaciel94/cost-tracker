namespace CostTracker.Domain.ValueObjects;

public readonly record struct CurrencyCode
{
    public CurrencyCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must be a three-letter code.", nameof(value));

        Value = normalized;
    }

    public string Value { get; }

    public static CurrencyCode Eur => new("EUR");

    public override string ToString() => Value;
}

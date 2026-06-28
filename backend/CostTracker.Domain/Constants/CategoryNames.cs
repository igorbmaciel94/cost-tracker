namespace CostTracker.Domain.Constants;

public static class CategoryNames
{
    public const string Investimento = "Investimento";
    public const string Lazer = "Lazer";
    public const string ComprasOnline = "Compras online";
    public const string Saving = "Saving";

    public static string Normalize(string? value)
    {
        if (string.Equals(value?.Trim(), "Estudos", StringComparison.OrdinalIgnoreCase))
        {
            return Investimento;
        }

        return value?.Trim() ?? string.Empty;
    }
}

namespace CostTracker.Domain.Constants;

public static class CategoryNames
{
    public const string Investimento = "Investimento";

    public static string Normalize(string? value)
    {
        if (string.Equals(value?.Trim(), "Estudos", StringComparison.OrdinalIgnoreCase))
        {
            return Investimento;
        }

        return value?.Trim() ?? string.Empty;
    }
}

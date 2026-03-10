namespace CostTracker.Domain.Constants;

public static class GroupNames
{
    public const string Essenciais = "Essenciais";
    public const string Desejos = "Desejos";
    public const string Investimento = "Investimento";
    public const string Saving = "Saving";
    public const string Buffer = "Buffer";

    public static readonly string[] All =
    [
        Essenciais,
        Desejos,
        Investimento,
        Saving,
        Buffer
    ];

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (string.Equals(normalized, "Estudos", StringComparison.OrdinalIgnoreCase))
        {
            return Investimento;
        }

        if (string.Equals(normalized, "Investimentos", StringComparison.OrdinalIgnoreCase))
        {
            return Saving;
        }

        var canonical = All.FirstOrDefault(groupName =>
            string.Equals(groupName, normalized, StringComparison.OrdinalIgnoreCase));

        return canonical ?? normalized;
    }
}

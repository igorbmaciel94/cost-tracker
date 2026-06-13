namespace CostTracker.Domain.Constants;

public static class GroupNames
{
    public const string CustosFixos = "Custos Fixos";
    public const string Prazeres = "Prazeres";
    public const string Conhecimento = "Conhecimento";
    public const string LiberdadeFinanceira = "Liberdade Financeira";
    public const string Metas = "Metas";
    public const string Conforto = "Conforto";

    public static readonly string[] All =
    [
        CustosFixos,
        Prazeres,
        Conhecimento,
        LiberdadeFinanceira,
        Metas,
        Conforto
    ];

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (string.Equals(normalized, "Essenciais", StringComparison.OrdinalIgnoreCase))
        {
            return CustosFixos;
        }

        if (string.Equals(normalized, "Desejos", StringComparison.OrdinalIgnoreCase))
        {
            return Prazeres;
        }

        if (string.Equals(normalized, "Investimento", StringComparison.OrdinalIgnoreCase))
        {
            return Conhecimento;
        }

        if (string.Equals(normalized, "Estudos", StringComparison.OrdinalIgnoreCase))
        {
            return Conhecimento;
        }

        if (string.Equals(normalized, "Saving", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Investimentos", StringComparison.OrdinalIgnoreCase))
        {
            return LiberdadeFinanceira;
        }

        if (string.Equals(normalized, "Buffer", StringComparison.OrdinalIgnoreCase))
        {
            return Metas;
        }

        var canonical = All.FirstOrDefault(groupName =>
            string.Equals(groupName, normalized, StringComparison.OrdinalIgnoreCase));

        return canonical ?? normalized;
    }
}

using CostTracker.Domain.Constants;

namespace CostTracker.Infrastructure.Seed;

public sealed class InitialMonthSeedModel
{
    public string ReferenceMonth { get; init; } = "2026-03";
    public decimal Salary { get; init; } = 2760m;
    public string Currency { get; init; } = "EUR";
    public IReadOnlyList<InitialCategorySeedModel> Categories { get; init; } = [];
    public IReadOnlyList<InitialTargetSeedModel> Targets { get; init; } = [];
    public IReadOnlyList<InitialEntrySeedModel> Entries { get; init; } = [];

    public static InitialMonthSeedModel CreateDefault()
    {
        return new InitialMonthSeedModel
        {
            ReferenceMonth = "2026-03",
            Salary = 2760m,
            Currency = "EUR",
            Categories =
            [
                new("Arrendamento", GroupNames.CustosFixos, 335m, 1),
                new("Agua", GroupNames.CustosFixos, 29m, 2),
                new("Luz", GroupNames.CustosFixos, 100m, 3),
                new("Internet", GroupNames.CustosFixos, 106m, 4),
                new("Mercado", GroupNames.CustosFixos, 400m, 5),
                new("Transporte", GroupNames.CustosFixos, 0m, 6),
                new("Lavanderia", GroupNames.CustosFixos, 40m, 7),
                new("Saude", GroupNames.CustosFixos, 60m, 8),
                new("Credito", GroupNames.CustosFixos, 75m, 9),
                new("Dividas", GroupNames.CustosFixos, 511m, 10),
                new("Lazer", GroupNames.Prazeres, 315m, 11),
                new("Compras online", GroupNames.Prazeres, 315m, 12),
                new("Viagem", GroupNames.Prazeres, 138m, 13),
                new("Assinaturas", GroupNames.Prazeres, 60m, 14),
                new("Saving", GroupNames.LiberdadeFinanceira, 276m, 15),
                new("Investimento", GroupNames.Conhecimento, 0m, 16)
            ],
            Targets =
            [
                new(GroupNames.CustosFixos, 0.6m),
                new(GroupNames.Prazeres, 0.3m),
                new(GroupNames.Conhecimento, 0.0m),
                new(GroupNames.LiberdadeFinanceira, 0.1m),
                new(GroupNames.Metas, 0.0m),
                new(GroupNames.Conforto, 0.0m)
            ],
            Entries =
            [
                new(new DateOnly(2026, 3, 1), "Arrendamento", "Pagamento Arrendamento", 0m),
                new(new DateOnly(2026, 3, 1), "Agua", "Pagamento agua", 0m),
                new(new DateOnly(2026, 3, 1), "Dividas", "Pagamento dividas", 0m),
                new(new DateOnly(2026, 3, 1), "Assinaturas", "Pagamento assinatura", 0m)
            ]
        };
    }
}

public sealed record InitialCategorySeedModel(string Name, string GroupName, decimal PlannedAmount, int DisplayOrder);

public sealed record InitialTargetSeedModel(string GroupName, decimal TargetPercent);

public sealed record InitialEntrySeedModel(DateOnly EntryDate, string CategoryName, string Description, decimal Amount);

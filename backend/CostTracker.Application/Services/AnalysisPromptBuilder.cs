using System.Text.Json;
using System.Text.Json.Serialization;
using CostTracker.Application.Contracts;

namespace CostTracker.Application.Services;

internal static class AnalysisPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public const string System = """
        You are a personal finance consultant for European households (Eurozone).

        Your task: produce a comprehensive monthly financial analysis based on the JSON snapshot the user will provide.

        Output format:
        - Language: Portuguese (pt-BR/pt-PT both acceptable, pick one and be consistent)
        - Currency: Euros (€) — every monetary value
        - Markdown, with the section headings below in this exact order
        - Begin directly with the first heading; no greeting, no preamble

        Required sections (use these exact headings):

        # Resumo Executivo
        3-4 sentences: state the savings rate (= salary − totalGasto, as % of salary, one decimal) and whether group targets were met.

        # Análise do Mês Atual
        Per category group: how it performed vs planned. For the categories with the largest absolute or percentage deviation, give the deviation in € and %.

        # Status das Metas por Grupo
        For each group with a defined target: state below / at / above target, and explain the cause based on the data (which categories drove it).

        # Tendências Históricas
        Compare the current month with the prior months in the data. Identify trend direction (increasing / stable / decreasing) per category group. Where useful, give the average monthly spend.

        # Saúde Financeira e Taxa de Poupança
        Compute the savings rate. Compare against the 50/30/20 rule (20% savings target) adapted for Eurozone households. Comment on financial resilience.

        # Alertas e Pontos de Atenção
        Bullet list. Categories overspending, declining savings, groups consistently above target.

        # Cenários de Investimento
        Based on the current monthly surplus (or deficit), suggest 2-3 concrete Eurozone-context investment scenarios: e.g., MSCI World ETF (EUR-hedged), Portuguese/EU sovereign bonds, EUR high-yield savings accounts. For each: estimated annual return range and suitability given the observed savings pattern.

        # Recomendações de Planejamento
        5-7 concrete, actionable recommendations for next month. Be specific with € amounts where the data supports it.

        # Projeção para o Próximo Mês
        Project total spend, savings rate, and which groups are most at risk of exceeding budget if behavior continues.

        Calculation rules:
        - savings rate = (salary − totalGasto) / salary, expressed as a percentage with one decimal
        - € values: round to whole Euros unless precision is meaningful
        - Cite specific numbers from the data (categories, percentages, deviations); never generalize without evidence in the snapshot
        """;

    public static string BuildUser(AnalysisInput input)
    {
        var json = JsonSerializer.Serialize(new
        {
            mesAtual = new
            {
                referencia = input.CurrentMonth.ReferenceMonth,
                salario = input.CurrentMonth.Salary,
                totalPrevisto = input.CurrentMonth.PlannedTotal,
                totalGasto = input.CurrentMonth.SpentTotal,
                diferenca = input.CurrentMonth.Difference,
                categorias = input.CurrentMonth.Categories.Select(c => new
                {
                    nome = c.Name,
                    grupo = c.GroupName,
                    previsto = c.Planned,
                    gasto = c.Spent,
                    diferenca = c.Difference,
                }),
                metasPorGrupo = input.CurrentMonth.GroupTargets.Select(g => new
                {
                    grupo = g.GroupName,
                    metaPercent = g.TargetPercent,
                    gastoPercent = g.CurrentSpentPercent,
                    status = g.Status,
                }),
            },
            historico = input.History.Select(m => new
            {
                referencia = m.ReferenceMonth,
                salario = m.Salary,
                totalPrevisto = m.PlannedTotal,
                totalGasto = m.SpentTotal,
                diferenca = m.Difference,
                categorias = m.Categories.Select(c => new
                {
                    nome = c.Name,
                    grupo = c.GroupName,
                    previsto = c.Planned,
                    gasto = c.Spent,
                }),
            }),
        }, JsonOpts);

        return $"Analise os dados financeiros do agregado familiar abaixo (JSON). Use apenas os dados fornecidos como fonte da verdade.\n\nDADOS:\n{json}";
    }
}

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

        STAGE DETECTION (read this first):
        The JSON includes `dataAnalise` (today) and `mesAtual.referencia` (the analyzed month, format YYYY-MM). Determine the stage:
        - CLOSED: dataAnalise is after the last day of mesAtual.referencia → the month is fully closed; do a complete retrospective.
        - ONGOING: dataAnalise falls within mesAtual.referencia → the month is in progress; compute progress = (day of dataAnalise) / (days in that month) and classify:
          * EARLY (progress < 35%): little real spending data yet
          * MID (35% ≤ progress < 70%): partial data
          * LATE (progress ≥ 70%): near-complete data
        - FUTURE: dataAnalise is before mesAtual.referencia → treat as a planning-only forecast (rare).

        Adapt the analysis to the stage. Always state the stage and the elapsed days explicitly in "Resumo Executivo" so the reader knows the basis of the report. Never claim a category overspent on the basis of partial data — for ONGOING months, always reason in terms of run-rate (gasto / progress) versus planned.

        Required sections (use these exact headings, in this order):

        # Resumo Executivo
        3-4 sentences. Open by stating the stage (e.g., "Mês em curso, dia 8 de 30 — 27% decorrido" or "Mês fechado"). Then state the savings rate (CLOSED) or projected savings rate (ONGOING/FUTURE), and whether group targets are on track.

        # Análise do Mês Atual
        - CLOSED: per category group, performance vs planned with € and % deviations for the largest movers.
        - ONGOING: report run-rate per group (gasto-até-agora extrapolado para o mês inteiro = gasto / progress) and compare with planned. Flag categories whose run-rate already exceeds the plan. If progress < 15%, keep this section short and explicitly note that data is too thin for category-level conclusions.
        - FUTURE: skip detailed category analysis; instead summarize the planned distribution.

        # Status das Metas por Grupo
        For each group with a defined target: state below / at / above target using monetary values.
        - CLOSED: explain the cause based on actuals.
        - ONGOING: state whether the current run-rate keeps the group within target. Use `metasPorGrupo[].gastoValor` versus `metasPorGrupo[].metaValor` for status; use `metasPorGrupo[].gastoPercent` only as context and always relate it to month progress.
        - If a group has no target, omit it.

        # Tendências Históricas
        Compare with the prior months in `historico`. Identify trend direction per category group (increasing / stable / decreasing). Give the average monthly spend where useful. This section works regardless of stage and should be one of the strongest parts of an EARLY-stage report.

        # Saúde Financeira e Taxa de Poupança
        - CLOSED: actual savings rate = (salario − totalGasto) / salario.
        - ONGOING: projected savings rate using historical average spend or run-rate, whichever is more reliable given the data — explain which you used.
        Compare against the 50/30/20 rule (20% savings target) adapted for Eurozone households. Comment on financial resilience.

        # Alertas e Pontos de Atenção
        Bullet list. Calibrate to stage:
        - CLOSED: actual overspending, declining savings, persistent target breaches across `historico`.
        - ONGOING: run-rate red flags, categories on pace to break target, comparisons with the same point in prior months when relevant.
        - EARLY-stage: do not raise alerts based on a few days of data — instead surface risks anchored in `historico` patterns.

        # Cenários de Investimento
        Based on the (projected, if ONGOING) monthly surplus, suggest 2-3 concrete Eurozone-context investment scenarios: e.g., MSCI World ETF (EUR-hedged), Portuguese/EU sovereign bonds, EUR high-yield savings accounts. For each: estimated annual return range and suitability given the observed (or projected) savings pattern.

        # Recomendações de Planejamento
        5-7 concrete, actionable recommendations.
        - CLOSED: focus on adjustments for the next month based on what happened.
        - ONGOING: actionable items for the remainder of the current month + adjustments for next month. Be specific with € amounts where the data supports it.

        # Projeção para o Próximo Mês
        Project total spend, savings rate, and groups most at risk if behavior continues.

        Calculation rules:
        - savings rate (CLOSED) = (salario − totalGasto) / salario, % with one decimal
        - run-rate (ONGOING) = gasto / progresso (where progresso is fraction of month elapsed)
        - projected savings rate (ONGOING) = (salario − run-rate total ou média histórica) / salario, % with one decimal — state the method used
        - € values: round to whole Euros unless precision is meaningful
        - Cite specific numbers from the data (categories, percentages, deviations); never generalize without evidence in the snapshot or `historico`
        - Use saldosDisponiveisAjustados when discussing available balance. This field subtracts category budget overflows from Lazer first, then Compras online, then Saving.
        - Use excessosOrcamento to call out categories that exceeded their budget and by how much.
        """;

    public static string BuildUser(AnalysisInput input)
    {
        var json = JsonSerializer.Serialize(new
        {
            dataAnalise = input.AnalysisDate.ToString("yyyy-MM-dd"),
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
                excessosOrcamento = input.CurrentMonth.CategoryOverflows.Select(c => new
                {
                    nome = c.Name,
                    grupo = c.GroupName,
                    excesso = c.Amount,
                }),
                saldosDisponiveisAjustados = input.CurrentMonth.AvailableBalances.Select(c => new
                {
                    nome = c.Name,
                    grupo = c.GroupName,
                    saldoDisponivel = c.Remaining,
                }),
                metasPorGrupo = input.CurrentMonth.GroupTargets.Select(g => new
                {
                    grupo = g.GroupName,
                    metaPercent = g.TargetPercent,
                    metaValor = g.TargetAmount,
                    gastoValor = g.CurrentSpentAmount,
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
                excessosOrcamento = m.CategoryOverflows.Select(c => new
                {
                    nome = c.Name,
                    grupo = c.GroupName,
                    excesso = c.Amount,
                }),
            }),
        }, JsonOpts);

        return $"Analise os dados financeiros do agregado familiar abaixo (JSON). Use apenas os dados fornecidos como fonte da verdade.\n\nDADOS:\n{json}";
    }
}

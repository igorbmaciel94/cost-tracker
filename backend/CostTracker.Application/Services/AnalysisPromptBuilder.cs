using System.Text;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Build(AnalysisInput input)
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
                    diferenca = c.Difference
                }),
                metasPorGrupo = input.CurrentMonth.GroupTargets.Select(g => new
                {
                    grupo = g.GroupName,
                    metaPercent = g.TargetPercent,
                    gastoPercent = g.CurrentSpentPercent,
                    status = g.Status
                })
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
                    gasto = c.Spent
                })
            })
        }, JsonOpts);

        return new StringBuilder()
            .AppendLine("You are a personal finance consultant specializing in household budgeting for European households.")
            .AppendLine("All monetary values in the data are in Euros (€). Use € throughout your report.")
            .AppendLine("Analyze the financial data below (JSON format) and produce a comprehensive report in Portuguese (pt-PT or pt-BR).")
            .AppendLine()
            .AppendLine("IMPORTANT: Start your response directly with the first section heading. Do not include any preamble, greeting, or introductory sentence like 'Sure, I will generate...' or 'Claro, vou gerar...'.")
            .AppendLine()
            .AppendLine("Format the report in Markdown with the following sections:")
            .AppendLine()
            .AppendLine("# Resumo Executivo")
            .AppendLine("3-4 sentence overview of the current month's financial health. Highlight the savings rate (salary minus total spent) and whether targets were met.")
            .AppendLine()
            .AppendLine("# Análise do Mês Atual")
            .AppendLine("Evaluate each category group's performance. Highlight categories that exceeded or came in under budget. Include the absolute and percentage deviation for the most significant ones.")
            .AppendLine()
            .AppendLine("# Status das Metas por Grupo")
            .AppendLine("For each group with a defined target, analyze whether it is within or above the target and explain why. Note trends across months where applicable.")
            .AppendLine()
            .AppendLine("# Tendências Históricas")
            .AppendLine("Compare the current month with prior months. Identify spending trends (increasing, stable, or decreasing) per category group. Calculate average monthly spend for the most relevant categories.")
            .AppendLine()
            .AppendLine("# Saúde Financeira e Taxa de Poupança")
            .AppendLine("Calculate and comment on the monthly savings rate. Benchmark it against the commonly recommended 20% savings rate (50/30/20 rule adapted to European standards). Assess the overall financial resilience.")
            .AppendLine()
            .AppendLine("# Alertas e Pontos de Atenção")
            .AppendLine("Bullet-point list of the main financial concerns identified, including overspending categories, declining savings, or groups consistently above target.")
            .AppendLine()
            .AppendLine("# Cenários de Investimento")
            .AppendLine("Based on the current monthly surplus (or deficit), suggest 2-3 concrete investment scenarios in the European context: e.g., Euro-denominated index funds (MSCI World ETF), Portuguese/EU government bonds, high-yield savings accounts available in the Eurozone. For each scenario, estimate potential annual return and suitability given the savings pattern observed.")
            .AppendLine()
            .AppendLine("# Recomendações de Planejamento")
            .AppendLine("Provide 5-7 concrete, actionable recommendations for next month based on the data. Be specific about amounts in € where possible.")
            .AppendLine()
            .AppendLine("# Projeção para o Próximo Mês")
            .AppendLine("Based on observed trends, estimate what may happen next month if behavior continues. Include projected total spend, savings rate, and which groups are most at risk of exceeding budget.")
            .AppendLine()
            .AppendLine("---")
            .AppendLine("FINANCIAL DATA:")
            .AppendLine(json)
            .ToString();
    }
}

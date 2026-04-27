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
            .AppendLine("Você é um consultor financeiro pessoal especializado em finanças domésticas para brasileiros.")
            .AppendLine("Analise os dados financeiros abaixo (formato JSON) e produza um relatório completo em português brasileiro.")
            .AppendLine()
            .AppendLine("O relatório deve conter as seguintes seções, formatadas em Markdown:")
            .AppendLine()
            .AppendLine("# Resumo Executivo")
            .AppendLine("Uma visão geral de 3-4 frases sobre a saúde financeira do mês atual.")
            .AppendLine()
            .AppendLine("# Análise do Mês Atual")
            .AppendLine("Avalie o desempenho de cada grupo de categorias. Destaque categorias acima e abaixo do previsto.")
            .AppendLine()
            .AppendLine("# Status das Metas por Grupo")
            .AppendLine("Para cada grupo com meta definida, analise se está dentro ou fora da meta e por quê.")
            .AppendLine()
            .AppendLine("# Tendências Históricas")
            .AppendLine("Compare o mês atual com os meses anteriores. Identifique tendências de aumento ou redução de gastos.")
            .AppendLine()
            .AppendLine("# Alertas e Pontos de Atenção")
            .AppendLine("Liste em bullet points as principais preocupações financeiras identificadas.")
            .AppendLine()
            .AppendLine("# Recomendações de Planejamento")
            .AppendLine("Forneça 5-7 recomendações concretas e acionáveis para o próximo mês, baseadas nos dados.")
            .AppendLine()
            .AppendLine("# Projeção para o Próximo Mês")
            .AppendLine("Com base nas tendências, estime o que pode acontecer no próximo mês caso o comportamento continue.")
            .AppendLine()
            .AppendLine("---")
            .AppendLine("DADOS FINANCEIROS:")
            .AppendLine(json)
            .ToString();
    }
}

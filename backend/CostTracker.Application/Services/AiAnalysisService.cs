using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Integrations.Ai;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Pdf;
using CostTracker.Application.Projections;
using CostTracker.Domain.Calculations;
using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Application.Services;

public class AiAnalysisService(
    ICostTrackerDbContext dbContext,
    MonthProjectionService projections,
    IAiAnalysisClient ai,
    IPdfRenderer pdf)
{
    public async Task<byte[]> GenerateAsync(Guid monthId, CancellationToken ct)
    {
        var current = await dbContext.Months
            .WithDetails()
            .FirstOrDefaultAsync(m => m.Id == monthId, ct)
            ?? throw new NotFoundException("Mês não encontrado.");

        var history = await dbContext.Months
            .WithDetails()
            .Where(m => string.Compare(m.ReferenceMonth, current.ReferenceMonth) < 0)
            .OrderByDescending(m => m.ReferenceMonth)
            .Take(5)
            .ToListAsync(ct);

        var input = new AnalysisInput(
            BuildSnapshot(current),
            history.Select(BuildSnapshot).ToList(),
            DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var userPrompt = AnalysisPromptBuilder.BuildUser(input);
        var analysisMarkdown = await ai.GenerateAnalysisAsync(AnalysisPromptBuilder.System, userPrompt, ct);

        return pdf.Render(current.ReferenceMonth, analysisMarkdown);
    }

    private AnalysisMonthSnapshot BuildSnapshot(Month month)
    {
        var budget = projections.ToBudgetResponse(month);
        var targets = projections.ToTargetsResponse(month);

        var categories = budget.Lines.Select(l => new AnalysisCategoryLine(
            l.Name,
            l.GroupName,
            l.Planned,
            l.Spent,
            l.Difference
        )).ToList();

        var categoryOverflows = budget.Lines
            .Where(l => l.Difference < 0)
            .OrderBy(l => l.Difference)
            .Select(l => new AnalysisCategoryOverflow(
                l.Name,
                l.GroupName,
                Math.Abs(l.Difference)
            ))
            .ToList();

        var availableBalances = MonthCalculations
            .ComputeAvailableBalanceByCategory(month.CategoryBudgets, month.Entries)
            .Where(l => l.RemainingAmount > 0)
            .OrderByDescending(l => l.RemainingAmount)
            .Select(l => new AnalysisAvailableBalance(
                l.CategoryName,
                l.GroupName,
                l.RemainingAmount
            ))
            .ToList();

        var groupTargets = targets.Items.Select(t => new AnalysisGroupTarget(
            t.GroupName,
            t.TargetPercent * 100,
            t.CurrentSpentPercent * 100,
            t.SpentStatus
        )).ToList();

        return new AnalysisMonthSnapshot(
            month.ReferenceMonth,
            month.Salary,
            budget.PlannedTotal,
            budget.SpentTotal,
            budget.DifferenceTotal,
            categories,
            categoryOverflows,
            availableBalances,
            groupTargets
        );
    }
}

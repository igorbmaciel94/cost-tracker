namespace CostTracker.Application.Contracts;

internal sealed record AnalysisMonthSnapshot(
    string ReferenceMonth,
    decimal Salary,
    decimal PlannedTotal,
    decimal SpentTotal,
    decimal Difference,
    IReadOnlyList<AnalysisCategoryLine> Categories,
    IReadOnlyList<AnalysisCategoryOverflow> CategoryOverflows,
    IReadOnlyList<AnalysisAvailableBalance> AvailableBalances,
    IReadOnlyList<AnalysisGroupTarget> GroupTargets
);

internal sealed record AnalysisCategoryLine(
    string Name,
    string GroupName,
    decimal Planned,
    decimal Spent,
    decimal Difference
);

internal sealed record AnalysisCategoryOverflow(
    string Name,
    string GroupName,
    decimal Amount
);

internal sealed record AnalysisAvailableBalance(
    string Name,
    string GroupName,
    decimal Remaining
);

internal sealed record AnalysisGroupTarget(
    string GroupName,
    decimal TargetPercent,
    decimal CurrentSpentPercent,
    string Status
);

internal sealed record AnalysisInput(
    AnalysisMonthSnapshot CurrentMonth,
    IReadOnlyList<AnalysisMonthSnapshot> History,
    DateOnly AnalysisDate
);

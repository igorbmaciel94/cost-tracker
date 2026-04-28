namespace CostTracker.Application.Integrations.Ai;

public interface IAiAnalysisClient
{
    Task<string> GenerateAnalysisAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

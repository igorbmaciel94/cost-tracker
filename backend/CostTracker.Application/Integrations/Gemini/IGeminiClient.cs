namespace CostTracker.Application.Integrations.Gemini;

public interface IGeminiClient
{
    Task<string> GenerateAnalysisAsync(string prompt, CancellationToken ct);
}

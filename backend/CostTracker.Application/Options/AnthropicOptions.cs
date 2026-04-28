namespace CostTracker.Application.Options;

public class AnthropicOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-opus-4-7";
    public int MaxTokens { get; set; } = 16000;
}

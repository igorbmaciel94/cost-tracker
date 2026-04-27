namespace CostTracker.Application.Options;

public class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-2.5-pro-preview-03-25";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}

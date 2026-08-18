namespace CostTracker.Application.Options;

public sealed class DividendOptions
{
    public bool EnableBackgroundProcessing { get; set; }
    public bool ProcessOnStartup { get; set; } = true;
    public string ProcessingTimeZone { get; set; } = "Europe/Lisbon";
    public int ProcessingHour { get; set; } = 6;
}

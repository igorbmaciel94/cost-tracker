namespace CostTracker.Application.Options;

public sealed class MarketDataOptions
{
    public string TwelveDataApiKey { get; set; } = string.Empty;
    public string MarketstackApiKey { get; set; } = string.Empty;
    public bool EnablePublicTestQuotes { get; set; }
    public bool EnableBackgroundRefresh { get; set; }
    public bool RefreshOnStartup { get; set; } = true;
    public string RefreshTimeZone { get; set; } = "Europe/Lisbon";
    public int RefreshHour { get; set; } = 6;
    public int QuoteWarningSessions { get; set; } = 1;
    public int QuoteBlockingSessions { get; set; } = 2;
    public int ManualValuationWarningDays { get; set; } = 7;
    public int ManualValuationBlockingDays { get; set; } = 31;
    public int HttpTimeoutSeconds { get; set; } = 15;
}

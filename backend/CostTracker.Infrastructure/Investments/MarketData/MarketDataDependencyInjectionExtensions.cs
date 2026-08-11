using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CostTracker.Infrastructure.Investments.MarketData;

public static class MarketDataDependencyInjectionExtensions
{
    public static IServiceCollection AddInvestmentMarketData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MarketDataOptions>()
            .Bind(configuration.GetSection("MarketData"))
            .Validate(options => options.RefreshHour is >= 0 and <= 23, "MarketData:RefreshHour must be between 0 and 23.")
            .Validate(options => options.HttpTimeoutSeconds is >= 2 and <= 120, "MarketData:HttpTimeoutSeconds must be between 2 and 120.")
            .Validate(options => options.QuoteWarningSessions >= 0 && options.QuoteBlockingSessions >= 0,
                "MarketData quote freshness thresholds cannot be negative.")
            .Validate(options => options.ManualValuationWarningDays >= 0 && options.ManualValuationBlockingDays >= 0,
                "MarketData manual valuation freshness thresholds cannot be negative.")
            .Validate(options => options.QuoteBlockingSessions >= options.QuoteWarningSessions,
                "MarketData quote blocking threshold must be greater than or equal to the warning threshold.")
            .Validate(options => options.ManualValuationBlockingDays >= options.ManualValuationWarningDays,
                "MarketData manual valuation blocking threshold must be greater than or equal to the warning threshold.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        AddProviderClient<TwelveDataMarketQuoteProvider>(services, "https://api.twelvedata.com/");
        AddProviderClient<MarketstackMarketQuoteProvider>(services, "https://api.marketstack.com/");
        AddProviderClient<YahooTestMarketQuoteProvider>(services, "https://query1.finance.yahoo.com/");
        AddProviderClient<EcbExchangeRateProvider>(services, "https://data-api.ecb.europa.eu/");
        AddProviderClient<BcbPtaxExchangeRateProvider>(
            services,
            "https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/");

        services.AddTransient<IMarketQuoteProvider>(provider => provider.GetRequiredService<TwelveDataMarketQuoteProvider>());
        services.AddTransient<IMarketQuoteProvider>(provider => provider.GetRequiredService<MarketstackMarketQuoteProvider>());
        services.AddTransient<IMarketQuoteProvider>(provider => provider.GetRequiredService<YahooTestMarketQuoteProvider>());
        services.AddTransient<IExchangeRateProvider>(provider => provider.GetRequiredService<EcbExchangeRateProvider>());
        services.AddTransient<IExchangeRateProvider>(provider => provider.GetRequiredService<BcbPtaxExchangeRateProvider>());

        return services;
    }

    private static void AddProviderClient<TClient>(IServiceCollection services, string baseAddress)
        where TClient : class
    {
        services.AddHttpClient<TClient>((provider, client) =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<MarketDataOptions>>().Value.HttpTimeoutSeconds);
        });
    }
}

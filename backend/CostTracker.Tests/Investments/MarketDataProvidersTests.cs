using System.Net;
using System.Text;
using CostTracker.Application.Investments.MarketData;
using CostTracker.Application.Options;
using CostTracker.Infrastructure.Investments.MarketData;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Investments;

public class MarketDataProvidersTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 11, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TwelveData_ShouldNormalizeAndValidateQuoteMetadata()
    {
        const string payload = """
            {
              "symbol": "VWRA",
              "exchange": "LSE",
              "mic_code": "XLON",
              "currency": "USD",
              "datetime": "2026-08-10",
              "close": "142.1250"
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(payload));
        var provider = new TwelveDataMarketQuoteProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.twelvedata.com/") },
            Options.Create(new MarketDataOptions { TwelveDataApiKey = "test-key" }),
            new FixedTimeProvider(FixedNow));

        var instrumentId = Guid.NewGuid();
        var result = await provider.GetLatestQuotesAsync(
            [new MarketQuoteRequest(instrumentId, "VWRA", "LSE", "XLON", "USD")],
            CancellationToken.None);

        var quote = Assert.Single(result.Items);
        Assert.Empty(result.Failures);
        Assert.Equal(instrumentId, quote.InstrumentId);
        Assert.Equal(142.1250m, quote.Price);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal("XLON", quote.Mic);
        Assert.Equal(new DateOnly(2026, 8, 10), quote.AsOf);
        Assert.False(quote.IsFallback);
    }

    [Fact]
    public async Task TwelveData_ShouldNotCallNetworkWithoutApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var provider = new TwelveDataMarketQuoteProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.twelvedata.com/") },
            Options.Create(new MarketDataOptions()),
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestQuotesAsync(
            [new MarketQuoteRequest(Guid.NewGuid(), "KO", "NYSE", "XNYS", "USD")],
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Contains(result.Failures, failure => failure.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Ecb_ShouldReturnCurrencyUnitsPerEur()
    {
        const string csv = """
            KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE,OBS_STATUS
            EXR.D.BRL.EUR.SP00.A,D,BRL,EUR,SP00,A,2026-08-10,5.8764,A
            EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2026-08-10,1.1555,A
            """;
        var provider = new EcbExchangeRateProvider(
            new HttpClient(new StubHttpMessageHandler(_ => TextResponse(csv)))
            {
                BaseAddress = new Uri("https://data-api.ecb.europa.eu/")
            },
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestRatesAsync(
            ["EUR", "USD", "BRL"],
            new DateOnly(2026, 8, 11),
            CancellationToken.None);

        Assert.Empty(result.Failures);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1m, result.Items.Single(rate => rate.QuoteCurrency == "EUR").Rate);
        Assert.Equal(1.1555m, result.Items.Single(rate => rate.QuoteCurrency == "USD").Rate);
        Assert.Equal(5.8764m, result.Items.Single(rate => rate.QuoteCurrency == "BRL").Rate);
    }

    [Fact]
    public async Task Ecb_ShouldUseLatestObservationWithoutAnArbitraryLookbackWindow()
    {
        const string csv = """
            KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE,OBS_STATUS
            EXR.D.BRL.EUR.SP00.A,D,BRL,EUR,SP00,A,2025-12-31,6.4251,A
            """;
        Uri? requestedUri = null;
        var provider = new EcbExchangeRateProvider(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                requestedUri = request.RequestUri;
                return TextResponse(csv);
            }))
            {
                BaseAddress = new Uri("https://data-api.ecb.europa.eu/")
            },
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestRatesAsync(
            ["BRL"],
            new DateOnly(2026, 8, 11),
            CancellationToken.None);

        var rate = Assert.Single(result.Items);
        Assert.Empty(result.Failures);
        Assert.Equal(new DateOnly(2025, 12, 31), rate.AsOf);
        Assert.DoesNotContain("startPeriod", requestedUri!.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("endPeriod=2026-08-11", requestedUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bcb_ShouldDeriveEurBasedRatesFromClosingPtax()
    {
        const string eurPayload = """
            {"value":[{"cotacaoVenda":5.88720,"dataHoraCotacao":"2026-08-10 13:10:22.642754","tipoBoletim":"Fechamento"}]}
            """;
        const string usdPayload = """
            {"value":[{"cotacaoVenda":5.09630,"dataHoraCotacao":"2026-08-10 13:10:22.642754","tipoBoletim":"Fechamento"}]}
            """;
        var handler = new StubHttpMessageHandler(request =>
            JsonResponse(request.RequestUri!.ToString().Contains("%27EUR%27", StringComparison.OrdinalIgnoreCase) ||
                         request.RequestUri.ToString().Contains("'EUR'", StringComparison.OrdinalIgnoreCase)
                ? eurPayload
                : usdPayload));
        var provider = new BcbPtaxExchangeRateProvider(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/")
            },
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestRatesAsync(
            ["USD", "BRL"],
            new DateOnly(2026, 8, 11),
            CancellationToken.None);

        Assert.Empty(result.Failures);
        Assert.Equal(5.88720m, result.Items.Single(rate => rate.QuoteCurrency == "BRL").Rate);
        Assert.Equal(
            decimal.Round(5.88720m / 5.09630m, 10),
            decimal.Round(result.Items.Single(rate => rate.QuoteCurrency == "USD").Rate, 10));
        Assert.All(result.Items, rate => Assert.True(rate.IsFallback));
    }

    [Fact]
    public async Task Bcb_ShouldUseLatestClosingPtaxFromAllAvailableHistory()
    {
        const string eurPayload = """
            {"value":[{"cotacaoVenda":6.12340,"dataHoraCotacao":"2025-12-31 13:10:22.642754","tipoBoletim":"Fechamento"}]}
            """;
        Uri? requestedUri = null;
        var provider = new BcbPtaxExchangeRateProvider(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                requestedUri = request.RequestUri;
                return JsonResponse(eurPayload);
            }))
            {
                BaseAddress = new Uri("https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/")
            },
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestRatesAsync(
            ["BRL"],
            new DateOnly(2026, 8, 11),
            CancellationToken.None);

        var rate = Assert.Single(result.Items);
        Assert.Empty(result.Failures);
        Assert.Equal(new DateOnly(2025, 12, 31), rate.AsOf);
        Assert.Contains("01-01-1984", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("08-11-2026", requestedUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicTestProvider_ShouldUseLastNonNullCloseAndRemainMarkedAsFallback()
    {
        const string payload = """
            {
              "chart": {
                "result": [{
                  "meta": {
                    "symbol": "KO",
                    "currency": "USD",
                    "fullExchangeName": "NYSE",
                    "exchangeTimezoneName": "America/New_York"
                  },
                  "timestamp": [1786305600, 1786392000],
                  "indicators": { "quote": [{ "close": [69.25, 70.50] }] }
                }],
                "error": null
              }
            }
            """;
        var provider = new YahooTestMarketQuoteProvider(
            new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(payload)))
            {
                BaseAddress = new Uri("https://query1.finance.yahoo.com/")
            },
            Options.Create(new MarketDataOptions { EnablePublicTestQuotes = true }),
            new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestQuotesAsync(
            [new MarketQuoteRequest(Guid.NewGuid(), "KO", "NYSE", "XNYS", "USD")],
            CancellationToken.None);

        var quote = Assert.Single(result.Items);
        Assert.Empty(result.Failures);
        Assert.Equal(70.50m, quote.Price);
        Assert.Equal("LATEST_AVAILABLE", quote.PriceKind);
        Assert.True(quote.IsFallback);
    }

    [Theory]
    [InlineData("YAHOO")]
    [InlineData("TWELVE_DATA")]
    public async Task QuoteProviders_ShouldPreserveLondonPenceAsGbx(string providerCode)
    {
        const string yahooPayload = """
            {
              "chart": {
                "result": [{
                  "meta": {
                    "symbol": "ISF.L",
                    "currency": "GBp",
                    "fullExchangeName": "LSE",
                    "exchangeTimezoneName": "Europe/London"
                  },
                  "timestamp": [1786392000],
                  "indicators": { "quote": [{ "close": [1056.88] }] }
                }],
                "error": null
              }
            }
            """;
        const string twelveDataPayload = """
            {
              "symbol": "ISF",
              "exchange": "LSE",
              "mic_code": "XLON",
              "currency": "GBp",
              "datetime": "2026-08-10",
              "close": "1056.88"
            }
            """;

        IMarketQuoteProvider provider = providerCode == "YAHOO"
            ? new YahooTestMarketQuoteProvider(
                new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(yahooPayload)))
                {
                    BaseAddress = new Uri("https://query1.finance.yahoo.com/")
                },
                Options.Create(new MarketDataOptions { EnablePublicTestQuotes = true }),
                new FixedTimeProvider(FixedNow))
            : new TwelveDataMarketQuoteProvider(
                new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(twelveDataPayload)))
                {
                    BaseAddress = new Uri("https://api.twelvedata.com/")
                },
                Options.Create(new MarketDataOptions { TwelveDataApiKey = "test-key" }),
                new FixedTimeProvider(FixedNow));

        var result = await provider.GetLatestQuotesAsync(
            [new MarketQuoteRequest(Guid.NewGuid(), "ISF.L", "LSE", "XLON", "GBX")],
            CancellationToken.None);

        var quote = Assert.Single(result.Items);
        Assert.Empty(result.Failures);
        Assert.Equal("GBX", quote.Currency);
        Assert.Equal(1056.88m, quote.Price);
    }

    private static HttpResponseMessage JsonResponse(string payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage TextResponse(string payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/csv")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using System.Net;
using System.Text;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Integrations.Gemini;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Tests.Integrations;

public class GeminiClientRetryTests
{
    private static GeminiOptions FastRetryOptions(int maxRetries = 3) => new()
    {
        ApiKey = "fake",
        Model = "fake-model",
        BaseUrl = "https://example.test",
        MaxRetries = maxRetries,
        InitialBackoffMs = 1
    };

    [Fact]
    public async Task GenerateAnalysisAsync_RetriesOn503AndSucceeds()
    {
        var handler = new QueuedHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") },
            BuildSuccess("ok response")
        );

        var client = new GeminiClient(new HttpClient(handler), Options.Create(FastRetryOptions()));

        var result = await client.GenerateAnalysisAsync("ping", CancellationToken.None);

        Assert.Equal("ok response", result);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAnalysisAsync_RetriesOn429AndSucceeds()
    {
        var handler = new QueuedHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("limit") },
            BuildSuccess("ok")
        );

        var client = new GeminiClient(new HttpClient(handler), Options.Create(FastRetryOptions()));

        var result = await client.GenerateAnalysisAsync("ping", CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAnalysisAsync_DoesNotRetryOn400()
    {
        var handler = new QueuedHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") }
        );

        var client = new GeminiClient(new HttpClient(handler), Options.Create(FastRetryOptions()));

        await Assert.ThrowsAsync<ConflictException>(() =>
            client.GenerateAnalysisAsync("ping", CancellationToken.None));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAnalysisAsync_ThrowsAfterExhaustingRetries()
    {
        var handler = new QueuedHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") }
        );

        var client = new GeminiClient(new HttpClient(handler), Options.Create(FastRetryOptions(maxRetries: 2)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            client.GenerateAnalysisAsync("ping", CancellationToken.None));
        Assert.Equal(3, handler.CallCount);
    }

    private static HttpResponseMessage BuildSuccess(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"" + text + "\"}]}}]}",
            Encoding.UTF8,
            "application/json")
    };

    private sealed class QueuedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _queue = new(responses);
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_queue.Dequeue());
        }
    }
}

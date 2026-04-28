using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Application.Integrations.Gemini;

public class GeminiClient(HttpClient http, IOptions<GeminiOptions> opts) : IGeminiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> GenerateAnalysisAsync(string prompt, CancellationToken ct)
    {
        var options = opts.Value;
        var url = $"{options.BaseUrl}/v1beta/models/{options.Model}:generateContent?key={options.ApiKey}";

        var requestBody = new GeminiRequest(
            [new GeminiContent([new GeminiPart(prompt)])],
            new GeminiGenerationConfig(0.7f)
        );

        HttpResponseMessage response = null!;
        string lastError = "";

        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            response = await http.PostAsJsonAsync(url, requestBody, JsonOpts, ct);

            if (response.IsSuccessStatusCode)
            {
                break;
            }

            lastError = await response.Content.ReadAsStringAsync(ct);

            if (!IsTransient(response.StatusCode) || attempt == options.MaxRetries)
            {
                throw new ConflictException($"Falha ao gerar análise (Gemini {response.StatusCode}): {lastError}");
            }

            response.Dispose();
            var delayMs = options.InitialBackoffMs * (1 << attempt);
            await Task.Delay(delayMs, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOpts, ct)
            ?? throw new ConflictException("Resposta inválida do Gemini.");

        return result.Candidates?[0]?.Content?.Parts?[0]?.Text
            ?? throw new ConflictException("Gemini não retornou texto na resposta.");
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.ServiceUnavailable || status == HttpStatusCode.TooManyRequests;

    private record GeminiRequest(
        [property: JsonPropertyName("contents")] List<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig
    );

    private record GeminiContent(
        [property: JsonPropertyName("parts")] List<GeminiPart> Parts
    );

    private record GeminiPart(
        [property: JsonPropertyName("text")] string Text
    );

    private record GeminiGenerationConfig(
        [property: JsonPropertyName("temperature")] float Temperature
    );

    private record GeminiResponse(
        [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates
    );

    private record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content
    );
}

using Anthropic;
using Anthropic.Models.Messages;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace CostTracker.Application.Integrations.Ai;

public class ClaudeAnalysisClient : IAiAnalysisClient
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    public ClaudeAnalysisClient(IOptions<AnthropicOptions> opts)
    {
        _options = opts.Value;
        _client = new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<string> GenerateAnalysisAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        Message response;
        try
        {
            response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                Thinking = new ThinkingConfigAdaptive(),
                OutputConfig = new OutputConfig { Effort = Effort.High },
                System = new List<TextBlockParam>
                {
                    new() { Text = systemPrompt },
                },
                Messages = [new() { Role = Role.User, Content = userPrompt }],
            });
        }
        catch (Exception ex)
        {
            throw new ConflictException($"Falha ao gerar análise (Anthropic): {ex.Message}");
        }

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text) && text is not null)
            {
                return text.Text;
            }
        }

        throw new ConflictException("Anthropic não retornou bloco de texto na resposta.");
    }
}

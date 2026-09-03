using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.AI.Services;

public class AICostCalculator : IAICostCalculator
{
    // Gemini 2.5 Flash pricing (USD per 1 token)
    private const decimal GeminiInputPerToken = 0.075m / 1_000_000m;
    private const decimal GeminiOutputPerToken = 0.30m / 1_000_000m;

    // DeepSeek Chat pricing (USD per 1 token)
    private const decimal DeepSeekInputPerToken = 0.14m / 1_000_000m;
    private const decimal DeepSeekOutputPerToken = 0.28m / 1_000_000m;

    public decimal CalculateCost(string provider, string model, int promptTokens, int completionTokens)
    {
        if (promptTokens <= 0 && completionTokens <= 0)
        {
            return 0m;
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();

        decimal cost = 0m;
        if (normalizedProvider.Contains("gemini"))
        {
            cost = (promptTokens * GeminiInputPerToken) + (completionTokens * GeminiOutputPerToken);
        }
        else if (normalizedProvider.Contains("deepseek"))
        {
            cost = (promptTokens * DeepSeekInputPerToken) + (completionTokens * DeepSeekOutputPerToken);
        }

        return Math.Round(cost, 6, MidpointRounding.AwayFromZero);
    }
}

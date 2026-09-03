using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

// ── System Prompt Builder ────────────────────────────────────────────────────

public static class AISystemPromptBuilder
{
    /// <summary>
    /// Builds a compact, bounded system prompt from the account context.
    /// Never embeds raw JSON. Uses human-readable Turkish text blocks.
    /// </summary>
    public static string Build(AIAccountContext? ctx)
    {
        var sb = new StringBuilder();

        // Core persona
        sb.AppendLine("Tarla Asistanı için uzman bir ziraat mühendisisin.");
        sb.AppendLine("Çiftçilere Türkçe, anlaşılır, bilimsel ve pratik tarımsal tavsiyeler ver.");
        sb.AppendLine("Fotoğraf gönderildiyse yaprak, meyve, gövde veya kökteki hastalık, zararlı ya da besin eksikliği belirtilerini teşhis et;");
        sb.AppendLine("kültürel önlemler ve etken madde bazlı çözüm önerilerini maddeler halinde açıkla.");
        sb.AppendLine();

        // Ground rules
        sb.AppendLine("--- KURALLAR ---");
        sb.AppendLine("- Aşağıdaki hava durumu ve tarla bilgileri backend'den alınan gerçek verilerdir.");
        sb.AppendLine("- Context'te olmayan hava bilgisini ASLA uydurma.");
        sb.AppendLine("- Hava durumu 'güncel değil' olarak işaretlenmişse, bu veriyi kesin güncelmiş gibi sunma.");
        sb.AppendLine("- Hava tahmini, kesinlik değildir; 'bekleniyor / tahmin ediliyor' dilini kullan.");
        sb.AppendLine("- Kullanıcının tarla adı ve mevcut işleriyle doğal biçimde ilişki kur.");
        sb.AppendLine("- Kullanıcı adını her mesajda tekrarlama.");
        sb.AppendLine("- Hava riskleri varsa bunları yanıtında dikkate al.");
        sb.AppendLine("- Kimyasal doz veya yasal ilaç tavsiyesi üretme.");
        sb.AppendLine("- Hava bilgisi tek başına kesin tarımsal güvenlik garantisi değildir.");
        sb.AppendLine("- Hava durumu 'mevcut değil' ise bunu açıkça belirt.");
        sb.AppendLine("- Backend iş-hava değerlendirmesini dikkate al; risk HIGH ise kesin şekilde 'uygundur' deme.");
        sb.AppendLine("- SuggestedAction değerini doğal Türkçeyle açıkla; kendi meteorolojik eşiklerini üretme.");
        sb.AppendLine("- LOW risk, koşulların kesin güvenli olduğu anlamına gelmez.");
        sb.AppendLine();

        if (ctx == null || ctx.Farms.Count == 0)
        {
            sb.AppendLine("--- KULLANICI TARILARI ---");
            sb.AppendLine("Henüz kayıtlı tarla bulunamadı.");
            return sb.ToString();
        }

        // User info
        if (!string.IsNullOrWhiteSpace(ctx.DisplayName))
        {
            sb.AppendLine($"--- KULLANICI ---");
            sb.AppendLine($"Ad: {ctx.DisplayName}");
            sb.AppendLine();
        }

        // Farm summaries
        sb.AppendLine("--- TARLALAR ---");
        foreach (var farm in ctx.Farms)
        {
            sb.AppendLine($"Tarla: {farm.Name}");
            if (!string.IsNullOrWhiteSpace(farm.CurrentCrop))
                sb.AppendLine($"  Ürün: {farm.CurrentCrop}");
            if (farm.AreaHa.HasValue)
                sb.AppendLine($"  Alan: {farm.AreaHa:F1} ha");
            if (!string.IsNullOrWhiteSpace(farm.NextTask))
            {
                var dueStr = farm.NextTaskDueDate.HasValue ? $" (Vade: {farm.NextTaskDueDate.Value:d MMM})" : "";
                sb.AppendLine($"  Sıradaki iş: {farm.NextTask}{dueStr}");
            }
            if (!string.IsNullOrWhiteSpace(farm.LastActivity))
            {
                var atStr = farm.LastActivityAt.HasValue
                    ? $" ({farm.LastActivityAt.Value:d MMM})"
                    : "";
                sb.AppendLine($"  Son faaliyet: {farm.LastActivity}{atStr}");
            }

            // Weather block (only if fetched)
            if (farm.Weather != null)
            {
                var w = farm.Weather;
                sb.AppendLine($"  HAVA — {farm.Name}");

                var statusStr = w.IsStale
                    ? $"güncel değil (son alınma: {w.DataTime:dd.MM.yyyy HH:mm} UTC)"
                    : $"güncel ({w.DataTime:dd.MM.yyyy HH:mm} UTC)";
                sb.AppendLine($"    Veri durumu: {statusStr}");

                if (!string.IsNullOrWhiteSpace(w.Condition))
                    sb.AppendLine($"    Durum: {w.Condition}");
                if (w.CurrentTemperatureC.HasValue)
                    sb.AppendLine($"    Sıcaklık: {w.CurrentTemperatureC:F1}°C");
                if (w.HumidityPercent.HasValue)
                    sb.AppendLine($"    Nem: %{w.HumidityPercent:F0}");
                if (w.WindSpeedKmh.HasValue)
                    sb.AppendLine($"    Rüzgar: {w.WindSpeedKmh:F0} km/s");
                if (w.NextRainProbabilityPct.HasValue)
                    sb.AppendLine($"    En yüksek yağış olasılığı (24s): %{w.NextRainProbabilityPct:F0}");
                if (w.Next24HoursPrecipitationMm.HasValue && w.Next24HoursPrecipitationMm > 0)
                    sb.AppendLine($"    Önümüzdeki 24 saat yağış: {w.Next24HoursPrecipitationMm:F1} mm");
                if (w.IsStale && !string.IsNullOrWhiteSpace(w.StaleReason))
                    sb.AppendLine($"    Uyarı: {w.StaleReason}");

                if (w.RiskSummaries.Count > 0)
                {
                    sb.AppendLine($"    Riskler:");
                    foreach (var risk in w.RiskSummaries)
                        sb.AppendLine($"      - {risk}");
                }
            }
            else if (WeatherContextWasExpected(farm))
            {
                sb.AppendLine($"  HAVA — {farm.Name}: Mevcut değil");
            }

            if (farm.WorkWeatherSignal != null)
            {
                var signal = farm.WorkWeatherSignal;
                sb.AppendLine("  İŞ-HAVA DEĞERLENDİRMESİ");
                sb.AppendLine($"    İş türü: {FormatWorkType(signal.WorkType)}");
                sb.AppendLine($"    Kod: {FormatCode(signal.Code)}");
                if (signal.RiskLevel.HasValue)
                    sb.AppendLine($"    Risk: {signal.RiskLevel.Value.ToString().ToUpperInvariant()}");
                sb.AppendLine($"    Öneri: {FormatAction(signal.SuggestedAction)}");
                sb.AppendLine($"    Güncel olmayan hava verisi: {(signal.IsBasedOnStaleWeather ? "EVET" : "HAYIR")}");
                sb.AppendLine("    Nedenler:");
                foreach (var reason in signal.Reasons)
                    sb.AppendLine($"      - {reason}");
            }

            sb.AppendLine();
        }

        if (ctx.Advisories != null && ctx.Advisories.Count > 0)
        {
            sb.AppendLine("--- AKTİF PROAKTİF UYARILAR ---");
            sb.AppendLine("Aşağıdaki uyarılar kullanıcının tarlaları için backend analiz motoru tarafından üretilmiş proaktif tavsiyelerdir.");
            sb.AppendLine("Kullanıcı selamlaştığında veya tarlasının durumunu sorduğunda bu tavsiyeleri proaktif, yapıcı ve samimi bir dille aktar:");
            foreach (var adv in ctx.Advisories)
            {
                sb.AppendLine($"• [{adv.FarmName}] {adv.Title} ({adv.Severity.ToString().ToUpperInvariant()})");
                sb.AppendLine($"  Özet: {adv.Summary}");
                sb.AppendLine($"  Gerekçe: {adv.AgronomicExplanation}");
                sb.AppendLine($"  Aksiyon: {adv.ActionRecommendation}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool WeatherContextWasExpected(AIFarmSummary farm) =>
        farm.WeatherRequested;

    private static string FormatWorkType(FarmWorkType type) => type switch
    {
        FarmWorkType.Spraying => "SPRAYING",
        FarmWorkType.Irrigation => "IRRIGATION",
        FarmWorkType.Fertilizing => "FERTILIZING",
        FarmWorkType.Sowing => "SOWING",
        FarmWorkType.Harvest => "HARVEST",
        _ => "UNKNOWN",
    };

    private static string FormatCode(WeatherActionSignalCode code) => code switch
    {
        WeatherActionSignalCode.SprayingConditions => "SPRAYING_CONDITIONS",
        WeatherActionSignalCode.IrrigationTiming => "IRRIGATION_TIMING",
        WeatherActionSignalCode.FertilizingConditions => "FERTILIZING_CONDITIONS",
        WeatherActionSignalCode.SowingConditions => "SOWING_CONDITIONS",
        WeatherActionSignalCode.HarvestConditions => "HARVEST_CONDITIONS",
        WeatherActionSignalCode.WeatherUnavailable => "WEATHER_UNAVAILABLE",
        WeatherActionSignalCode.ForecastNotAvailable => "FORECAST_NOT_AVAILABLE",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string FormatAction(WeatherSuggestedAction action) => action switch
    {
        WeatherSuggestedAction.Proceed => "PROCEED",
        WeatherSuggestedAction.ReviewTiming => "REVIEW_TIMING",
        WeatherSuggestedAction.DelayConsidered => "DELAY_CONSIDERED",
        WeatherSuggestedAction.WeatherUnavailable => "WEATHER_UNAVAILABLE",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };
}

// ── Local (fallback) Provider ────────────────────────────────────────────────

public class LocalAIChatProvider : IAIChatProvider
{
    public Task<AIChatResponseDto> GenerateAsync(AIChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var reply = request.PhotoBytes != null
            ? "Fotoğrafınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı analiz dönecek."
            : "Mesajınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı yanıt dönecek.";

        return Task.FromResult(new AIChatResponseDto(reply, conversationId));
    }

    public async IAsyncEnumerable<AIChatStreamChunkDto> GenerateStreamAsync(
        AIChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var reply = request.PhotoBytes != null
            ? "Fotoğrafınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı analiz dönecek."
            : "Mesajınızı aldım. AI sağlayıcısı bağlandığında ayrıntılı yanıt dönecek.";

        var words = reply.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var piece = i == words.Length - 1 ? words[i] : words[i] + " ";
            yield return new AIChatStreamChunkDto(Content: piece, ConversationId: conversationId);
            await Task.Yield();
        }

        yield return new AIChatStreamChunkDto(
            Done: true,
            ConversationId: conversationId,
            PromptTokens: 10,
            CompletionTokens: 10,
            TotalTokens: 20,
            EstimatedCostUsd: 0m);
    }
}

// ── DeepSeek Provider ────────────────────────────────────────────────────────

public class DeepSeekAIChatProvider : IAIChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly IAICostCalculator _costCalculator;

    public DeepSeekAIChatProvider(HttpClient httpClient, IConfiguration config, IAICostCalculator? costCalculator = null)
    {
        _httpClient = httpClient;
        _costCalculator = costCalculator ?? new AICostCalculator();
        _apiKey = FirstConfiguredValue(
            config["AI:DeepSeekApiKey"],
            config["DEEPSEEK_API_KEY"],
            Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")) ?? string.Empty;
        _model = FirstConfiguredValue(
            config["AI:DeepSeekModel"],
            config["DEEPSEEK_MODEL"],
            Environment.GetEnvironmentVariable("DEEPSEEK_MODEL")) ?? "deepseek-chat";
        _baseUrl = (FirstConfiguredValue(
            config["AI:DeepSeekBaseUrl"],
            config["DEEPSEEK_BASE_URL"],
            Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL")) ?? "https://api.deepseek.com").TrimEnd('/');
    }

    private static string? FirstConfiguredValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public async Task<AIChatResponseDto> GenerateAsync(AIChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.PhotoBytes != null)
        {
            throw new NotSupportedException("DeepSeek sağlayıcısı fotoğraf analizini desteklemiyor.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return await new LocalAIChatProvider().GenerateAsync(request, cancellationToken);
        }

        var systemPrompt = AISystemPromptBuilder.Build(request.AccountContext);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (request.History != null)
        {
            foreach (var h in request.History)
            {
                messages.Add(new { role = h.Role, content = h.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Message });

        var payload = new
        {
            model = _model,
            messages = messages,
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResult = JsonSerializer.Deserialize<DeepSeekChatCompletionResponse>(responseJson);

        var reply = chatResult?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException("AI sağlayıcısı geçerli bir yanıt döndürmedi.");
        }

        int? promptTokens = chatResult?.Usage?.PromptTokens;
        int? completionTokens = chatResult?.Usage?.CompletionTokens;
        int? totalTokens = chatResult?.Usage?.TotalTokens;
        decimal? cost = (promptTokens.HasValue || completionTokens.HasValue)
            ? _costCalculator.CalculateCost("deepseek", _model, promptTokens ?? 0, completionTokens ?? 0)
            : null;

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        return new AIChatResponseDto(reply, conversationId, promptTokens, completionTokens, totalTokens, cost);
    }

    public async IAsyncEnumerable<AIChatStreamChunkDto> GenerateStreamAsync(
        AIChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.PhotoBytes != null)
        {
            throw new NotSupportedException("DeepSeek sağlayıcısı fotoğraf analizini desteklemiyor.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            await foreach (var chunk in new LocalAIChatProvider().GenerateStreamAsync(request, cancellationToken))
            {
                yield return chunk;
            }
            yield break;
        }

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var systemPrompt = AISystemPromptBuilder.Build(request.AccountContext);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (request.History != null)
        {
            foreach (var h in request.History)
            {
                messages.Add(new { role = h.Role, content = h.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Message });

        var payload = new
        {
            model = _model,
            messages = messages,
            temperature = 0.2,
            stream = true,
            stream_options = new { include_usage = true }
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        int? promptTokens = null;
        int? completionTokens = null;
        int? totalTokens = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var contentEl))
            {
                var content = contentEl.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    yield return new AIChatStreamChunkDto(Content: content, ConversationId: conversationId);
                }
            }

            if (doc.RootElement.TryGetProperty("usage", out var usageEl))
            {
                if (usageEl.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
                if (usageEl.TryGetProperty("completion_tokens", out var ct)) completionTokens = ct.GetInt32();
                if (usageEl.TryGetProperty("total_tokens", out var tt)) totalTokens = tt.GetInt32();
            }
        }

        decimal? cost = (promptTokens.HasValue || completionTokens.HasValue)
            ? _costCalculator.CalculateCost("deepseek", _model, promptTokens ?? 0, completionTokens ?? 0)
            : null;

        yield return new AIChatStreamChunkDto(
            Done: true,
            ConversationId: conversationId,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens,
            EstimatedCostUsd: cost);
    }

    private class DeepSeekChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<DeepSeekChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public DeepSeekUsage? Usage { get; set; }
    }

    private class DeepSeekUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    private class DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekMessage? Message { get; set; }
    }

    private class DeepSeekMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

// ── Gemini Provider ──────────────────────────────────────────────────────────

public class GeminiAIChatProvider : IAIChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly IAICostCalculator _costCalculator;

    public GeminiAIChatProvider(HttpClient httpClient, IConfiguration config, IAICostCalculator? costCalculator = null)
    {
        _httpClient = httpClient;
        _costCalculator = costCalculator ?? new AICostCalculator();
        _apiKey = config.GetValue<string>("AI:GeminiApiKey")
            ?? config.GetValue<string>("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;
        _model = config.GetValue<string>("AI:GeminiModel")
            ?? config.GetValue<string>("GEMINI_MODEL")
            ?? Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? "gemini-2.5-flash";
    }

    public async Task<AIChatResponseDto> GenerateAsync(AIChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return await new LocalAIChatProvider().GenerateAsync(request, cancellationToken);
        }

        var systemPrompt = AISystemPromptBuilder.Build(request.AccountContext);

        var contents = new List<object>();

        if (request.History != null)
        {
            foreach (var h in request.History)
            {
                var role = h.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "model";
                contents.Add(new
                {
                    role,
                    parts = new object[] { new { text = h.Content } }
                });
            }
        }

        var userParts = new List<object>();
        if (request.PhotoBytes != null && request.PhotoBytes.Length > 0)
        {
            var mimeType = request.PhotoContentType ?? "image/jpeg";
            var b64Data = Convert.ToBase64String(request.PhotoBytes);
            userParts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = b64Data
                }
            });
        }

        userParts.Add(new { text = request.Message });
        contents.Add(new { role = "user", parts = userParts });

        var payload = new
        {
            system_instruction = new
            {
                parts = new object[]
                {
                    new { text = systemPrompt }
                }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.2
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        string? reply = null;
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts))
        {
            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    sb.Append(textEl.GetString());
                }
            }
            reply = sb.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException("AI sağlayıcısı geçerli bir yanıt döndürmedi.");
        }

        int? promptTokens = null;
        int? completionTokens = null;
        int? totalTokens = null;
        if (doc.RootElement.TryGetProperty("usageMetadata", out var usageEl))
        {
            if (usageEl.TryGetProperty("promptTokenCount", out var pt)) promptTokens = pt.GetInt32();
            if (usageEl.TryGetProperty("candidatesTokenCount", out var ct)) completionTokens = ct.GetInt32();
            if (usageEl.TryGetProperty("totalTokenCount", out var tt)) totalTokens = tt.GetInt32();
        }
        decimal? cost = (promptTokens.HasValue || completionTokens.HasValue)
            ? _costCalculator.CalculateCost("gemini", _model, promptTokens ?? 0, completionTokens ?? 0)
            : null;

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        return new AIChatResponseDto(reply, conversationId, promptTokens, completionTokens, totalTokens, cost);
    }

    public async IAsyncEnumerable<AIChatStreamChunkDto> GenerateStreamAsync(
        AIChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            await foreach (var chunk in new LocalAIChatProvider().GenerateStreamAsync(request, cancellationToken))
            {
                yield return chunk;
            }
            yield break;
        }

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var systemPrompt = AISystemPromptBuilder.Build(request.AccountContext);

        var contents = new List<object>();

        if (request.History != null)
        {
            foreach (var h in request.History)
            {
                var role = h.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "model";
                contents.Add(new
                {
                    role,
                    parts = new object[] { new { text = h.Content } }
                });
            }
        }

        var userParts = new List<object>();
        if (request.PhotoBytes != null && request.PhotoBytes.Length > 0)
        {
            var mimeType = request.PhotoContentType ?? "image/jpeg";
            var b64Data = Convert.ToBase64String(request.PhotoBytes);
            userParts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = b64Data
                }
            });
        }

        userParts.Add(new { text = request.Message });
        contents.Add(new { role = "user", parts = userParts });

        var payload = new
        {
            system_instruction = new
            {
                parts = new object[]
                {
                    new { text = systemPrompt }
                }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.2
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:streamGenerateContent?alt=sse";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        int? promptTokens = null;
        int? completionTokens = null;
        int? totalTokens = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

            var data = line["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(data)) continue;

            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textEl))
                    {
                        var text = textEl.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return new AIChatStreamChunkDto(Content: text, ConversationId: conversationId);
                        }
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("usageMetadata", out var usageEl))
            {
                if (usageEl.TryGetProperty("promptTokenCount", out var pt)) promptTokens = pt.GetInt32();
                if (usageEl.TryGetProperty("candidatesTokenCount", out var ct)) completionTokens = ct.GetInt32();
                if (usageEl.TryGetProperty("totalTokenCount", out var tt)) totalTokens = tt.GetInt32();
            }
        }

        decimal? cost = (promptTokens.HasValue || completionTokens.HasValue)
            ? _costCalculator.CalculateCost("gemini", _model, promptTokens ?? 0, completionTokens ?? 0)
            : null;

        yield return new AIChatStreamChunkDto(
            Done: true,
            ConversationId: conversationId,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens,
            EstimatedCostUsd: cost);
    }
}

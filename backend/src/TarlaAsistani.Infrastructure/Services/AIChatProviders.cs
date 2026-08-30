using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

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
}

public class DeepSeekAIChatProvider : IAIChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public DeepSeekAIChatProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config.GetValue<string>("AI:DeepSeekApiKey")
            ?? config.GetValue<string>("DEEPSEEK_API_KEY")
            ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
            ?? string.Empty;
        _model = config.GetValue<string>("AI:DeepSeekModel")
            ?? config.GetValue<string>("DEEPSEEK_MODEL")
            ?? Environment.GetEnvironmentVariable("DEEPSEEK_MODEL")
            ?? "deepseek-chat";
        _baseUrl = (config.GetValue<string>("AI:DeepSeekBaseUrl")
            ?? config.GetValue<string>("DEEPSEEK_BASE_URL")
            ?? Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL")
            ?? "https://api.deepseek.com").TrimEnd('/');
    }

    public async Task<AIChatResponseDto> GenerateAsync(AIChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.PhotoBytes != null)
        {
            throw new NotSupportedException("DeepSeek sağlayıcısı fotoğraf analizini desteklemiyor.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            // Fallback to local deterministic response if API key is not configured
            return await new LocalAIChatProvider().GenerateAsync(request, cancellationToken);
        }

        var messages = new List<object>
        {
            new { role = "system", content = "Tarla Asistanı için Türkçe, güvenli ve uygulanabilir tarım önerileri ver." }
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

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        return new AIChatResponseDto(reply, conversationId);
    }

    private class DeepSeekChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<DeepSeekChoice>? Choices { get; set; }
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

public class GeminiAIChatProvider : IAIChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAIChatProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
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
                    new
                    {
                        text = "Tarla Asistanı için uzman bir ziraat mühendisisin. " +
                               "Çiftçilere Türkçe, anlaşılır, bilimsel ve pratik tarımsal tavsiyeler ver. " +
                               "Fotoğraf gönderildiyse yaprak, meyve, gövde veya kökteki hastalık, " +
                               "zararlı ya da besin eksikliği belirtilerini teşhis et; " +
                               "kültürel önlemler ve etken madde bazlı çözüm önerilerini maddeler halinde açıkla."
                    }
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

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        return new AIChatResponseDto(reply, conversationId);
    }
}


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
        _apiKey = config.GetValue<string>("AI:DeepSeekApiKey") ?? string.Empty;
        _model = config.GetValue<string>("AI:DeepSeekModel") ?? "deepseek-chat";
        _baseUrl = (config.GetValue<string>("AI:DeepSeekBaseUrl") ?? "https://api.deepseek.com").TrimEnd('/');
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

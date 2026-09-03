using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.AI;

namespace TarlaAsistani.Infrastructure.Services.AI.DeepSeek;

/// <summary>
/// DeepSeek OpenAI-compatible Chat Completions implementation of <see cref="IAIAgentProvider"/>.
/// Translates provider-independent agent requests and tool definitions into DeepSeek chat completion calls,
/// parses genuine tool_calls responses, preserves tool call IDs and arguments,
/// and securely round-trips opaque reasoning_content metadata.
/// </summary>
public class DeepSeekAIAgentProvider : IAIAgentProvider
{
    public const string ReasoningContentMetadataKey = "deepseek_reasoning_content";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly ILogger<DeepSeekAIAgentProvider>? _logger;

    public DeepSeekAIAgentProvider(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<DeepSeekAIAgentProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;

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

    private string GetChatCompletionsUrl()
    {
        if (_baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return _baseUrl;
        }

        return $"{_baseUrl}/chat/completions";
    }

    /// <inheritdoc />
    public async Task<AIAgentResponse> GenerateResponseAsync(
        AIAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("DeepSeek API key is not configured. Set 'AI:DeepSeekApiKey' or 'DEEPSEEK_API_KEY'.");
        }

        var deepSeekRequest = BuildDeepSeekRequest(request);
        var requestJson = JsonSerializer.Serialize(deepSeekRequest, SerializerOptions);
        var url = GetChatCompletionsUrl();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to communicate with DeepSeek API endpoint.");
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var safeSnippet = errorBody.Length > 500 ? errorBody[..500] : errorBody;
                throw new HttpRequestException($"DeepSeek API request failed with status code {response.StatusCode}: {safeSnippet}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseDeepSeekResponse(responseJson);
        }
    }

    private DeepSeekChatCompletionRequest BuildDeepSeekRequest(AIAgentRequest request)
    {
        var deepSeekRequest = new DeepSeekChatCompletionRequest
        {
            Model = _model,
            Temperature = 0.2,
            Stream = false
        };

        // 1. Tool declarations
        if (request.Tools.Count > 0)
        {
            deepSeekRequest.Tools = request.Tools.Select(t => new DeepSeekToolDefinition
            {
                Type = "function",
                Function = new DeepSeekFunction
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.ParametersSchema
                }
            }).ToList();

            deepSeekRequest.ToolChoice = "auto";
        }

        // 2. System prompt from request
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            deepSeekRequest.Messages.Add(new DeepSeekChatMessage
            {
                Role = "system",
                Content = request.SystemPrompt.Trim()
            });
        }

        // 3. Messages from history
        foreach (var msg in request.Messages)
        {
            if (msg.Role == AIAgentRole.System)
            {
                deepSeekRequest.Messages.Add(new DeepSeekChatMessage
                {
                    Role = "system",
                    Content = msg.Content ?? string.Empty
                });
            }
            else if (msg.Role == AIAgentRole.User)
            {
                deepSeekRequest.Messages.Add(new DeepSeekChatMessage
                {
                    Role = "user",
                    Content = msg.Content ?? string.Empty
                });
            }
            else if (msg.Role == AIAgentRole.Assistant)
            {
                string? reasoningContent = null;
                if (msg.ProviderMetadata != null &&
                    msg.ProviderMetadata.TryGetValue(ReasoningContentMetadataKey, out var rc) &&
                    !string.IsNullOrWhiteSpace(rc))
                {
                    reasoningContent = rc;
                }

                List<DeepSeekToolCall>? toolCalls = null;
                if (msg.ToolCalls.Count > 0)
                {
                    toolCalls = msg.ToolCalls.Select(tc => new DeepSeekToolCall
                    {
                        Id = tc.CallId,
                        Type = "function",
                        Function = new DeepSeekFunctionCall
                        {
                            Name = tc.ToolName,
                            Arguments = tc.Arguments.ValueKind != JsonValueKind.Undefined && tc.Arguments.ValueKind != JsonValueKind.Null
                                ? tc.Arguments.GetRawText()
                                : "{}"
                        }
                    }).ToList();
                }

                deepSeekRequest.Messages.Add(new DeepSeekChatMessage
                {
                    Role = "assistant",
                    Content = msg.Content,
                    ReasoningContent = reasoningContent,
                    ToolCalls = toolCalls
                });
            }
            else if (msg.Role == AIAgentRole.Tool)
            {
                string toolContent;
                var tr = msg.ToolResult;

                if (tr == null)
                {
                    toolContent = "{}";
                }
                else if (tr.IsSuccess)
                {
                    toolContent = tr.Result.HasValue ? tr.Result.Value.GetRawText() : "{\"success\":true}";
                }
                else
                {
                    var failPayload = new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["error_code"] = tr.ErrorCode ?? "tool_execution_error",
                        ["error_message"] = tr.ErrorMessage ?? tr.GetContentString()
                    };
                    toolContent = JsonSerializer.Serialize(failPayload);
                }

                deepSeekRequest.Messages.Add(new DeepSeekChatMessage
                {
                    Role = "tool",
                    ToolCallId = tr?.CallId ?? string.Empty,
                    Content = toolContent
                });
            }
        }

        return deepSeekRequest;
    }

    private AIAgentResponse ParseDeepSeekResponse(string responseJson)
    {
        var chatResponse = JsonSerializer.Deserialize<DeepSeekChatCompletionResponse>(responseJson);

        if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
        {
            return new AIAgentResponse(null, finishReason: AIAgentFinishReason.Stop);
        }

        var choice = chatResponse.Choices[0];
        var message = choice.Message;
        var textContent = !string.IsNullOrWhiteSpace(message?.Content) ? message.Content.Trim() : null;

        var toolCalls = new List<AIToolCall>();
        if (message?.ToolCalls != null && message.ToolCalls.Count > 0)
        {
            foreach (var tc in message.ToolCalls)
            {
                var toolName = tc.Function?.Name ?? string.Empty;
                var rawArgs = tc.Function?.Arguments;
                JsonElement parsedArguments;

                if (string.IsNullOrWhiteSpace(rawArgs))
                {
                    using var emptyDoc = JsonDocument.Parse("{}");
                    parsedArguments = emptyDoc.RootElement.Clone();
                }
                else
                {
                    try
                    {
                        using var argDoc = JsonDocument.Parse(rawArgs);
                        parsedArguments = argDoc.RootElement.Clone();
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "DeepSeek returned malformed JSON arguments for tool '{ToolName}'.", toolName);
                        throw new InvalidOperationException($"DeepSeek returned malformed JSON in tool call arguments for '{toolName}'.", ex);
                    }
                }

                toolCalls.Add(new AIToolCall(tc.Id, toolName, parsedArguments));
            }
        }

        // Capture opaque reasoning_content if present
        Dictionary<string, string>? providerMetadata = null;
        if (!string.IsNullOrWhiteSpace(message?.ReasoningContent))
        {
            providerMetadata = new Dictionary<string, string>
            {
                [ReasoningContentMetadataKey] = message.ReasoningContent
            };
        }

        // Determine finish reason
        AIAgentFinishReason finishReason;
        if (toolCalls.Count > 0)
        {
            finishReason = AIAgentFinishReason.ToolCalls;
        }
        else
        {
            finishReason = choice.FinishReason?.ToLowerInvariant() switch
            {
                "tool_calls" => AIAgentFinishReason.ToolCalls,
                "stop" => AIAgentFinishReason.Stop,
                "length" => AIAgentFinishReason.Length,
                "content_filter" => AIAgentFinishReason.Error,
                _ => AIAgentFinishReason.Stop
            };
        }

        // Token usage
        int? promptTokens = chatResponse.Usage?.PromptTokens;
        int? completionTokens = chatResponse.Usage?.CompletionTokens;
        int? totalTokens = chatResponse.Usage?.TotalTokens;

        return new AIAgentResponse(
            content: textContent,
            toolCalls: toolCalls,
            finishReason: finishReason,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            totalTokens: totalTokens,
            providerMetadata: providerMetadata);
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.AI;

namespace TarlaAsistani.Infrastructure.Services.AI.Gemini;

/// <summary>
/// Google Gemini REST API implementation of <see cref="IAIAgentProvider"/>.
/// Translates provider-independent agent requests and tool definitions into Gemini generateContent calls
/// and translates Gemini functionCall responses into <see cref="AIAgentResponse"/>.
/// </summary>
public class GeminiAIAgentProvider : IAIAgentProvider
{
    private static readonly JsonSerializerOptions GeminiJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiAIAgentProvider>? _logger;

    public GeminiAIAgentProvider(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiAIAgentProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;

        _apiKey = config.GetValue<string>("AI:GeminiApiKey")
            ?? config.GetValue<string>("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        // Respect explicitly configured model without legacy downgrades
        _model = config.GetValue<string>("AI:GeminiModel")
            ?? config.GetValue<string>("GEMINI_MODEL")
            ?? Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? "gemini-1.5-flash";
    }

    /// <inheritdoc />
    public async Task<AIAgentResponse> GenerateResponseAsync(
        AIAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured. Set 'AI:GeminiApiKey' or 'GEMINI_API_KEY'.");
        }

        var geminiRequest = BuildGeminiRequest(request);
        var requestJson = JsonSerializer.Serialize(geminiRequest, GeminiJsonOptions);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

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
            _logger?.LogError(ex, "Failed to communicate with Gemini API endpoint.");
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var safeSnippet = errorBody.Length > 500 ? errorBody[..500] : errorBody;
                throw new HttpRequestException($"Gemini API request failed with status code {response.StatusCode}: {safeSnippet}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseGeminiResponse(responseJson);
        }
    }

    private static GeminiGenerateContentRequest BuildGeminiRequest(AIAgentRequest request)
    {
        var geminiRequest = new GeminiGenerateContentRequest();

        // 1. System instruction
        var systemTexts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            systemTexts.Add(request.SystemPrompt.Trim());
        }

        foreach (var msg in request.Messages.Where(m => m.Role == AIAgentRole.System && !string.IsNullOrWhiteSpace(m.Content)))
        {
            systemTexts.Add(msg.Content!.Trim());
        }

        if (systemTexts.Count > 0)
        {
            geminiRequest.SystemInstruction = new GeminiContent
            {
                Role = "user",
                Parts = systemTexts.Select(t => new GeminiPart { Text = t }).ToList()
            };
        }

        // 2. Conversation messages
        foreach (var msg in request.Messages)
        {
            if (msg.Role == AIAgentRole.System)
            {
                continue; // Handled in SystemInstruction
            }

            if (msg.Role == AIAgentRole.User)
            {
                geminiRequest.Contents.Add(new GeminiContent
                {
                    Role = "user",
                    Parts = new List<GeminiPart>
                    {
                        new() { Text = msg.Content ?? string.Empty }
                    }
                });
            }
            else if (msg.Role == AIAgentRole.Assistant)
            {
                var modelParts = new List<GeminiPart>();

                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    modelParts.Add(new GeminiPart { Text = msg.Content });
                }

                foreach (var tc in msg.ToolCalls)
                {
                    string? thoughtSig = null;
                    if (tc.ProviderMetadata != null && tc.ProviderMetadata.TryGetValue("thought_signature", out var sig))
                    {
                        thoughtSig = sig;
                    }
                    else if (msg.ProviderMetadata != null && msg.ProviderMetadata.TryGetValue("thought_signature", out var msgSig))
                    {
                        thoughtSig = msgSig;
                    }

                    object? argsObj = null;
                    if (tc.Arguments.ValueKind != JsonValueKind.Undefined && tc.Arguments.ValueKind != JsonValueKind.Null)
                    {
                        argsObj = tc.Arguments;
                    }

                    modelParts.Add(new GeminiPart
                    {
                        FunctionCall = new GeminiFunctionCall
                        {
                            Name = tc.ToolName,
                            Args = argsObj,
                            Id = !string.IsNullOrWhiteSpace(tc.CallId) ? tc.CallId : null
                        },
                        ThoughtSignature = thoughtSig
                    });
                }

                geminiRequest.Contents.Add(new GeminiContent
                {
                    Role = "model",
                    Parts = modelParts
                });
            }
            else if (msg.Role == AIAgentRole.Tool)
            {
                var toolPart = CreateFunctionResponsePart(msg.ToolResult);

                // Group consecutive tool responses into a single user turn if previous turn was tool responses
                if (geminiRequest.Contents.Count > 0 &&
                    geminiRequest.Contents[^1].Role == "user" &&
                    geminiRequest.Contents[^1].Parts.All(p => p.FunctionResponse != null))
                {
                    geminiRequest.Contents[^1].Parts.Add(toolPart);
                }
                else
                {
                    geminiRequest.Contents.Add(new GeminiContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPart> { toolPart }
                    });
                }
            }
        }

        // 3. Tool declarations
        if (request.Tools.Count > 0)
        {
            var declarations = request.Tools.Select(t => new GeminiFunctionDeclaration
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = GeminiAgentSchemaSanitizer.Sanitize(t.ParametersSchema)
            }).ToList();

            geminiRequest.Tools = new List<GeminiTool>
            {
                new() { FunctionDeclarations = declarations }
            };
        }

        // 4. Conservative temperature (0.2)
        geminiRequest.GenerationConfig = new GeminiGenerationConfig
        {
            Temperature = 0.2
        };

        return geminiRequest;
    }

    private static GeminiPart CreateFunctionResponsePart(AIToolResult? toolResult)
    {
        if (toolResult == null)
        {
            return new GeminiPart
            {
                FunctionResponse = new GeminiFunctionResponse
                {
                    Name = "unknown",
                    Response = new { output = string.Empty }
                }
            };
        }

        object responsePayload;
        if (toolResult.IsSuccess)
        {
            if (toolResult.Result.HasValue)
            {
                responsePayload = toolResult.Result.Value;
            }
            else
            {
                responsePayload = new { output = toolResult.GetContentString() };
            }
        }
        else
        {
            responsePayload = new Dictionary<string, object?>
            {
                ["success"] = false,
                ["error_code"] = toolResult.ErrorCode ?? "tool_execution_error",
                ["error_message"] = toolResult.ErrorMessage ?? toolResult.GetContentString()
            };
        }

        return new GeminiPart
        {
            FunctionResponse = new GeminiFunctionResponse
            {
                Name = toolResult.ToolName,
                Response = responsePayload,
                Id = !string.IsNullOrWhiteSpace(toolResult.CallId) ? toolResult.CallId : null
            }
        };
    }

    private static AIAgentResponse ParseGeminiResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidatesEl) || candidatesEl.GetArrayLength() == 0)
        {
            var blockedReason = AIAgentFinishReason.Stop;
            if (root.TryGetProperty("promptFeedback", out var pfEl) && pfEl.TryGetProperty("blockReason", out _))
            {
                blockedReason = AIAgentFinishReason.Error;
            }

            return new AIAgentResponse(null, finishReason: blockedReason);
        }

        var candidate = candidatesEl[0];
        var textBuilder = new StringBuilder();
        var toolCalls = new List<AIToolCall>();

        if (candidate.TryGetProperty("content", out var contentEl) &&
            contentEl.TryGetProperty("parts", out var partsEl) &&
            partsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in partsEl.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    var t = textEl.GetString();
                    if (!string.IsNullOrEmpty(t))
                    {
                        textBuilder.Append(t);
                    }
                }

                if (part.TryGetProperty("functionCall", out var fcEl) && fcEl.ValueKind == JsonValueKind.Object)
                {
                    var name = fcEl.GetProperty("name").GetString() ?? string.Empty;
                    string? callId = null;
                    if (fcEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    {
                        callId = idEl.GetString();
                    }

                    JsonElement args = default;
                    if (fcEl.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                    {
                        args = argsEl.Clone();
                    }
                    else
                    {
                        using var emptyDoc = JsonDocument.Parse("{}");
                        args = emptyDoc.RootElement.Clone();
                    }

                    Dictionary<string, string>? partMetadata = null;
                    if (part.TryGetProperty("thoughtSignature", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
                    {
                        partMetadata = new Dictionary<string, string>
                        {
                            ["thought_signature"] = tsEl.GetString()!
                        };
                    }
                    else if (part.TryGetProperty("thought_signature", out var tsSnakeEl) && tsSnakeEl.ValueKind == JsonValueKind.String)
                    {
                        partMetadata = new Dictionary<string, string>
                        {
                            ["thought_signature"] = tsSnakeEl.GetString()!
                        };
                    }

                    toolCalls.Add(new AIToolCall(callId, name, args, partMetadata));
                }
            }
        }

        var textContent = textBuilder.Length > 0 ? textBuilder.ToString() : null;

        AIAgentFinishReason finishReason;
        if (toolCalls.Count > 0)
        {
            finishReason = AIAgentFinishReason.ToolCalls;
        }
        else if (candidate.TryGetProperty("finishReason", out var frEl) && frEl.ValueKind == JsonValueKind.String)
        {
            var frStr = frEl.GetString();
            finishReason = frStr switch
            {
                "STOP" => AIAgentFinishReason.Stop,
                "MAX_TOKENS" => AIAgentFinishReason.Length,
                "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" => AIAgentFinishReason.Error,
                _ => AIAgentFinishReason.Stop
            };
        }
        else
        {
            finishReason = AIAgentFinishReason.Stop;
        }

        int? promptTokens = null;
        int? completionTokens = null;
        int? totalTokens = null;
        if (root.TryGetProperty("usageMetadata", out var usageEl))
        {
            if (usageEl.TryGetProperty("promptTokenCount", out var pt) && pt.TryGetInt32(out var ptVal)) promptTokens = ptVal;
            if (usageEl.TryGetProperty("candidatesTokenCount", out var ct) && ct.TryGetInt32(out var ctVal)) completionTokens = ctVal;
            if (usageEl.TryGetProperty("totalTokenCount", out var tt) && tt.TryGetInt32(out var ttVal)) totalTokens = ttVal;
        }

        return new AIAgentResponse(
            content: textContent,
            toolCalls: toolCalls,
            finishReason: finishReason,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            totalTokens: totalTokens);
    }
}

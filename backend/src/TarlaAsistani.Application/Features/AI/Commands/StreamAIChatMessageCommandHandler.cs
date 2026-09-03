using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class StreamAIChatMessageCommandHandler : IStreamRequestHandler<StreamAIChatMessageCommand, AIChatStreamChunkDto>
{
    private readonly IAIChatProvider _aiChatProvider;
    private readonly IAIContextService _contextService;
    private readonly IAIQuotaService _quotaService;
    private readonly IMediator? _mediator;
    private readonly string _providerName;
    private readonly string _modelName;
    private readonly bool _agentEnabled;

    public StreamAIChatMessageCommandHandler(
        IAIChatProvider aiChatProvider,
        IAIContextService contextService,
        IAIQuotaService quotaService,
        IConfiguration config,
        IMediator? mediator = null)
    {
        _aiChatProvider = aiChatProvider;
        _contextService = contextService;
        _quotaService = quotaService;
        _mediator = mediator;

        _providerName = FirstConfiguredValue(
            config["AI_CHAT_PROVIDER"],
            Environment.GetEnvironmentVariable("AI_CHAT_PROVIDER"),
            config["AI__Provider"],
            Environment.GetEnvironmentVariable("AI__Provider"),
            config["AI:Provider"])
            ?? "local";

        _modelName = _providerName.ToLowerInvariant().Contains("gemini")
            ? (FirstConfiguredValue(config["GEMINI_MODEL"], Environment.GetEnvironmentVariable("GEMINI_MODEL"), config["AI:GeminiModel"], config["AI__GeminiModel"]) ?? "gemini-1.5-flash")
            : _providerName.ToLowerInvariant().Contains("deepseek")
                ? (FirstConfiguredValue(config["DEEPSEEK_MODEL"], Environment.GetEnvironmentVariable("DEEPSEEK_MODEL"), config["AI:DeepSeekModel"], config["AI__DeepSeekModel"]) ?? "deepseek-chat")
                : "local";

        var isLocal = _providerName.Equals("local", StringComparison.OrdinalIgnoreCase);
        var agentEnabledConfig = FirstConfiguredValue(
            config["AI_AGENT_ENABLED"],
            Environment.GetEnvironmentVariable("AI_AGENT_ENABLED"),
            config["AI:AgentEnabled"],
            config["AI__AgentEnabled"]);

        if (bool.TryParse(agentEnabledConfig, out var parsedEnabled))
        {
            _agentEnabled = parsedEnabled;
        }
        else
        {
            _agentEnabled = !isLocal;
        }
    }

    private static string? FirstConfiguredValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public async IAsyncEnumerable<AIChatStreamChunkDto> Handle(
        StreamAIChatMessageCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var hasPhoto = request.PhotoBytes != null && request.PhotoBytes.Length > 0;
        var isSupportedAgentProvider = _providerName.Equals("gemini", StringComparison.OrdinalIgnoreCase) ||
                                       _providerName.Equals("deepseek", StringComparison.OrdinalIgnoreCase);

        // If Text-only and Agent is enabled on Gemini/DeepSeek, route execution through full agent loop
        if (!hasPhoto && _agentEnabled && isSupportedAgentProvider && _mediator != null)
        {
            var sendCommand = new SendAIChatMessageCommand(
                UserId: request.UserId,
                Message: request.Message,
                FieldId: request.FieldId,
                ConversationId: request.ConversationId,
                History: request.History,
                PhotoBytes: null,
                PhotoContentType: null);

            var agentResponse = await _mediator.Send(sendCommand, cancellationToken);

            yield return new AIChatStreamChunkDto(
                Content: agentResponse.Reply,
                Done: false,
                ConversationId: agentResponse.ConversationId);

            yield return new AIChatStreamChunkDto(
                Done: true,
                ConversationId: agentResponse.ConversationId,
                PromptTokens: agentResponse.PromptTokens,
                CompletionTokens: agentResponse.CompletionTokens,
                TotalTokens: agentResponse.TotalTokens,
                EstimatedCostUsd: agentResponse.EstimatedCostUsd,
                QuotaInfo: agentResponse.QuotaInfo);

            yield break;
        }

        // ── PASSIVE / MULTIMODAL STREAMING FLOW ──────────────────────────────────
        // 1. Quota Check (Fast fail before opening stream)
        await _quotaService.CheckQuotaAsync(request.UserId, hasPhoto, cancellationToken);

        // Resolve optional hintFarmId (string field_id → Guid?)
        Guid? hintFarmId = null;
        if (!string.IsNullOrWhiteSpace(request.FieldId) &&
            Guid.TryParse(request.FieldId, out var parsedFarmId))
        {
            hintFarmId = parsedFarmId;
        }

        // Build account context from authenticated user's DB records.
        var accountContext = await _contextService.BuildContextAsync(
            userId: request.UserId,
            message: request.Message,
            hintFarmId: hintFarmId,
            cancellationToken: cancellationToken);

        var aiRequest = new AIChatRequestDto(
            Message: request.Message.Trim(),
            FieldId: string.IsNullOrWhiteSpace(request.FieldId) ? null : request.FieldId.Trim(),
            ConversationId: string.IsNullOrWhiteSpace(request.ConversationId) ? null : request.ConversationId.Trim(),
            History: request.History,
            PhotoBytes: request.PhotoBytes,
            PhotoContentType: request.PhotoContentType,
            AccountContext: accountContext
        );

        var sw = Stopwatch.StartNew();
        var fullReply = new StringBuilder();
        string? conversationId = request.ConversationId;
        int? promptTokens = null;
        int? completionTokens = null;
        int? totalTokens = null;
        decimal? estimatedCost = null;

        await foreach (var chunk in _aiChatProvider.GenerateStreamAsync(aiRequest, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.ConversationId))
            {
                conversationId = chunk.ConversationId;
            }

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullReply.Append(chunk.Content);
                yield return chunk;
            }

            if (chunk.Done)
            {
                promptTokens = chunk.PromptTokens;
                completionTokens = chunk.CompletionTokens;
                totalTokens = chunk.TotalTokens;
                estimatedCost = chunk.EstimatedCostUsd;
            }
        }

        sw.Stop();

        // 2. Token & Cost resolution
        var promptTok = promptTokens ?? Math.Max(1, request.Message.Length / 4);
        var compTok = completionTokens ?? Math.Max(1, fullReply.Length / 4);
        var cost = estimatedCost ?? 0m;

        // 3. Record AI Token Usage & Cost in DB & structured logs
        await _quotaService.RecordUsageAsync(
            userId: request.UserId,
            provider: _providerName,
            model: _modelName,
            hasPhoto: hasPhoto,
            promptTokens: promptTok,
            completionTokens: compTok,
            durationMs: sw.ElapsedMilliseconds,
            estimatedCostUsd: cost,
            cancellationToken: cancellationToken);

        // 4. Attach updated quota info
        var quotaInfo = await _quotaService.GetQuotaStatusAsync(request.UserId, cancellationToken);

        yield return new AIChatStreamChunkDto(
            Done: true,
            ConversationId: conversationId ?? Guid.NewGuid().ToString("N"),
            PromptTokens: promptTok,
            CompletionTokens: compTok,
            TotalTokens: promptTok + compTok,
            EstimatedCostUsd: cost,
            QuotaInfo: quotaInfo);
    }
}

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
    private readonly string _providerName;
    private readonly string _modelName;

    public StreamAIChatMessageCommandHandler(
        IAIChatProvider aiChatProvider,
        IAIContextService contextService,
        IAIQuotaService quotaService,
        IConfiguration config)
    {
        _aiChatProvider = aiChatProvider;
        _contextService = contextService;
        _quotaService = quotaService;

        _providerName = config["AI:Provider"]
            ?? config["AI_CHAT_PROVIDER"]
            ?? Environment.GetEnvironmentVariable("AI_CHAT_PROVIDER")
            ?? "local";

        _modelName = _providerName.ToLowerInvariant().Contains("gemini")
            ? (config["AI:GeminiModel"] ?? config["GEMINI_MODEL"] ?? Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-1.5-flash")
            : _providerName.ToLowerInvariant().Contains("deepseek")
                ? (config["AI:DeepSeekModel"] ?? config["DEEPSEEK_MODEL"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-chat")
                : "local";
    }

    public async IAsyncEnumerable<AIChatStreamChunkDto> Handle(
        StreamAIChatMessageCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var hasPhoto = request.PhotoBytes != null && request.PhotoBytes.Length > 0;

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

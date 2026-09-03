using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class SendAIChatMessageCommandHandler : IRequestHandler<SendAIChatMessageCommand, AIChatResponseDto>
{
    private readonly IAIChatProvider _aiChatProvider;
    private readonly IAIContextService _contextService;
    private readonly IAIQuotaService _quotaService;
    private readonly string _providerName;
    private readonly string _modelName;

    public SendAIChatMessageCommandHandler(
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

    public async Task<AIChatResponseDto> Handle(
        SendAIChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        var hasPhoto = request.PhotoBytes != null && request.PhotoBytes.Length > 0;

        // 1. Quota Check (Fast fail before expensive AI invocation)
        await _quotaService.CheckQuotaAsync(request.UserId, hasPhoto, cancellationToken);

        // Resolve optional hintFarmId (string field_id → Guid?)
        Guid? hintFarmId = null;
        if (!string.IsNullOrWhiteSpace(request.FieldId) &&
            Guid.TryParse(request.FieldId, out var parsedFarmId))
        {
            hintFarmId = parsedFarmId;
        }

        // Build account context from authenticated user's DB records.
        // Weather included only when message is weather-relevant.
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
        var response = await _aiChatProvider.GenerateAsync(aiRequest, cancellationToken);
        sw.Stop();

        // 2. Record AI Token Usage & Cost
        var promptTokens = response.PromptTokens ?? 0;
        var completionTokens = response.CompletionTokens ?? 0;
        var cost = response.EstimatedCostUsd ?? 0m;

        await _quotaService.RecordUsageAsync(
            userId: request.UserId,
            provider: _providerName,
            model: _modelName,
            hasPhoto: hasPhoto,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            durationMs: sw.ElapsedMilliseconds,
            estimatedCostUsd: cost,
            cancellationToken: cancellationToken);

        // 3. Attach current quota info
        var quotaInfo = await _quotaService.GetQuotaStatusAsync(request.UserId, cancellationToken);

        return response with { QuotaInfo = quotaInfo };
    }
}

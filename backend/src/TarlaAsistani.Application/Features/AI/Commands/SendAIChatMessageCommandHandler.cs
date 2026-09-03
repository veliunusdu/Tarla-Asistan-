using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Services;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class SendAIChatMessageCommandHandler : IRequestHandler<SendAIChatMessageCommand, AIChatResponseDto>
{
    private readonly IAIChatProvider _aiChatProvider;
    private readonly IAIContextService _contextService;
    private readonly IAIQuotaService _quotaService;
    private readonly IAIAgentOrchestrator? _agentOrchestrator;
    private readonly IAIAgentSystemPromptBuilder? _systemPromptBuilder;
    private readonly ICurrentUserContext? _currentUserContext;
    private readonly IAICostCalculator? _costCalculator;
    private readonly ILogger<SendAIChatMessageCommandHandler> _logger;
    private readonly IConfiguration _config;
    private readonly string _providerName;
    private readonly string _modelName;
    private readonly bool _agentEnabled;

    public SendAIChatMessageCommandHandler(
        IAIChatProvider aiChatProvider,
        IAIContextService contextService,
        IAIQuotaService quotaService,
        IConfiguration config,
        IAIAgentOrchestrator? agentOrchestrator = null,
        IAIAgentSystemPromptBuilder? systemPromptBuilder = null,
        ICurrentUserContext? currentUserContext = null,
        IAICostCalculator? costCalculator = null,
        ILogger<SendAIChatMessageCommandHandler>? logger = null)
    {
        _aiChatProvider = aiChatProvider;
        _contextService = contextService;
        _quotaService = quotaService;
        _agentOrchestrator = agentOrchestrator;
        _systemPromptBuilder = systemPromptBuilder;
        _currentUserContext = currentUserContext;
        _costCalculator = costCalculator;
        _logger = logger ?? NullLogger<SendAIChatMessageCommandHandler>.Instance;
        _config = config;

        _providerName = config["AI:Provider"]
            ?? config["AI_CHAT_PROVIDER"]
            ?? Environment.GetEnvironmentVariable("AI_CHAT_PROVIDER")
            ?? "local";

        _modelName = _providerName.ToLowerInvariant().Contains("gemini")
            ? (config["AI:GeminiModel"] ?? config["GEMINI_MODEL"] ?? Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-1.5-flash")
            : _providerName.ToLowerInvariant().Contains("deepseek")
                ? (config["AI:DeepSeekModel"] ?? config["DEEPSEEK_MODEL"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-chat")
                : "local";

        var isLocal = _providerName.Equals("local", StringComparison.OrdinalIgnoreCase);
        var agentEnabledConfig = config["AI:AgentEnabled"]
            ?? config["AI_AGENT_ENABLED"]
            ?? Environment.GetEnvironmentVariable("AI_AGENT_ENABLED");

        if (bool.TryParse(agentEnabledConfig, out var parsedEnabled))
        {
            _agentEnabled = parsedEnabled;
        }
        else
        {
            _agentEnabled = !isLocal;
        }
    }

    public async Task<AIChatResponseDto> Handle(
        SendAIChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        var hasPhoto = request.PhotoBytes != null && request.PhotoBytes.Length > 0;

        // 1. Quota Check (Fast fail before expensive AI invocation)
        await _quotaService.CheckQuotaAsync(request.UserId, hasPhoto, cancellationToken);

        // 2. Defense-in-depth: Current user context consistency check
        if (_currentUserContext != null &&
            _currentUserContext.UserId.HasValue &&
            _currentUserContext.UserId.Value != Guid.Empty &&
            _currentUserContext.UserId.Value != request.UserId)
        {
            throw new UnauthorizedAccessException("Current user context does not match request user identity.");
        }

        // 3. Resolve optional hintFarmId (string field_id → Guid?)
        Guid? hintFarmId = null;
        if (!string.IsNullOrWhiteSpace(request.FieldId) &&
            Guid.TryParse(request.FieldId, out var parsedFarmId))
        {
            hintFarmId = parsedFarmId;
        }

        // 4. Build account context from authenticated user's DB records.
        var accountContext = await _contextService.BuildContextAsync(
            userId: request.UserId,
            message: request.Message,
            hintFarmId: hintFarmId,
            cancellationToken: cancellationToken);

        var isSupportedAgentProvider = _providerName.Equals("gemini", StringComparison.OrdinalIgnoreCase) ||
                                       _providerName.Equals("deepseek", StringComparison.OrdinalIgnoreCase);

        var runAgent = !hasPhoto && _agentEnabled && isSupportedAgentProvider && _agentOrchestrator != null;

        if (runAgent)
        {
            // ── AI AGENT FLOW ────────────────────────────────────────────────────────
            var agentMessages = new List<AIAgentMessage>();
            if (request.History != null)
            {
                foreach (var h in request.History)
                {
                    if (h.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        agentMessages.Add(AIAgentMessage.CreateUser(h.Content));
                    }
                    else if (h.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ||
                             h.Role.Equals("model", StringComparison.OrdinalIgnoreCase))
                    {
                        agentMessages.Add(AIAgentMessage.CreateAssistant(h.Content));
                    }
                    // Reject/ignore untrusted client roles (system, tool, etc.)
                }
            }
            agentMessages.Add(AIAgentMessage.CreateUser(request.Message.Trim()));

            var promptBuilder = _systemPromptBuilder ?? new AIAgentSystemPromptBuilder(TimeProvider.System, _config);
            var systemPrompt = promptBuilder.Build(accountContext);

            var agentRequest = new AIAgentRequest(agentMessages, systemPrompt: systemPrompt);
            var sw = Stopwatch.StartNew();
            var runResult = await _agentOrchestrator!.RunAsync(agentRequest, cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "AI Agent execution completed for user {UserId}. Success={Success}, Iterations={Iterations}, ProviderCalls={ProviderCalls}, DurationMs={DurationMs}",
                request.UserId, runResult.IsSuccess, runResult.Iterations, runResult.ProviderCalls, sw.ElapsedMilliseconds);

            if (!runResult.IsSuccess)
            {
                _logger.LogWarning(
                    "AI Agent execution failed for user {UserId}. ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                    request.UserId, runResult.ErrorCode, runResult.ErrorMessage);

                throw new AIAgentExecutionException(runResult.ErrorCode ?? "agent_execution_failed", "AI hizmeti şu anda kullanılamıyor.");
            }

            var promptTokens = runResult.PromptTokens ?? Math.Max(1, request.Message.Length / 4);
            var completionTokens = runResult.CompletionTokens ?? Math.Max(1, (runResult.Content?.Length ?? 0) / 4);
            var totalTokens = runResult.TotalTokens ?? (promptTokens + completionTokens);
            var cost = _costCalculator?.CalculateCost(_providerName, _modelName, promptTokens, completionTokens) ?? 0m;

            await _quotaService.RecordUsageAsync(
                userId: request.UserId,
                provider: _providerName,
                model: _modelName,
                hasPhoto: false,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                durationMs: sw.ElapsedMilliseconds,
                estimatedCostUsd: cost,
                cancellationToken: cancellationToken);

            var quotaInfo = await _quotaService.GetQuotaStatusAsync(request.UserId, cancellationToken);
            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");

            return new AIChatResponseDto(
                Reply: runResult.Content ?? string.Empty,
                ConversationId: conversationId,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens,
                EstimatedCostUsd: cost,
                QuotaInfo: quotaInfo);
        }

        // ── PASSIVE / PHOTO / LOCAL FLOW ─────────────────────────────────────────
        var aiRequest = new AIChatRequestDto(
            Message: request.Message.Trim(),
            FieldId: string.IsNullOrWhiteSpace(request.FieldId) ? null : request.FieldId.Trim(),
            ConversationId: string.IsNullOrWhiteSpace(request.ConversationId) ? null : request.ConversationId.Trim(),
            History: request.History,
            PhotoBytes: request.PhotoBytes,
            PhotoContentType: request.PhotoContentType,
            AccountContext: accountContext
        );

        var passiveSw = Stopwatch.StartNew();
        var response = await _aiChatProvider.GenerateAsync(aiRequest, cancellationToken);
        passiveSw.Stop();

        // Record AI Token Usage & Cost
        var pTokens = response.PromptTokens ?? 0;
        var cTokens = response.CompletionTokens ?? 0;
        var pCost = response.EstimatedCostUsd ?? 0m;

        await _quotaService.RecordUsageAsync(
            userId: request.UserId,
            provider: _providerName,
            model: _modelName,
            hasPhoto: hasPhoto,
            promptTokens: pTokens,
            completionTokens: cTokens,
            durationMs: passiveSw.ElapsedMilliseconds,
            estimatedCostUsd: pCost,
            cancellationToken: cancellationToken);

        // Attach current quota info
        var currentQuota = await _quotaService.GetQuotaStatusAsync(request.UserId, cancellationToken);

        return response with { QuotaInfo = currentQuota };
    }
}

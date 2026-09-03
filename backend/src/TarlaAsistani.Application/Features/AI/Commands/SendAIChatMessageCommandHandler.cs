using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class SendAIChatMessageCommandHandler : IRequestHandler<SendAIChatMessageCommand, AIChatResponseDto>
{
    private readonly IAIChatProvider _aiChatProvider;
    private readonly IAIContextService _contextService;

    public SendAIChatMessageCommandHandler(
        IAIChatProvider aiChatProvider,
        IAIContextService contextService)
    {
        _aiChatProvider = aiChatProvider;
        _contextService = contextService;
    }

    public async Task<AIChatResponseDto> Handle(
        SendAIChatMessageCommand request,
        CancellationToken cancellationToken)
    {
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

        return await _aiChatProvider.GenerateAsync(aiRequest, cancellationToken);
    }
}

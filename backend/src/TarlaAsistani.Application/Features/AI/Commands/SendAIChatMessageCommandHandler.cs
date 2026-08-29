using MediatR;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class SendAIChatMessageCommandHandler : IRequestHandler<SendAIChatMessageCommand, AIChatResponseDto>
{
    private readonly IAIChatProvider _aiChatProvider;

    public SendAIChatMessageCommandHandler(IAIChatProvider aiChatProvider)
    {
        _aiChatProvider = aiChatProvider;
    }

    public async Task<AIChatResponseDto> Handle(SendAIChatMessageCommand request, CancellationToken cancellationToken)
    {
        var aiRequest = new AIChatRequestDto(
            Message: request.Message.Trim(),
            FieldId: string.IsNullOrWhiteSpace(request.FieldId) ? null : request.FieldId.Trim(),
            ConversationId: string.IsNullOrWhiteSpace(request.ConversationId) ? null : request.ConversationId.Trim(),
            History: request.History,
            PhotoBytes: request.PhotoBytes,
            PhotoContentType: request.PhotoContentType
        );

        return await _aiChatProvider.GenerateAsync(aiRequest, cancellationToken);
    }
}

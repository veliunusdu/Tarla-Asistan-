using MediatR;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Commands;

public record StreamAIChatMessageCommand(
    Guid UserId,
    string Message,
    string? FieldId = null,
    string? ConversationId = null,
    List<ChatHistoryItem>? History = null,
    byte[]? PhotoBytes = null,
    string? PhotoContentType = null
) : IStreamRequest<AIChatStreamChunkDto>;

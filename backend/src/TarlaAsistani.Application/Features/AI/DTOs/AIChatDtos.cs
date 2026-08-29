using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Features.AI.DTOs;

public record ChatHistoryItem(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public record AIChatRequestDto(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("field_id")] string? FieldId = null,
    [property: JsonPropertyName("conversation_id")] string? ConversationId = null,
    [property: JsonPropertyName("history")] List<ChatHistoryItem>? History = null,
    [property: JsonIgnore] byte[]? PhotoBytes = null,
    [property: JsonIgnore] string? PhotoContentType = null
);

public record AIChatResponseDto(
    [property: JsonPropertyName("reply")] string Reply,
    [property: JsonPropertyName("conversation_id")] string ConversationId
);

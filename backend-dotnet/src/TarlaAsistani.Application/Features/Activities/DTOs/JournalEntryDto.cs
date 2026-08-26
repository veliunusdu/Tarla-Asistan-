namespace TarlaAsistani.Application.Features.Activities.DTOs;

public record JournalEntryDto(
    string EntryType,
    Guid Id,
    DateTime OccurredAt,
    string Title,
    string? Description,
    Dictionary<string, string?> Metadata
);

public record FarmJournalResponseDto(
    List<JournalEntryDto> Items,
    int Total,
    int Limit,
    int Offset
);

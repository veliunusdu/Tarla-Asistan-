using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.DTOs;

public record CaseMediaDto(
    Guid Id,
    MediaKind Kind,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    string Url
)
{
    public static CaseMediaDto FromEntity(MediaAsset m) => new(
        m.Id,
        m.Kind,
        m.OriginalName,
        m.ContentType,
        m.SizeBytes,
        m.Url
    );
}

public record CaseMessageDto(
    Guid Id,
    Guid CaseId,
    Guid SenderId,
    CaseMessageType MessageType,
    string Body,
    DateTime CreatedAtUtc,
    List<CaseMediaDto> Media
)
{
    public static CaseMessageDto FromEntity(CaseMessage msg) => new(
        msg.Id,
        msg.CaseId,
        msg.SenderId,
        msg.MessageType,
        msg.Body,
        msg.CreatedAtUtc,
        msg.MediaLinks.Select(ml => CaseMediaDto.FromEntity(ml.Media)).ToList()
    );
}

public record CaseSummaryDto(
    Guid Id,
    Guid FarmId,
    string FarmName,
    Guid CreatedById,
    Guid? AssignedExpertId,
    CaseCategory Category,
    CasePriority Priority,
    CaseStatus Status,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc,
    int MessageCount,
    int MediaCount
);

public record CaseDetailDto(
    Guid Id,
    Guid FarmId,
    string FarmName,
    Guid CreatedById,
    Guid? AssignedExpertId,
    CaseCategory Category,
    CasePriority Priority,
    CaseStatus Status,
    string Title,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc,
    List<CaseMediaDto> Media,
    List<CaseMessageDto> Messages
)
{
    public static CaseDetailDto FromEntity(SupportCase c) => new(
        c.Id,
        c.FarmId,
        c.Farm?.Name ?? string.Empty,
        c.CreatedById,
        c.AssignedExpertId,
        c.Category,
        c.Priority,
        c.Status,
        c.Title,
        c.Description,
        c.CreatedAtUtc,
        c.UpdatedAtUtc,
        c.ClosedAtUtc,
        c.MediaLinks.Select(ml => CaseMediaDto.FromEntity(ml.Media)).ToList(),
        c.Messages.Select(m => CaseMessageDto.FromEntity(m)).ToList()
    );
}

public record CaseListDto(
    List<CaseSummaryDto> Items,
    int Total,
    int Limit,
    int Offset
);

using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Features.Cases.DTOs;

public record RecentActivitySnapshotDto(
    Guid Id,
    string ActivityName,
    string? ActivityType,
    string Status,
    DateTime OccurredAtUtc,
    string? Description
);

public record CaseContextSnapshotDto(
    string FarmName,
    double? Latitude,
    double? Longitude,
    double? SizeInHectares,
    string? IrrigationMethod,
    string? SoilType,
    string? FarmNote,
    string? CropName,
    DateOnly? CropPlantedAt,
    DateOnly? CropHarvestedAt,
    int? CropGrowingDay,
    string? WeatherProvider,
    DateTime? WeatherFetchedAtUtc,
    bool IsBasedOnStaleWeather,
    double? CurrentTemperatureC,
    double? CurrentHumidityPercent,
    double? Next24HoursPrecipitationMm,
    List<RecentActivitySnapshotDto> RecentActivities
)
{
    public static CaseContextSnapshotDto FromEntity(CaseContextSnapshot snapshot)
    {
        var activities = JsonSerializer.Deserialize<List<RecentActivitySnapshotDto>>(snapshot.RecentActivitiesJson) ?? [];
        return new(
            snapshot.FarmName, snapshot.Latitude, snapshot.Longitude, snapshot.SizeInHectares,
            snapshot.IrrigationMethod, snapshot.SoilType, snapshot.FarmNote, snapshot.CropName,
            snapshot.CropPlantedAt, snapshot.CropHarvestedAt, snapshot.CropGrowingDay,
            snapshot.WeatherProvider, snapshot.WeatherFetchedAtUtc, snapshot.IsBasedOnStaleWeather,
            snapshot.CurrentTemperatureC, snapshot.CurrentHumidityPercent,
            snapshot.Next24HoursPrecipitationMm, activities);
    }
}

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? FarmId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FarmName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CreatedById,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? AssignedExpertId,
    CaseCategory Category,
    CasePriority Priority,
    CaseStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc,
    int MessageCount,
    int MediaCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SystemSummary = null,
    bool IsRedacted = false
)
{
    public static CaseSummaryDto FromEntity(SupportCase c) => new(
        c.Id,
        c.FarmId,
        c.Farm?.Name ?? string.Empty,
        c.CreatedById,
        c.AssignedExpertId,
        c.Category,
        c.Priority,
        c.Status,
        c.Title,
        c.CreatedAtUtc,
        c.UpdatedAtUtc,
        c.ClosedAtUtc,
        c.Messages?.Count ?? 0,
        c.MediaLinks?.Count ?? 0,
        SystemSummary: null,
        IsRedacted: false
    );

    public static CaseSummaryDto FromEntityRedacted(SupportCase c) => new(
        c.Id,
        FarmId: null,
        FarmName: null,
        CreatedById: null,
        AssignedExpertId: c.AssignedExpertId,
        Category: c.Category,
        Priority: c.Priority,
        Status: c.Status,
        Title: null,
        CreatedAtUtc: c.CreatedAtUtc,
        UpdatedAtUtc: c.UpdatedAtUtc,
        ClosedAtUtc: c.ClosedAtUtc,
        MessageCount: 0,
        MediaCount: c.MediaLinks?.Count ?? 0,
        SystemSummary: $"{c.Category} - {c.Priority}",
        IsRedacted: true
    );
}

public record CaseDetailDto(
    Guid Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? FarmId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FarmName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CreatedById,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? AssignedExpertId,
    CaseCategory Category,
    CasePriority Priority,
    CaseStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CaseMediaDto>? Media,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CaseMessageDto>? Messages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CaseContextSnapshotDto? Context = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SystemSummary = null,
    bool IsRedacted = false
)
{
    public static CaseDetailDto FromEntity(SupportCase c) => new(
        Id: c.Id,
        FarmId: c.FarmId,
        FarmName: c.Farm?.Name ?? string.Empty,
        CreatedById: c.CreatedById,
        AssignedExpertId: c.AssignedExpertId,
        Category: c.Category,
        Priority: c.Priority,
        Status: c.Status,
        Title: c.Title,
        Description: c.Description,
        CreatedAtUtc: c.CreatedAtUtc,
        UpdatedAtUtc: c.UpdatedAtUtc,
        ClosedAtUtc: c.ClosedAtUtc,
        Media: c.MediaLinks.Select(ml => CaseMediaDto.FromEntity(ml.Media)).ToList(),
        Messages: c.Messages.Select(m => CaseMessageDto.FromEntity(m)).ToList(),
        Context: c.Context == null ? null : CaseContextSnapshotDto.FromEntity(c.Context),
        SystemSummary: null,
        IsRedacted: false
    );

    public static CaseDetailDto FromEntityRedacted(SupportCase c) => new(
        Id: c.Id,
        FarmId: null,
        FarmName: null,
        CreatedById: null,
        AssignedExpertId: null,
        Category: c.Category,
        Priority: c.Priority,
        Status: c.Status,
        Title: null,
        Description: null,
        CreatedAtUtc: c.CreatedAtUtc,
        UpdatedAtUtc: c.UpdatedAtUtc,
        ClosedAtUtc: c.ClosedAtUtc,
        Media: null,
        Messages: null,
        Context: null,
        SystemSummary: $"{c.Category} - {c.Priority}",
        IsRedacted: true
    );
}

public record UnassignedCasePoolDto(
    Guid Id,
    CaseCategory Category,
    CasePriority Priority,
    CaseStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MediaCount,
    string? SystemSummary
)
{
    public static UnassignedCasePoolDto FromEntity(SupportCase c) => new(
        c.Id,
        c.Category,
        c.Priority,
        c.Status,
        c.CreatedAtUtc,
        c.UpdatedAtUtc,
        c.MediaLinks?.Count ?? 0,
        $"{c.Category} - {c.Priority}"
    );
}

public record CaseListDto(
    IReadOnlyList<CaseSummaryDto> Items,
    int Total,
    int Limit,
    int Offset
);

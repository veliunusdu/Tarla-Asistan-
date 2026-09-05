using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using System.Text.Json;

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
    List<CaseMessageDto> Messages,
    CaseContextSnapshotDto? Context = null
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
        c.Messages.Select(m => CaseMessageDto.FromEntity(m)).ToList(),
        c.Context == null ? null : CaseContextSnapshotDto.FromEntity(c.Context)
    );
}

public record CaseListDto(
    List<CaseSummaryDto> Items,
    int Total,
    int Limit,
    int Offset
);

using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Activities.DTOs;

public record ActivityRevisionDto(
    Guid Id,
    Guid ActivityId,
    Guid? ChangedById,
    string PreviousValues,
    DateTime ChangedAtUtc
)
{
    public static ActivityRevisionDto FromEntity(ActivityRevision ar) => new(
        ar.Id,
        ar.ActivityId,
        ar.ChangedById,
        ar.PreviousValues,
        ar.ChangedAtUtc
    );
}

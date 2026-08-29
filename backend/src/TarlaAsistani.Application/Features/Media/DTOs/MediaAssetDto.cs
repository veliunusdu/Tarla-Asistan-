using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Media.DTOs;

public record MediaAssetDto(
    Guid Id,
    Guid OwnerId,
    MediaKind Kind,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string Url
)
{
    public static MediaAssetDto FromEntity(MediaAsset m) => new(
        m.Id,
        m.OwnerId,
        m.Kind,
        m.OriginalName,
        m.ContentType,
        m.SizeBytes,
        m.CreatedAtUtc,
        m.Url
    );
}

public record MediaContentDto(
    byte[] Content,
    string ContentType
);

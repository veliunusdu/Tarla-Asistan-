using System.Text.Json.Serialization;

namespace TarlaAsistani.Application.Features.AI.DTOs;

public record AIQuotaStatusDto(
    [property: JsonPropertyName("daily_photo_limit")] int DailyPhotoLimit,
    [property: JsonPropertyName("photos_used_today")] int PhotosUsedToday,
    [property: JsonPropertyName("photos_remaining_today")] int PhotosRemainingToday,
    [property: JsonPropertyName("daily_text_limit")] int DailyTextLimit,
    [property: JsonPropertyName("texts_used_today")] int TextsUsedToday,
    [property: JsonPropertyName("texts_remaining_today")] int TextsRemainingToday,
    [property: JsonPropertyName("resets_at_utc")] DateTime ResetsAtUtc
);

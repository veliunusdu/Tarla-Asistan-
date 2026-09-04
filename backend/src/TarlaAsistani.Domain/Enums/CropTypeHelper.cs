namespace TarlaAsistani.Domain.Enums;

public static class CropTypeHelper
{
    public static string ToTurkishName(CropType cropType) => cropType switch
    {
        CropType.Wheat => "Buğday",
        CropType.Barley => "Arpa",
        CropType.Corn => "Mısır",
        CropType.Sunflower => "Ayçiçeği",
        CropType.Tomato => "Domates",
        _ => cropType.ToString()
    };

    public static CropType? TryMatchCanonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim().ToLowerInvariant();
        return normalized switch
        {
            "buğday" or "bugday" or "wheat" => CropType.Wheat,
            "arpa" or "barley" => CropType.Barley,
            "mısır" or "misir" or "corn" => CropType.Corn,
            "ayçiçeği" or "aycicegi" or "sunflower" => CropType.Sunflower,
            "domates" or "tomato" => CropType.Tomato,
            _ => null
        };
    }
}

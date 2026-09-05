namespace TarlaAsistani.Application.Features.Media.Commands;

internal static class MediaFileSignatureValidator
{
    public static bool IsImageSignatureValid(string contentType, byte[] data) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => StartsWith(data, 0xFF, 0xD8, 0xFF),
        "image/png" => StartsWith(data, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
        "image/webp" => data.Length >= 12 &&
                        StartsWith(data, 0x52, 0x49, 0x46, 0x46) &&
                        StartsWith(data, new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8),
        _ => true
    };

    public static bool IsExtensionCompatible(string fileName, string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => HasExtension(fileName, ".jpg", ".jpeg"),
            "image/png" => HasExtension(fileName, ".png"),
            "image/webp" => HasExtension(fileName, ".webp"),
            "audio/mpeg" => HasExtension(fileName, ".mp3"),
            "audio/mp4" or "audio/x-m4a" => HasExtension(fileName, ".mp4", ".m4a"),
            "audio/wav" => HasExtension(fileName, ".wav"),
            "audio/ogg" => HasExtension(fileName, ".ogg"),
            _ => false
        };

    private static bool HasExtension(string fileName, params string[] extensions) =>
        extensions.Any(extension => string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase));

    private static bool StartsWith(byte[] data, params byte[] signature) => StartsWith(data, signature, 0);

    private static bool StartsWith(byte[] data, byte[] signature, int offset)
    {
        if (offset < 0 || data.Length < offset + signature.Length) return false;
        for (var i = 0; i < signature.Length; i++)
        {
            if (data[offset + i] != signature[i]) return false;
        }
        return true;
    }
}

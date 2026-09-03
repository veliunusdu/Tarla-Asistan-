using System.Text.Encodings.Web;
using System.Text.Json;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Provides standardized JSON serialization settings for AI tool definitions, arguments, and result payloads.
/// Employs lowercase snake_case property naming and unescaped Unicode characters for optimal model readability.
/// </summary>
public static class AIToolJsonOptions
{
    /// <summary>
    /// Gets the shared <see cref="JsonSerializerOptions"/> configured with snake_case naming and relaxed Unicode escaping.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };
}

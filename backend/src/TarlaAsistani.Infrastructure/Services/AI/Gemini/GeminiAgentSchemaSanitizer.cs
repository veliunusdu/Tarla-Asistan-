using System.Buffers;
using System.Text.Json;

namespace TarlaAsistani.Infrastructure.Services.AI.Gemini;

/// <summary>
/// Sanitizes provider-independent JSON schemas into a Gemini-compatible OpenAPI subset.
/// Strips non-standard or unsupported keywords (such as additionalProperties, minLength, format: uuid)
/// on the provider outbound copy without mutating original application tool definitions.
/// </summary>
internal static class GeminiAgentSchemaSanitizer
{
    private static readonly HashSet<string> DisallowedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "additionalProperties",
        "$schema",
        "minLength",
        "maxLength",
        "pattern"
    };

    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "float",
        "double",
        "int32",
        "int64",
        "date-time"
    };

    /// <summary>
    /// Returns a new <see cref="JsonElement"/> containing a Gemini-compatible OpenAPI schema.
    /// </summary>
    public static JsonElement Sanitize(JsonElement originalSchema)
    {
        if (originalSchema.ValueKind != JsonValueKind.Object)
        {
            return originalSchema.Clone();
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteSanitizedObject(originalSchema, writer);
        }

        using var doc = JsonDocument.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    private static void WriteSanitizedObject(JsonElement element, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        foreach (var prop in element.EnumerateObject())
        {
            if (DisallowedPropertyNames.Contains(prop.Name))
            {
                continue;
            }

            if (prop.NameEquals("format"))
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var formatStr = prop.Value.GetString();
                    if (formatStr != null && AllowedFormats.Contains(formatStr))
                    {
                        prop.WriteTo(writer);
                    }
                }
                continue;
            }

            if (prop.NameEquals("properties"))
            {
                writer.WritePropertyName("properties");
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach (var childProp in prop.Value.EnumerateObject())
                    {
                        writer.WritePropertyName(childProp.Name);
                        if (childProp.Value.ValueKind == JsonValueKind.Object)
                        {
                            WriteSanitizedObject(childProp.Value, writer);
                        }
                        else
                        {
                            childProp.Value.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
                continue;
            }

            if (prop.NameEquals("items"))
            {
                writer.WritePropertyName("items");
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    WriteSanitizedObject(prop.Value, writer);
                }
                else
                {
                    prop.Value.WriteTo(writer);
                }
                continue;
            }

            // Write all other standard properties (type, description, required, enum, etc.)
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

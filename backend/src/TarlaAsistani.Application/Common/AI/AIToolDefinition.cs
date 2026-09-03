using System.Text.Json;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Represents the definition and parameter schema of a tool/function exposed to an AI agent.
/// </summary>
public sealed record AIToolDefinition
{
    /// <summary>
    /// Gets the unique name of the tool (e.g., "create_task", "get_weather").
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the human-readable description of what the tool does and when it should be called.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the JSON Schema defining the expected parameters for this tool.
    /// </summary>
    public JsonElement ParametersSchema { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIToolDefinition"/>.
    /// </summary>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="parametersSchema">The JSON schema for tool arguments.</param>
    public AIToolDefinition(string name, string description, JsonElement parametersSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name;
        Description = description;
        ParametersSchema = parametersSchema;
    }

    /// <summary>
    /// Creates an <see cref="AIToolDefinition"/> from a raw JSON schema string.
    /// </summary>
    public static AIToolDefinition Create(string name, string description, string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);
        using var document = JsonDocument.Parse(jsonSchema);
        return new AIToolDefinition(name, description, document.RootElement.Clone());
    }

    /// <summary>
    /// Creates an <see cref="AIToolDefinition"/> for a tool that takes no parameters.
    /// </summary>
    public static AIToolDefinition CreateEmpty(string name, string description)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}");
        return new AIToolDefinition(name, description, document.RootElement.Clone());
    }
}

using System.Text.Json;

namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Represents the execution outcome of a tool invoked by the AI agent.
/// </summary>
public sealed record AIToolResult
{
    /// <summary>
    /// Gets the invocation identifier of the corresponding tool call.
    /// </summary>
    public string CallId { get; init; }

    /// <summary>
    /// Gets the name of the executed tool.
    /// </summary>
    public string ToolName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the structured JSON result payload when execution succeeds.
    /// </summary>
    public JsonElement? Result { get; init; }

    /// <summary>
    /// Gets the error message if tool execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets an optional machine-readable error code if tool execution failed (e.g., "unknown_tool", "tool_execution_error").
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="AIToolResult"/>.
    /// </summary>
    public AIToolResult(
        string? callId,
        string toolName,
        bool isSuccess,
        JsonElement? result = null,
        string? errorMessage = null,
        string? errorCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        CallId = callId ?? string.Empty;
        ToolName = toolName;
        IsSuccess = isSuccess;
        Result = result;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a successful tool execution result with a structured <see cref="JsonElement"/>.
    /// </summary>
    public static AIToolResult Success(string? callId, string toolName, JsonElement result) =>
        new(callId, toolName, isSuccess: true, result: result);

    /// <summary>
    /// Creates a successful tool execution result by parsing a JSON string.
    /// </summary>
    public static AIToolResult Success(string? callId, string toolName, string jsonResult)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(jsonResult) ? "{}" : jsonResult);
        return new AIToolResult(callId, toolName, isSuccess: true, result: document.RootElement.Clone());
    }

    /// <summary>
    /// Creates a successful tool execution result by serializing any object.
    /// </summary>
    public static AIToolResult Success<T>(string? callId, string toolName, T data, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(data, options);
        using var document = JsonDocument.Parse(json);
        return new AIToolResult(callId, toolName, isSuccess: true, result: document.RootElement.Clone());
    }

    /// <summary>
    /// Creates a failed tool execution result with an error message and optional machine-readable error code.
    /// </summary>
    public static AIToolResult Failure(string? callId, string toolName, string errorMessage, string? errorCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new AIToolResult(callId, toolName, isSuccess: false, errorMessage: errorMessage, errorCode: errorCode);
    }

    /// <summary>
    /// Returns the result formatted as a string (either the serialized JSON result or error description).
    /// </summary>
    public string GetContentString()
    {
        if (!IsSuccess)
        {
            return ErrorMessage ?? "Unknown tool error";
        }

        return Result.HasValue ? Result.Value.GetRawText() : "{}";
    }
}

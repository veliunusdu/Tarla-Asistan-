namespace TarlaAsistani.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an AI agent orchestration fails (e.g. provider error, empty response, or iteration limit).
/// Encapsulates a machine-readable error code and a safe user-facing message.
/// </summary>
public class AIAgentExecutionException : Exception
{
    public string ErrorCode { get; }

    public AIAgentExecutionException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

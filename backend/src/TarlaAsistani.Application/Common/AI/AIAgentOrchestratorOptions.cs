namespace TarlaAsistani.Application.Common.AI;

/// <summary>
/// Configuration options for the <see cref="IAIAgentOrchestrator"/> multi-turn tool calling loop.
/// </summary>
public sealed record AIAgentOrchestratorOptions
{
    /// <summary>
    /// Conservative default maximum tool-calling loop iterations.
    /// </summary>
    public const int DefaultMaxIterations = 5;

    /// <summary>
    /// Gets the maximum number of tool execution iterations before the orchestrator halts.
    /// Must be at least 1.
    /// </summary>
    public int MaxIterations { get; set; } = DefaultMaxIterations;

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentOrchestratorOptions"/> with default settings.
    /// </summary>
    public AIAgentOrchestratorOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AIAgentOrchestratorOptions"/> with a custom iteration limit.
    /// </summary>
    /// <param name="maxIterations">Maximum loop iterations (at least 1).</param>
    public AIAgentOrchestratorOptions(int maxIterations)
    {
        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "MaxIterations must be at least 1.");
        }

        MaxIterations = maxIterations;
    }
}

namespace TarlaAsistani.Domain.Entities;

public class AiUsageLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Provider { get; set; } = null!;

    public string Model { get; set; } = null!;

    public bool HasPhoto { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public long DurationMs { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}

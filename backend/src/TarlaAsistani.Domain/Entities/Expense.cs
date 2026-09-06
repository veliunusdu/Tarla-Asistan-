using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid CropPeriodId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? CreatedById { get; set; }

    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ClientOperationId { get; set; }

    public Farm Farm { get; set; } = null!;
    public CropPeriod CropPeriod { get; set; } = null!;
    public Activity? Activity { get; set; }
    public User? CreatedBy { get; set; }
}

namespace TarlaAsistani.Domain.Entities;

public sealed class CropSale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid CropPeriodId { get; set; }
    public Guid? CreatedById { get; set; }

    public decimal HarvestQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly SoldAt { get; set; }
    public string? BuyerOrMarketNote { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ClientOperationId { get; set; }

    public Farm Farm { get; set; } = null!;
    public CropPeriod CropPeriod { get; set; } = null!;
    public User? CreatedBy { get; set; }
}

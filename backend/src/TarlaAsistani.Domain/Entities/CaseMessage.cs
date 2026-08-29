using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

public class CaseMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CaseId { get; set; }

    public Guid SenderId { get; set; }

    public CaseMessageType MessageType { get; set; } = CaseMessageType.Comment;

    public string Body { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public SupportCase Case { get; set; } = null!;

    public User Sender { get; set; } = null!;

    public ICollection<CaseMessageMedia> MediaLinks { get; set; } = new List<CaseMessageMedia>();
}

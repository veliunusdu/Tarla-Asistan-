namespace TarlaAsistani.Domain.Entities;

using TarlaAsistani.Domain.Enums;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
}
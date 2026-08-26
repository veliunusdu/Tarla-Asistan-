using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<CropPeriod> CropPeriods => Set<CropPeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- USER CONFIG ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
        });

        // --- FARM CONFIG ---
        modelBuilder.Entity<Farm>(entity =>
        {
            entity.HasKey(f => f.Id);

            // Composite index for listing and duplicate name warnings
            entity.HasIndex(f => new { f.OwnerId, f.ArchivedAt });
            entity.HasIndex(f => new { f.OwnerId, f.Name });

            entity.Property(f => f.Name).HasMaxLength(120).IsRequired();

            // Relationships
            entity.HasOne(f => f.Owner)
                  .WithMany(u => u.Farms)
                  .HasForeignKey(f => f.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // --- CROP PERIOD CONFIG ---
        modelBuilder.Entity<CropPeriod>(entity =>
        {
            entity.HasKey(cp => cp.Id);

            // Database rule: A farm can only have ONE active crop period at a time
            // We use a filtered index (PostgreSQL partial index) for this
            entity.HasIndex(cp => new { cp.FarmId, cp.Status })
                  .HasFilter("\"Status\" = 0") // 0 represents 'Active' in our Enum
                  .IsUnique();

            entity.HasOne(cp => cp.Farm)
                  .WithMany(f => f.CropPeriods)
                  .HasForeignKey(cp => cp.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
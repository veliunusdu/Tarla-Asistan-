using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Farm> Farms => Set<Farm>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Farm>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.FarmerPhoneNumber)
                .HasMaxLength(30)
                .IsRequired();
        });
    }
}

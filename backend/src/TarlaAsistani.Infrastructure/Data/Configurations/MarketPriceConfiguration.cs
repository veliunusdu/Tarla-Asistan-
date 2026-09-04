using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Infrastructure.Data.Configurations;

/// <summary>
/// <see cref="MarketPrice"/> varlığı için Entity Framework Core Fluent API yapılandırması.
/// </summary>
public class MarketPriceConfiguration : IEntityTypeConfiguration<MarketPrice>
{
    public void Configure(EntityTypeBuilder<MarketPrice> builder)
    {
        builder.ToTable("market_prices");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Code)
               .IsUnique();

        builder.Property(x => x.Code)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.Name)
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(x => x.Category)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.CurrentPrice)
               .HasPrecision(18, 4)
               .IsRequired();

        builder.Property(x => x.PreviousPrice)
               .HasPrecision(18, 4)
               .IsRequired();

        builder.Property(x => x.ChangePercent)
               .HasPrecision(6, 2)
               .IsRequired();

        builder.Property(x => x.Unit)
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.Source)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
               .IsRequired();

        // Hesaplanmış yön bilgisini veritabanı sütunundan muaf tut
        builder.Ignore(x => x.ChangeDirection);

        // Başlangıç tohum (seed) verilerini ekle
        builder.HasData(GetInitialSeedData());
    }

    /// <summary>
    /// 2024 Türkiye piyasa koşullarına uygun 8 temel piyasa kaleminin başlangıç tohum verilerini üretir.
    /// </summary>
    public static List<MarketPrice> GetInitialSeedData()
    {
        // EF Core HasData için deterministik (sabit) GUID'ler ve UTC tarih
        var seedTime = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        return
        [
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                Code = "DIESEL",
                Name = "Motorin (Mazot)",
                Category = MarketCategory.Fuel,
                CurrentPrice = 44.85m,
                PreviousPrice = 44.20m,
                ChangePercent = 1.47m,
                Unit = "TL/Lt",
                Source = "EPDK",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                Code = "GASOLINE",
                Name = "Benzin (95 Oktan)",
                Category = MarketCategory.Fuel,
                CurrentPrice = 43.20m,
                PreviousPrice = 43.20m,
                ChangePercent = 0.00m,
                Unit = "TL/Lt",
                Source = "EPDK",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                Code = "UREA",
                Name = "Üre Gübresi (%46 N)",
                Category = MarketCategory.Fertilizer,
                CurrentPrice = 14250.00m,
                PreviousPrice = 14500.00m,
                ChangePercent = -1.72m,
                Unit = "TL/Ton",
                Source = "GUBRETAS",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                Code = "DAP",
                Name = "DAP Gübresi (18-46-0)",
                Category = MarketCategory.Fertilizer,
                CurrentPrice = 20800.00m,
                PreviousPrice = 20800.00m,
                ChangePercent = 0.00m,
                Unit = "TL/Ton",
                Source = "GUBRETAS",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                Code = "WHEAT",
                Name = "Ekmeklik Buğday",
                Category = MarketCategory.Crop,
                CurrentPrice = 9850.00m,
                PreviousPrice = 9700.00m,
                ChangePercent = 1.55m,
                Unit = "TL/Ton",
                Source = "TURIB",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                Code = "CORN",
                Name = "Mısır (1. Sınıf)",
                Category = MarketCategory.Crop,
                CurrentPrice = 8200.00m,
                PreviousPrice = 8250.00m,
                ChangePercent = -0.61m,
                Unit = "TL/Ton",
                Source = "TURIB",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111107"),
                Code = "USD_TRY",
                Name = "Dolar",
                Category = MarketCategory.Fx,
                CurrentPrice = 34.2200m,
                PreviousPrice = 34.1800m,
                ChangePercent = 0.12m,
                Unit = "TL",
                Source = "TCMB",
                UpdatedAtUtc = seedTime
            },
            new MarketPrice
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111108"),
                Code = "EUR_TRY",
                Name = "Euro",
                Category = MarketCategory.Fx,
                CurrentPrice = 37.9500m,
                PreviousPrice = 38.0200m,
                ChangePercent = -0.18m,
                Unit = "TL",
                Source = "TCMB",
                UpdatedAtUtc = seedTime
            }
        ];
    }
}

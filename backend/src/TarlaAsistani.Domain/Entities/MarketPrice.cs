using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Entities;

/// <summary>
/// Çiftçiler için takip edilen akaryakıt, gübre, mahsul veya döviz gibi piyasa fiyat verilerini temsil eder.
/// </summary>
public class MarketPrice
{
    /// <summary>
    /// Kaydın benzersiz kimliği.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Piyasa kaleminin benzersiz sistem kodu (örn: DIESEL, GASOLINE, UREA, DAP, WHEAT, CORN, USD_TRY, EUR_TRY).
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Kullanıcıya gösterilen ürün veya parite adı (örn: "Motorin (Mazot)", "Üre Gübresi (%46 N)").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Ürünün piyasa kategorisi (Akaryakıt, Gübre, Mahsul, Döviz).
    /// </summary>
    public MarketCategory Category { get; set; }

    /// <summary>
    /// Güncel birim fiyatı (TL cinsinden).
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Bir önceki günün kapanış veya referans fiyatı (TL cinsinden).
    /// </summary>
    public decimal PreviousPrice { get; set; }

    /// <summary>
    /// Günlük değişim yüzdesi (örn: 1.45, -0.80).
    /// </summary>
    public decimal ChangePercent { get; set; }

    /// <summary>
    /// Fiyatlandırma birimi (örn: "TL/Lt", "TL/Ton", "TL").
    /// </summary>
    public string Unit { get; set; } = null!;

    /// <summary>
    /// Verinin temin edildiği kaynak kurum veya sağlayıcı (örn: "TCMB", "EPDK", "TURIB", "GUBRETAS").
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// Fiyat verisinin son güncellenme zamanı (UTC).
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Günlük değişim yüzdesine göre yön göstergesi ("up", "down", "neutral").
    /// </summary>
    public string ChangeDirection => ChangePercent switch
    {
        > 0 => "up",
        < 0 => "down",
        _ => "neutral"
    };
}

namespace TarlaAsistani.Domain.Enums;

/// <summary>
/// Tarımsal piyasa verilerinin kategorilerini temsil eder.
/// </summary>
public enum MarketCategory
{
    /// <summary>
    /// Akaryakıt ürünleri (Motorin, Benzin vb.).
    /// </summary>
    Fuel = 1,

    /// <summary>
    /// Gübre ürünleri (Üre, DAP vb.).
    /// </summary>
    Fertilizer = 2,

    /// <summary>
    /// Mahsul ve hububat ürünleri (Ekmeklik Buğday, Mısır vb.).
    /// </summary>
    Crop = 3,

    /// <summary>
    /// Döviz kurları (USD/TRY, EUR/TRY vb.).
    /// </summary>
    Fx = 4
}

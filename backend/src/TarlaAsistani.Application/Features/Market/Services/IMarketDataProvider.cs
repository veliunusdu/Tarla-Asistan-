using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Market.Services;

/// <summary>
/// Harici kaynaklardan veya piyasa bültenlerinden güncel fiyat verilerini toplayan sağlayıcılar için ortak arayüz.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Bu sağlayıcının belirtilen piyasa kategorisini destekleyip desteklemediğini doğrular.
    /// </summary>
    /// <param name="category">Kontrol edilecek piyasa kategorisi.</param>
    /// <returns>Kategori destekleniyorsa true, aksi halde false.</returns>
    Task<bool> CanHandleAsync(MarketCategory category);

    /// <summary>
    /// Belirtilen kategoriye ait en güncel piyasa fiyatlarını harici kaynaktan çeker.
    /// </summary>
    /// <remarks>
    /// Dönen <see cref="MarketPrice"/> nesnelerinde Code, Name, Category, CurrentPrice, Unit, Source ve UpdatedAtUtc doldurulmalıdır.
    /// PreviousPrice ve ChangePercent hesaplaması senkronizasyon servisi tarafından yönetilir.
    /// </remarks>
    /// <param name="category">Sorgulanacak kategori.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Taze piyasa verisi varlıkları koleksiyonu.</returns>
    Task<IEnumerable<MarketPrice>> FetchAsync(MarketCategory category, CancellationToken ct = default);
}

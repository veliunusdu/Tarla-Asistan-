using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Queries;

public sealed class ListCropSalesQueryHandler : IRequestHandler<ListCropSalesQuery, IReadOnlyList<CropSaleDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCropSalesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CropSaleDto>> Handle(ListCropSalesQuery request, CancellationToken cancellationToken)
    {
        var scope = await FinancialScope.LoadAsync(_db, request.FarmId, request.CropPeriodId, request.UserId, cancellationToken);
        if (scope is null) throw new KeyNotFoundException("Tarla veya üretim dönemi bulunamadı.");

        return await _db.CropSales.AsNoTracking()
            .Where(s => s.FarmId == request.FarmId && s.CropPeriodId == request.CropPeriodId && s.ArchivedAtUtc == null)
            .OrderByDescending(s => s.SoldAt).ThenByDescending(s => s.Id)
            .Select(s => new CropSaleDto(s.Id, s.FarmId, s.CropPeriodId, s.HarvestQuantity, s.Unit,
                s.UnitPrice, s.TotalAmount, s.SoldAt, s.BuyerOrMarketNote, s.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

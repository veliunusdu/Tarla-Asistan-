using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class UpdateCropSaleCommandHandler : IRequestHandler<UpdateCropSaleCommand, CropSaleDto?>
{
    private readonly IApplicationDbContext _db;

    public UpdateCropSaleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CropSaleDto?> Handle(UpdateCropSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _db.CropSales.FirstOrDefaultAsync(
            s => s.Id == request.SaleId && s.CreatedById == request.UserId && s.ArchivedAtUtc == null,
            cancellationToken);
        if (sale is null) return null;
        var farm = await _db.Farms.FirstOrDefaultAsync(
            f => f.Id == sale.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null,
            cancellationToken);
        if (farm is null) return null;

        sale.HarvestQuantity = request.HarvestQuantity;
        sale.Unit = request.Unit.Trim();
        sale.UnitPrice = request.UnitPrice;
        sale.TotalAmount = FinancialMath.CalculateSaleTotal(request.HarvestQuantity, request.UnitPrice);
        sale.SoldAt = request.SoldAt;
        sale.BuyerOrMarketNote = string.IsNullOrWhiteSpace(request.BuyerOrMarketNote) ? null : request.BuyerOrMarketNote.Trim();
        sale.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return CropSaleDtoMapper.Map(sale);
    }
}

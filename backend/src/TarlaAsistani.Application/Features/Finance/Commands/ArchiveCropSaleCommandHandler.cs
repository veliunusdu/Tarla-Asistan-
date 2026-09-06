using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class ArchiveCropSaleCommandHandler : IRequestHandler<ArchiveCropSaleCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public ArchiveCropSaleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<bool> Handle(ArchiveCropSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _db.CropSales.FirstOrDefaultAsync(
            s => s.Id == request.SaleId && s.CreatedById == request.UserId && s.ArchivedAtUtc == null,
            cancellationToken);
        if (sale is null) return false;
        var farm = await _db.Farms.FirstOrDefaultAsync(
            f => f.Id == sale.FarmId && f.OwnerId == request.UserId && f.ArchivedAt == null,
            cancellationToken);
        if (farm is null) return false;

        sale.ArchivedAtUtc = DateTime.UtcNow;
        sale.UpdatedAtUtc = sale.ArchivedAtUtc.Value;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

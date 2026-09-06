using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class CreateCropSaleCommandHandler : IRequestHandler<CreateCropSaleCommand, CropSaleDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCropSaleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CropSaleDto> Handle(CreateCropSaleCommand request, CancellationToken cancellationToken)
    {
        var scope = await FinancialScope.LoadAsync(_db, request.FarmId, request.CropPeriodId, request.UserId, cancellationToken);
        if (scope is null) throw new KeyNotFoundException("Tarla veya üretim dönemi bulunamadı.");

        if (request.ClientOperationId.HasValue)
        {
            var existing = await _db.CropSales.FirstOrDefaultAsync(
                s => s.CreatedById == request.UserId && s.ClientOperationId == request.ClientOperationId,
                cancellationToken);
            if (existing is not null) return CropSaleDtoMapper.Map(existing);
        }

        var now = DateTime.UtcNow;
        var sale = new CropSale
        {
            FarmId = request.FarmId,
            CropPeriodId = request.CropPeriodId,
            CreatedById = request.UserId,
            HarvestQuantity = request.HarvestQuantity,
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            TotalAmount = FinancialMath.CalculateSaleTotal(request.HarvestQuantity, request.UnitPrice),
            SoldAt = request.SoldAt,
            BuyerOrMarketNote = string.IsNullOrWhiteSpace(request.BuyerOrMarketNote) ? null : request.BuyerOrMarketNote.Trim(),
            ClientOperationId = request.ClientOperationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.CropSales.Add(sale);
        await _db.SaveChangesAsync(cancellationToken);
        return CropSaleDtoMapper.Map(sale);
    }
}

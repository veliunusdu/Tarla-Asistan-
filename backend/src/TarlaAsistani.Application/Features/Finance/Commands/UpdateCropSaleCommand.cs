using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record UpdateCropSaleCommand(
    Guid SaleId,
    Guid UserId,
    decimal HarvestQuantity,
    string Unit,
    decimal UnitPrice,
    DateOnly SoldAt,
    string? BuyerOrMarketNote = null) : IRequest<CropSaleDto?>;

public sealed class UpdateCropSaleCommandValidator : AbstractValidator<UpdateCropSaleCommand>
{
    public UpdateCropSaleCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.HarvestQuantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.UnitPrice).GreaterThan(0);
        RuleFor(x => x.BuyerOrMarketNote).MaximumLength(500);
    }
}

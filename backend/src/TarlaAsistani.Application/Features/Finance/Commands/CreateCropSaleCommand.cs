using FluentValidation;
using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record CreateCropSaleCommand(
    Guid FarmId,
    Guid CropPeriodId,
    Guid UserId,
    decimal HarvestQuantity,
    string Unit,
    decimal UnitPrice,
    DateOnly SoldAt,
    string? BuyerOrMarketNote = null,
    Guid? ClientOperationId = null) : IRequest<CropSaleDto>;

public sealed class CreateCropSaleCommandValidator : AbstractValidator<CreateCropSaleCommand>
{
    public CreateCropSaleCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.CropPeriodId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.HarvestQuantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.UnitPrice).GreaterThan(0);
        RuleFor(x => x.BuyerOrMarketNote).MaximumLength(500);
        RuleFor(x => x.ClientOperationId).Must(id => !id.HasValue || id.Value != Guid.Empty);
    }
}

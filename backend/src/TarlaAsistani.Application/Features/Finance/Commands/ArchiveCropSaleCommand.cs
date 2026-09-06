using MediatR;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public record ArchiveCropSaleCommand(Guid SaleId, Guid UserId) : IRequest<bool>;

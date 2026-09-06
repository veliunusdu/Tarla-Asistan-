using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Queries;

public record ListExpensesQuery(Guid FarmId, Guid CropPeriodId, Guid UserId) : IRequest<IReadOnlyList<ExpenseDto>>;

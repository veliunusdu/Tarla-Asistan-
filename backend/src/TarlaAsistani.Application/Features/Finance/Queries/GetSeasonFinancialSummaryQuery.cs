using MediatR;
using TarlaAsistani.Application.Features.Finance.DTOs;

namespace TarlaAsistani.Application.Features.Finance.Queries;

public record GetSeasonFinancialSummaryQuery(Guid FarmId, Guid CropPeriodId, Guid UserId) : IRequest<FinancialSummaryDto>;

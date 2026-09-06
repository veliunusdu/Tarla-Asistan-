using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Finance.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Features.Finance.Commands;

public sealed class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, ExpenseDto>
{
    private readonly IApplicationDbContext _db;

    public CreateExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ExpenseDto> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var scope = await FinancialScope.LoadAsync(_db, request.FarmId, request.CropPeriodId, request.UserId, cancellationToken);
        if (scope is null) throw new KeyNotFoundException("Tarla veya üretim dönemi bulunamadı.");

        if (request.ClientOperationId.HasValue)
        {
            var existing = await _db.Expenses.FirstOrDefaultAsync(
                e => e.CreatedById == request.UserId && e.ClientOperationId == request.ClientOperationId,
                cancellationToken);
            if (existing is not null) return ExpenseDtoMapper.Map(existing);
        }

        var now = DateTime.UtcNow;
        var expense = new Expense
        {
            FarmId = request.FarmId,
            CropPeriodId = request.CropPeriodId,
            CreatedById = request.UserId,
            Category = request.Category,
            Amount = request.Amount,
            OccurredAtUtc = request.OccurredAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.OccurredAtUtc, DateTimeKind.Utc)
                : request.OccurredAtUtc.ToUniversalTime(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ClientOperationId = request.ClientOperationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseDtoMapper.Map(expense);
    }
}

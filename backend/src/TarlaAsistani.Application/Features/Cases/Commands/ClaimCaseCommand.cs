using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public record ClaimCaseCommand(
    Guid CaseId,
    Guid ExpertId
) : IRequest<CaseDetailDto>;

public class ClaimCaseCommandValidator : AbstractValidator<ClaimCaseCommand>
{
    public ClaimCaseCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty().WithMessage("Vaka kimliği gereklidir.");
        RuleFor(x => x.ExpertId).NotEmpty().WithMessage("Uzman kimliği gereklidir.");
    }
}

public class ClaimCaseCommandHandler : IRequestHandler<ClaimCaseCommand, CaseDetailDto>
{
    private readonly IApplicationDbContext _db;

    public ClaimCaseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseDetailDto> Handle(ClaimCaseCommand request, CancellationToken cancellationToken)
    {
        var sc = await _db.SupportCases
            .Include(c => c.Farm)
            .Include(c => c.MediaLinks).ThenInclude(ml => ml.Media)
            .Include(c => c.Messages).ThenInclude(m => m.MediaLinks).ThenInclude(ml => ml.Media)
            .Include(c => c.Context)
            .FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException("Vaka bulunamadı.");

        TarlaAsistani.Application.Features.Cases.Services.CaseClaimWorkflow.ApplyClaim(sc, request.ExpertId, _db, "Claimed by expert");

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await TarlaAsistani.Application.Features.Cases.Services.CaseClaimWorkflow.HandleConcurrencyConflictAsync(_db, request.CaseId, request.ExpertId, cancellationToken);
            return CaseDetailDto.FromEntity(fresh);
        }

        return CaseDetailDto.FromEntity(sc);
    }
}

using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class CreateCaseCommandHandler : IRequestHandler<CreateCaseCommand, CaseDetailDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCaseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseDetailDto> Handle(CreateCaseCommand request, CancellationToken cancellationToken)
    {
        // 0. Idempotency check
        var payloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.FarmId}:{request.Category}:{request.Title.Trim()}:{request.Description.Trim()}"))).ToLowerInvariant();

        if (request.ClientOperationId.HasValue)
        {
            var existingOp = await _db.ClientOperations
                .FirstOrDefaultAsync(co => co.ActorId == request.CreatedById && co.ClientOperationId == request.ClientOperationId.Value, cancellationToken);

            if (existingOp != null)
            {
                if (existingOp.Scope != "case.create" || existingOp.PayloadHash != payloadDigest)
                {
                    throw new InvalidOperationException("Bu client_operation_id farklı bir işlem için kullanılmış.");
                }

                var replayed = await _db.SupportCases
                    .Include(sc => sc.Farm)
                    .Include(sc => sc.MediaLinks)
                        .ThenInclude(ml => ml.Media)
                    .Include(sc => sc.Messages)
                    .FirstOrDefaultAsync(sc => sc.Id == existingOp.ResourceId, cancellationToken);

                if (replayed != null)
                {
                    return CaseDetailDto.FromEntity(replayed);
                }
            }
        }

        // 1. Verify Farm ownership and not archived
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.OwnerId == request.CreatedById && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        // 2. Verify media items belong to user
        var mediaIds = request.MediaIds?.Distinct().ToList() ?? new List<Guid>();
        if (mediaIds.Count > 0)
        {
            var ownedMediaCount = await _db.MediaAssets
                .CountAsync(m => mediaIds.Contains(m.Id) && m.OwnerId == request.CreatedById, cancellationToken);

            if (ownedMediaCount != mediaIds.Count)
            {
                throw new ArgumentException("Medya bulunamadı veya bu kullanıcıya ait değil.");
            }
        }

        // 3. Create SupportCase entity
        var now = DateTime.UtcNow;
        var supportCase = new SupportCase
        {
            FarmId = farm.Id,
            CreatedById = request.CreatedById,
            Category = request.Category,
            Priority = CasePriority.Medium,
            Status = CaseStatus.Open,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.SupportCases.Add(supportCase);

        foreach (var mediaId in mediaIds)
        {
            supportCase.MediaLinks.Add(new CaseMedia
            {
                CaseId = supportCase.Id,
                MediaId = mediaId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 3.1 Record idempotency operation
        if (request.ClientOperationId.HasValue)
        {
            _db.ClientOperations.Add(new ClientOperation
            {
                ActorId = request.CreatedById,
                ClientOperationId = request.ClientOperationId.Value,
                Scope = "case.create",
                PayloadHash = payloadDigest,
                ResourceType = "case",
                ResourceId = supportCase.Id,
                CreatedAtUtc = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 4. Return full CaseDetailDto with loaded media and farm
        var created = await _db.SupportCases
            .Include(sc => sc.Farm)
            .Include(sc => sc.MediaLinks)
                .ThenInclude(ml => ml.Media)
            .Include(sc => sc.Messages)
            .FirstAsync(sc => sc.Id == supportCase.Id, cancellationToken);

        return CaseDetailDto.FromEntity(created);
    }
}

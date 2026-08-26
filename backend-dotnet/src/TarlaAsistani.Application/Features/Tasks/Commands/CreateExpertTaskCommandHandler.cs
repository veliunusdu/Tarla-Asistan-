using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Tasks.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus;

namespace TarlaAsistani.Application.Features.Tasks.Commands;

public class CreateExpertTaskCommandHandler : IRequestHandler<CreateExpertTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _db;

    public CreateExpertTaskCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TaskDto> Handle(CreateExpertTaskCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Farm exists and is not archived
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId && f.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Tarla bulunamadı.");

        // 2. Resolve CropPeriodId
        Guid? resolvedCropPeriodId = request.CropPeriodId;
        if (resolvedCropPeriodId == null)
        {
            var activeCrop = await _db.CropPeriods
                .FirstOrDefaultAsync(cp => cp.FarmId == request.FarmId && cp.Status == CropPeriodStatus.Active, cancellationToken);
            resolvedCropPeriodId = activeCrop?.Id;
        }
        else
        {
            var cropExists = await _db.CropPeriods
                .AnyAsync(cp => cp.Id == resolvedCropPeriodId.Value && cp.FarmId == request.FarmId, cancellationToken);

            if (!cropExists)
            {
                throw new ArgumentException("Üretim dönemi bu tarlaya ait değil.");
            }
        }

        // 3. Generate DedupeKey
        var dedupeRaw = $"EXPERT|{request.Title.Trim()}|{request.Description.Trim()}|{request.Reason.Trim()}|{resolvedCropPeriodId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(dedupeRaw));
        var dedupeKey = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (dedupeKey.Length > 64)
        {
            dedupeKey = dedupeKey[..64];
        }

        // 4. Create FarmTask
        var now = DateTime.UtcNow;
        var task = new FarmTask
        {
            FarmId = farm.Id,
            CropPeriodId = resolvedCropPeriodId,
            CreatedById = request.CreatedById,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Reason = request.Reason.Trim(),
            Priority = request.Priority,
            Status = TaskStatus.New,
            Source = TaskSource.Expert,
            Confidence = request.Confidence,
            DueDate = request.DueDate,
            DedupeKey = dedupeKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FarmTasks.Add(task);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Aynı görev bu tarla ve tarih için zaten mevcut.");
        }

        return TaskDto.FromEntity(task);
    }
}

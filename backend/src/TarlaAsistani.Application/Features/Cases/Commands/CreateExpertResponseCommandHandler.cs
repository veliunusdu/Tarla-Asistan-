using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class CreateExpertResponseCommandHandler : IRequestHandler<CreateExpertResponseCommand, CaseDetailDto>
{
    private readonly IApplicationDbContext _db;

    public CreateExpertResponseCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CaseDetailDto> Handle(CreateExpertResponseCommand request, CancellationToken cancellationToken)
    {
        if (request.Role != UserRole.Agronomist)
        {
            throw new UnauthorizedAccessException("Yalnızca uzman cevap oluşturabilir.");
        }

        // 0. Idempotency check
        var payloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.CaseId}:{request.Body.Trim()}:{request.CloseCase}"))).ToLowerInvariant();

        if (request.ClientOperationId.HasValue)
        {
            var existingOp = await _db.ClientOperations
                .FirstOrDefaultAsync(co => co.ActorId == request.ExpertId && co.ClientOperationId == request.ClientOperationId.Value, cancellationToken);

            if (existingOp != null)
            {
                if (existingOp.Scope != $"case.expert-response:{request.CaseId}" || existingOp.PayloadHash != payloadDigest)
                {
                    throw new InvalidOperationException("Bu client_operation_id farklı bir işlem için kullanılmış.");
                }

                var replayedCase = await _db.SupportCases
                    .Include(sc => sc.Farm)
                    .Include(sc => sc.MediaLinks)
                        .ThenInclude(ml => ml.Media)
                    .Include(sc => sc.Messages)
                        .ThenInclude(m => m.MediaLinks)
                            .ThenInclude(ml => ml.Media)
                    .FirstOrDefaultAsync(sc => sc.Id == request.CaseId, cancellationToken);

                if (replayedCase != null)
                {
                    return CaseDetailDto.FromEntity(replayedCase);
                }
            }
        }

        var supportCase = await _db.SupportCases
            .Include(sc => sc.Farm)
            .Include(sc => sc.MediaLinks)
                .ThenInclude(ml => ml.Media)
            .Include(sc => sc.Messages)
                .ThenInclude(m => m.MediaLinks)
                    .ThenInclude(ml => ml.Media)
            .FirstOrDefaultAsync(sc => sc.Id == request.CaseId && sc.Farm.ArchivedAt == null, cancellationToken)
            ?? throw new KeyNotFoundException("Vaka bulunamadı.");

        if (supportCase.Status == CaseStatus.Closed)
        {
            throw new InvalidOperationException("Kapalı vakaya cevap eklenemez.");
        }

        // Verify media ownership
        var mediaIds = request.MediaIds?.Distinct().ToList() ?? new List<Guid>();
        if (mediaIds.Count > 0)
        {
            var ownedMediaCount = await _db.MediaAssets
                .CountAsync(m => mediaIds.Contains(m.Id) && m.OwnerId == request.ExpertId, cancellationToken);

            if (ownedMediaCount != mediaIds.Count)
            {
                throw new ArgumentException("Medya bulunamadı veya bu kullanıcıya ait değil.");
            }
        }

        var now = DateTime.UtcNow;
        var message = new CaseMessage
        {
            CaseId = supportCase.Id,
            SenderId = request.ExpertId,
            MessageType = CaseMessageType.ExpertResponse,
            Body = request.Body.Trim(),
            CreatedAtUtc = now
        };

        _db.CaseMessages.Add(message);

        foreach (var mediaId in mediaIds)
        {
            message.MediaLinks.Add(new CaseMessageMedia
            {
                MessageId = message.Id,
                MediaId = mediaId
            });
        }

        supportCase.Status = request.CloseCase ? CaseStatus.Closed : CaseStatus.Answered;
        supportCase.ClosedAtUtc = request.CloseCase ? now : null;
        supportCase.AssignedExpertId = request.ExpertId;
        supportCase.UpdatedAtUtc = now;

        // Notification to farmer
        var notification = new Notification
        {
            UserId = supportCase.Farm.OwnerId,
            NotificationType = NotificationType.ExpertResponse,
            Title = "Uzmanınız vakanızı yanıtladı",
            Body = request.Body.Length > 300 ? request.Body[..300] : request.Body,
            DeepLink = $"tarla-asistani://cases/{supportCase.Id}",
            Data = $"{{\"case_id\":\"{supportCase.Id}\",\"message_id\":\"{message.Id}\"}}",
            DedupeKey = $"expert-response:{message.Id}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        // Record idempotency operation
        if (request.ClientOperationId.HasValue)
        {
            _db.ClientOperations.Add(new ClientOperation
            {
                ActorId = request.ExpertId,
                ClientOperationId = request.ClientOperationId.Value,
                Scope = $"case.expert-response:{request.CaseId}",
                PayloadHash = payloadDigest,
                ResourceType = "case",
                ResourceId = supportCase.Id,
                CreatedAtUtc = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return CaseDetailDto.FromEntity(supportCase);
    }
}

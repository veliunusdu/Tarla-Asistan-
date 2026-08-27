using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Cases.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Cases.Commands;

public class CreateCaseMessageCommandHandler : IRequestHandler<CreateCaseMessageCommand, CaseMessageDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPushNotificationService _pushService;

    public CreateCaseMessageCommandHandler(IApplicationDbContext db, IPushNotificationService pushService)
    {
        _db = db;
        _pushService = pushService;
    }

    public async Task<CaseMessageDto> Handle(CreateCaseMessageCommand request, CancellationToken cancellationToken)
    {
        // 1. Check farmer message type constraint
        if (request.Role == UserRole.Farmer && request.MessageType != CaseMessageType.Comment)
        {
            throw new UnauthorizedAccessException("Bu mesaj türü yalnızca uzmana açıktır.");
        }

        // 2. Fetch accessible case
        var query = _db.SupportCases
            .Include(sc => sc.Farm)
                .ThenInclude(f => f.Owner)
                    .ThenInclude(u => u.Profile)
            .Where(sc => sc.Id == request.CaseId && sc.Farm.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(sc => sc.Farm.OwnerId == request.SenderId);
        }

        var supportCase = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Vaka bulunamadı.");

        if (supportCase.Status == CaseStatus.Closed)
        {
            throw new InvalidOperationException("Kapalı vakaya mesaj eklenemez.");
        }

        // 3. Verify media ownership
        var mediaIds = request.MediaIds?.Distinct().ToList() ?? new List<Guid>();
        if (mediaIds.Count > 0)
        {
            var ownedMediaCount = await _db.MediaAssets
                .CountAsync(m => mediaIds.Contains(m.Id) && m.OwnerId == request.SenderId, cancellationToken);

            if (ownedMediaCount != mediaIds.Count)
            {
                throw new ArgumentException("Medya bulunamadı veya bu kullanıcıya ait değil.");
            }
        }

        // 4. Create CaseMessage
        var now = DateTime.UtcNow;
        var message = new CaseMessage
        {
            CaseId = supportCase.Id,
            SenderId = request.SenderId,
            MessageType = request.MessageType,
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

        // 5. Apply status side effects
        if (request.MessageType == CaseMessageType.AdditionalInfoRequest)
        {
            supportCase.Status = CaseStatus.WaitingFarmer;
            supportCase.AssignedExpertId = request.SenderId;
        }
        else if (request.MessageType == CaseMessageType.ExpertResponse)
        {
            supportCase.Status = CaseStatus.Answered;
            supportCase.AssignedExpertId = request.SenderId;
        }
        else if (request.Role == UserRole.Farmer && supportCase.Status == CaseStatus.WaitingFarmer)
        {
            supportCase.Status = CaseStatus.InReview;
        }

        supportCase.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        // 6. Notify farmer if expert response or additional info request
        if ((request.MessageType == CaseMessageType.ExpertResponse || request.MessageType == CaseMessageType.AdditionalInfoRequest) &&
            (supportCase.Farm?.Owner?.Profile?.NotificationsEnabled ?? true))
        {
            var isExpertResponse = request.MessageType == CaseMessageType.ExpertResponse;
            var notification = new Notification
            {
                UserId = supportCase.Farm.OwnerId,
                NotificationType = NotificationType.ExpertResponse,
                Title = isExpertResponse ? "Uzmanınız vakanızı yanıtladı" : "Uzmanınız ek bilgi talep etti",
                Body = request.Body.Length > 300 ? request.Body[..300] : request.Body,
                DeepLink = $"tarla-asistani://cases/{supportCase.Id}",
                Data = $"{{\"case_id\":\"{supportCase.Id}\",\"message_id\":\"{message.Id}\"}}",
                DedupeKey = $"case-msg:{message.Id}",
                Status = NotificationStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(cancellationToken);

            var activeTokens = await _db.DeviceTokens
                .Where(d => d.UserId == supportCase.Farm.OwnerId && d.Active)
                .ToListAsync(cancellationToken);

            foreach (var device in activeTokens)
            {
                var sent = await _pushService.SendNotificationAsync(notification, device.Token, cancellationToken);
                if (sent)
                {
                    notification.Status = NotificationStatus.Sent;
                    notification.SentAtUtc = DateTime.UtcNow;
                }
                else
                {
                    notification.AttemptCount++;
                }
                notification.UpdatedAtUtc = DateTime.UtcNow;
            }

            if (activeTokens.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var created = await _db.CaseMessages
            .Include(m => m.MediaLinks)
                .ThenInclude(ml => ml.Media)
            .FirstAsync(m => m.Id == message.Id, cancellationToken);

        return CaseMessageDto.FromEntity(created);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Media.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Media.Queries;

public class GetMediaContentQueryHandler : IRequestHandler<GetMediaContentQuery, MediaContentDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediaStorageService _storage;

    public GetMediaContentQueryHandler(IApplicationDbContext db, IMediaStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<MediaContentDto?> Handle(GetMediaContentQuery request, CancellationToken cancellationToken)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);
        if (asset == null)
        {
            return null;
        }

        var hasAccess = await CanAccessMediaAsync(asset, request.UserId, request.Role, cancellationToken);
        if (!hasAccess)
        {
            return null;
        }

        var content = await _storage.LoadAsync(asset.StorageKey, cancellationToken);
        return new MediaContentDto(content, asset.ContentType);
    }

    private async Task<bool> CanAccessMediaAsync(MediaAsset asset, Guid userId, UserRole role, CancellationToken ct)
    {
        if (asset.OwnerId == userId)
        {
            return true;
        }

        if (role == UserRole.Agronomist)
        {
            var isCaseMedia = await _db.CaseMedia.AnyAsync(cm => cm.MediaId == asset.Id, ct);
            if (isCaseMedia) return true;

            var isMessageMedia = await _db.CaseMessageMedia.AnyAsync(cmm => cmm.MediaId == asset.Id, ct);
            return isMessageMedia;
        }

        // Farmer check: attached to a support case on user's own farm
        var attachedToFarmerCase = await (
            from sc in _db.SupportCases
            join f in _db.Farms on sc.FarmId equals f.Id
            where f.OwnerId == userId && f.ArchivedAt == null
            where _db.CaseMedia.Any(cm => cm.CaseId == sc.Id && cm.MediaId == asset.Id) ||
                  _db.CaseMessages.Any(cm => cm.CaseId == sc.Id && _db.CaseMessageMedia.Any(cmm => cmm.MessageId == cm.Id && cmm.MediaId == asset.Id))
            select sc.Id
        ).AnyAsync(ct);

        return attachedToFarmerCase;
    }
}

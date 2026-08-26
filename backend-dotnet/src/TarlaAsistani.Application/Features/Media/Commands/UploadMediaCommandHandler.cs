using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Media.DTOs;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Media.Commands;

public class UploadMediaCommandHandler : IRequestHandler<UploadMediaCommand, MediaAssetDto>
{
    private static readonly Dictionary<string, (MediaKind Kind, string Ext)> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = (MediaKind.Image, ".jpg"),
        ["image/png"] = (MediaKind.Image, ".png"),
        ["image/webp"] = (MediaKind.Image, ".webp"),
        ["audio/mpeg"] = (MediaKind.Audio, ".mp3"),
        ["audio/mp4"] = (MediaKind.Audio, ".m4a"),
        ["audio/x-m4a"] = (MediaKind.Audio, ".m4a"),
        ["audio/wav"] = (MediaKind.Audio, ".wav"),
        ["audio/ogg"] = (MediaKind.Audio, ".ogg"),
    };

    private readonly IApplicationDbContext _db;
    private readonly IMediaStorageService _storage;

    public UploadMediaCommandHandler(IApplicationDbContext db, IMediaStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<MediaAssetDto> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify User exists
        var userExists = await _db.Users.AnyAsync(u => u.Id == request.OwnerId, cancellationToken);
        if (!userExists)
        {
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        }

        if (!ContentTypes.TryGetValue(request.ContentType, out var meta))
        {
            throw new ArgumentException("Desteklenmeyen medya türü.");
        }

        // 2. Generate unique storage key and checksum
        var storageKey = $"{Guid.NewGuid():N}{meta.Ext}";
        var checksumSha256 = Convert.ToHexString(SHA256.HashData(request.Data)).ToLowerInvariant();

        // 3. Save to storage
        await _storage.SaveAsync(storageKey, request.Data, request.ContentType, cancellationToken);

        // 4. Save to Database
        var asset = new MediaAsset
        {
            OwnerId = request.OwnerId,
            Kind = meta.Kind,
            OriginalName = Path.GetFileName(request.FileName.Length > 255 ? request.FileName[..255] : request.FileName),
            ContentType = request.ContentType,
            SizeBytes = request.Data.Length,
            StorageKey = storageKey,
            ChecksumSha256 = checksumSha256,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            _db.MediaAssets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return MediaAssetDto.FromEntity(asset);
    }
}

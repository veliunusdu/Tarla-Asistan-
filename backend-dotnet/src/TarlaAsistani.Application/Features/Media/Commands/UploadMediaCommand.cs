using MediatR;
using TarlaAsistani.Application.Features.Media.DTOs;

namespace TarlaAsistani.Application.Features.Media.Commands;

public record UploadMediaCommand(
    Guid OwnerId,
    string FileName,
    string ContentType,
    byte[] Data
) : IRequest<MediaAssetDto>;

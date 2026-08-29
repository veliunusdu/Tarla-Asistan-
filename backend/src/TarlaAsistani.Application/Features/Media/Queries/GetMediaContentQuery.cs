using MediatR;
using TarlaAsistani.Application.Features.Media.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Media.Queries;

public record GetMediaContentQuery(
    Guid MediaId,
    Guid UserId,
    UserRole Role
) : IRequest<MediaContentDto?>;

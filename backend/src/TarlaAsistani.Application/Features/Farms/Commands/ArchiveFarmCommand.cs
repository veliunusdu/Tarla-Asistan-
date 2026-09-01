using MediatR;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

// We return a boolean: true if it was found and archived, false if it didn't exist or user lacks permission
public record ArchiveFarmCommand(
    Guid FarmId,
    Guid UserId,
    UserRole Role = UserRole.Farmer
) : IRequest<bool>;

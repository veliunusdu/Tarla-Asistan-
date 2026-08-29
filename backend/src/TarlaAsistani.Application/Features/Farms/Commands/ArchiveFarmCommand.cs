using MediatR;

namespace TarlaAsistani.Application.Features.Farms.Commands;

// We return a boolean: true if it was found and archived, false if it didn't exist
public record ArchiveFarmCommand(Guid FarmId) : IRequest<bool>;

using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Application.Features.Notifications.Commands;

public record DeactivateDeviceTokenCommand(Guid DeviceId, Guid UserId) : IRequest<bool>;

public class DeactivateDeviceTokenCommandHandler : IRequestHandler<DeactivateDeviceTokenCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public DeactivateDeviceTokenCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeactivateDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var device = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == request.UserId, cancellationToken);

        if (device == null)
        {
            return false;
        }

        device.Active = false;
        device.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

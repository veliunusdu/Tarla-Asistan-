using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Farms.Commands;

public class ArchiveFarmCommandHandler : IRequestHandler<ArchiveFarmCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public ArchiveFarmCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(ArchiveFarmCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch the farm from the database with ownership / role check
        // Note: We don't use .AsNoTracking() here because we want to UPDATE it
        var query = _db.Farms
            .Where(f => f.Id == request.FarmId && f.ArchivedAt == null);

        if (request.Role == UserRole.Farmer)
        {
            query = query.Where(f => f.OwnerId == request.UserId);
        }

        var farm = await query.FirstOrDefaultAsync(cancellationToken);

        // 2. If it doesn't exist (or is already archived/hidden, or not owned by the farmer), return false
        if (farm is null)
        {
            return false;
        }

        // 3. Execute Domain Logic
        farm.Archive();

        // 4. Save changes to PostgreSQL
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

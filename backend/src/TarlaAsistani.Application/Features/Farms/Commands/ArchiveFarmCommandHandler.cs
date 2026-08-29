using MediatR;
using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;

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
        // 1. Fetch the farm from the database
        // Note: We don't use .AsNoTracking() here because we want to UPDATE it
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == request.FarmId, cancellationToken);

        // 2. If it doesn't exist (or is already archived/hidden), return false
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

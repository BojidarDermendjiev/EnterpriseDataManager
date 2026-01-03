namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class RecoveryJobRepository : GenericRepository<RecoveryJob>, IRecoveryJobRepository
{
    public RecoveryJobRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public override async Task<RecoveryJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(j => j.ArchiveJob)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryJob>> GetByArchiveJobAsync(
        Guid archiveJobId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.ArchiveJobId == archiveJobId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryJob>> GetByStatusAsync(
        ArchiveStatus status,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == status)
            .Include(j => j.ArchiveJob)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == ArchiveStatus.Running)
            .Include(j => j.ArchiveJob)
            .ToListAsync(cancellationToken);
    }
}

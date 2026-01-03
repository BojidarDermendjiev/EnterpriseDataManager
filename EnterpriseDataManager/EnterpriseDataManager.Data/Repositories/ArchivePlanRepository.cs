namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class ArchivePlanRepository : GenericRepository<ArchivePlan>, IArchivePlanRepository
{
    public ArchivePlanRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public override async Task<ArchivePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.StorageProvider)
            .Include(p => p.RetentionPolicy)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsActive)
            .Include(p => p.StorageProvider)
            .Include(p => p.RetentionPolicy)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetByStorageProviderAsync(
        Guid storageProviderId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.StorageProviderId == storageProviderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetByRetentionPolicyAsync(
        Guid retentionPolicyId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.RetentionPolicyId == retentionPolicyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetScheduledPlansAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsActive && p.Schedule != null)
            .Include(p => p.StorageProvider)
            .ToListAsync(cancellationToken);
    }

    public async Task<ArchivePlan?> GetWithJobsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(p => p.ArchiveJobs)
            .Include(p => p.StorageProvider)
            .Include(p => p.RetentionPolicy)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivePlan>> GetPlansDueForExecutionAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsActive && p.NextRunAt != null && p.NextRunAt <= asOf)
            .Include(p => p.StorageProvider)
            .ToListAsync(cancellationToken);
    }
}

namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class ArchiveJobRepository : GenericRepository<ArchiveJob>, IArchiveJobRepository
{
    public ArchiveJobRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public override async Task<ArchiveJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(j => j.ArchivePlan)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetByPlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.ArchivePlanId == planId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetByStatusAsync(
        ArchiveStatus status,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == status)
            .Include(j => j.ArchivePlan)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == ArchiveStatus.Running)
            .Include(j => j.ArchivePlan)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == ArchiveStatus.Draft || j.Status == ArchiveStatus.Scheduled)
            .Include(j => j.ArchivePlan)
            .OrderBy(j => j.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ArchiveJob?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(j => j.Items)
            .Include(j => j.ArchivePlan)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<ArchiveJob?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetWithItemsAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetJobsInDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.CreatedAt >= from && j.CreatedAt <= to)
            .Include(j => j.ArchivePlan)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetScheduledJobsDueAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == ArchiveStatus.Scheduled && j.ScheduledAt != null && j.ScheduledAt <= asOf)
            .Include(j => j.ArchivePlan)
            .OrderBy(j => j.ScheduledAt)
            .ToListAsync(cancellationToken);
    }
}

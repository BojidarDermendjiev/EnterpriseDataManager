namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class AuditRecordRepository : GenericRepository<AuditRecord>, IAuditRecordRepository
{
    public AuditRecordRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AuditRecord>> GetByActorAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.Actor == actor)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetByActionAsync(
        string action,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.Action == action)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetByResourceAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetInDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.Timestamp >= from && a.Timestamp <= to)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetFailedActionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => !a.Success)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.CorrelationId == correlationId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }
}

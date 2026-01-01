namespace EnterpriseDataManager.Core.Interfaces.Repositories;

using EnterpriseDataManager.Core.Entities;

public interface IUnitOfWork : IDisposable
{
    IRepository<ArchivePlan> ArchivePlans { get; }
    IRepository<ArchiveJob> ArchiveJobs { get; }
    IRepository<ArchiveItem> ArchiveItems { get; }
    IRepository<RecoveryJob> RecoveryJobs { get; }
    IRepository<RetentionPolicy> RetentionPolicies { get; }
    IRepository<StorageProvider> StorageProviders { get; }
    IRepository<AuditRecord> AuditRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(string? userId, CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

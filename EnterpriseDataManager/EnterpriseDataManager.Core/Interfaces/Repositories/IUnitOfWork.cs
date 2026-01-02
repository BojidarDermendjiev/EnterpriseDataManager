namespace EnterpriseDataManager.Core.Interfaces.Repositories;

using EnterpriseDataManager.Core.Entities;

public interface IUnitOfWork : IDisposable
{
    IArchivePlanRepository ArchivePlans { get; }
    IArchiveJobRepository ArchiveJobs { get; }
    IRepository<ArchiveItem> ArchiveItems { get; }
    IRecoveryJobRepository RecoveryJobs { get; }
    IRetentionPolicyRepository RetentionPolicies { get; }
    IStorageProviderRepository StorageProviders { get; }
    IAuditRecordRepository AuditRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(string? userId, CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

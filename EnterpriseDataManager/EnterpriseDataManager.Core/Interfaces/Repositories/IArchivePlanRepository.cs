namespace EnterpriseDataManager.Core.Interfaces.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
public interface IArchivePlanRepository : IRepository<ArchivePlan>
{
    Task<IReadOnlyList<ArchivePlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivePlan>> GetByStorageProviderAsync(Guid storageProviderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivePlan>> GetByRetentionPolicyAsync(Guid retentionPolicyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivePlan>> GetScheduledPlansAsync(CancellationToken cancellationToken = default);
    Task<ArchivePlan?> GetWithJobsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivePlan>> GetPlansDueForExecutionAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

public interface IArchiveJobRepository : IRepository<ArchiveJob>
{
    Task<IReadOnlyList<ArchiveJob>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetByStatusAsync(ArchiveStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default);
    Task<ArchiveJob?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ArchiveJob?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetJobsInDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetScheduledJobsDueAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

public interface IRecoveryJobRepository : IRepository<RecoveryJob>
{
    Task<IReadOnlyList<RecoveryJob>> GetByArchiveJobAsync(Guid archiveJobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryJob>> GetByStatusAsync(ArchiveStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default);
}

public interface IStorageProviderRepository : IRepository<StorageProvider>
{
    Task<IReadOnlyList<StorageProvider>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageProvider>> GetByTypeAsync(StorageType type, CancellationToken cancellationToken = default);
    Task<StorageProvider?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

public interface IRetentionPolicyRepository : IRepository<RetentionPolicy>
{
    Task<RetentionPolicy?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionPolicy>> GetPoliciesWithLegalHoldAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionPolicy>> GetLegalHoldPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionPolicy>> GetImmutablePoliciesAsync(CancellationToken cancellationToken = default);
}

public interface IAuditRecordRepository : IRepository<AuditRecord>
{
    Task<IReadOnlyList<AuditRecord>> GetByActorAsync(string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetByActionAsync(string action, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetByResourceAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetInDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetFailedActionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}

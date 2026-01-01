namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Entities;

public interface IArchivalService
{
    Task<ArchiveJob> CreateJobAsync(Guid archivePlanId, JobPriority priority = JobPriority.Normal, CancellationToken cancellationToken = default);
    Task<ArchiveJob> StartJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ArchiveJob> CompleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ArchiveJob> FailJobAsync(Guid jobId, string reason, CancellationToken cancellationToken = default);
    Task<ArchiveJob> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ArchiveJob?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default);
    Task ProcessScheduledJobsAsync(CancellationToken cancellationToken = default);
    Task<ArchiveItemResult> ArchiveItemAsync(Guid jobId, string sourcePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveItemResult>> ArchiveItemsAsync(Guid jobId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default);
}

public record ArchiveItemResult(
    bool Success,
    string SourcePath,
    string? TargetPath,
    long? SizeBytes,
    string? Hash,
    string? Error);

public interface IArchivePlanService
{
    Task<ArchivePlan> CreatePlanAsync(string name, string sourcePath, SecurityLevel securityLevel = SecurityLevel.Internal, CancellationToken cancellationToken = default);
    Task<ArchivePlan> UpdatePlanAsync(Guid planId, string name, string? description, CancellationToken cancellationToken = default);
    Task<ArchivePlan> ActivatePlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<ArchivePlan> DeactivatePlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<ArchivePlan> SetScheduleAsync(Guid planId, string cronExpression, string? description = null, CancellationToken cancellationToken = default);
    Task<ArchivePlan> SetStorageProviderAsync(Guid planId, Guid storageProviderId, CancellationToken cancellationToken = default);
    Task<ArchivePlan> SetRetentionPolicyAsync(Guid planId, Guid retentionPolicyId, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivePlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
}

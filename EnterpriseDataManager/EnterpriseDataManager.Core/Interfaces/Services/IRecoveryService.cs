namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities;

public interface IRecoveryService
{
    Task<RecoveryJob> CreateRecoveryJobAsync(Guid archiveJobId, string destinationPath, CancellationToken cancellationToken = default);
    Task<RecoveryJob> StartRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default);
    Task<RecoveryJob> CompleteRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default);
    Task<RecoveryJob> FailRecoveryAsync(Guid recoveryJobId, string reason, CancellationToken cancellationToken = default);
    Task<RecoveryJob> CancelRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default);
    Task<RecoveryJob?> GetRecoveryStatusAsync(Guid recoveryJobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryJob>> GetRunningRecoveriesAsync(CancellationToken cancellationToken = default);
    Task<RecoveryItemResult> RecoverItemAsync(Guid recoveryJobId, string archiveItemPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryItemResult>> RecoverItemsAsync(Guid recoveryJobId, IEnumerable<string> archiveItemPaths, CancellationToken cancellationToken = default);
    Task<bool> ValidateArchiveIntegrityAsync(Guid archiveJobId, CancellationToken cancellationToken = default);
}

public record RecoveryItemResult(
    bool Success,
    string SourcePath,
    string? DestinationPath,
    long? SizeBytes,
    bool? IntegrityVerified,
    string? Error);

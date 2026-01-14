namespace EnterpriseDataManager.Core.Interfaces.Services;

using EnterpriseDataManager.Core.Entities;

/// <summary>
/// High-level service for archive operations, providing a simplified API for controllers.
/// </summary>
public interface IArchiveService
{
    /// <summary>
    /// Starts an archive operation for the specified plan.
    /// Creates a new job and begins processing.
    /// </summary>
    Task<ArchiveJob> StartArchiveAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running archive job.
    /// </summary>
    Task CancelArchiveAsync(Guid jobId, CancellationToken cancellationToken = default);
}

namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// High-level archive service that provides a simplified API for controllers.
/// Wraps the lower-level IArchivalService for common operations.
/// </summary>
public sealed class ArchiveService : IArchiveService
{
    private readonly IArchivalService _archivalService;
    private readonly ILogger<ArchiveService>? _logger;

    public ArchiveService(
        IArchivalService archivalService,
        ILogger<ArchiveService>? logger = null)
    {
        _archivalService = archivalService;
        _logger = logger;
    }

    public async Task<ArchiveJob> StartArchiveAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting archive for plan {PlanId}", planId);

        var job = await _archivalService.CreateJobAsync(planId, JobPriority.Normal, cancellationToken);
        job = await _archivalService.StartJobAsync(job.Id, cancellationToken);

        _logger?.LogInformation("Archive job {JobId} started for plan {PlanId}", job.Id, planId);
        return job;
    }

    public async Task CancelArchiveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Cancelling archive job {JobId}", jobId);

        await _archivalService.CancelJobAsync(jobId, cancellationToken);

        _logger?.LogInformation("Archive job {JobId} cancelled", jobId);
    }
}

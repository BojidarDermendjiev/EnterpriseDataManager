namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Enums;

/// <summary>
/// API controller for dashboard data and statistics.
/// </summary>
[Route("api/dashboard")]
public class DashboardApiController : ApiBaseController
{
    private readonly IArchivePlanRepository _archivePlanRepository;
    private readonly IArchiveJobRepository _archiveJobRepository;
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly IStorageProviderRepository _storageProviderRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<DashboardApiController> _logger;

    public DashboardApiController(
        IArchivePlanRepository archivePlanRepository,
        IArchiveJobRepository archiveJobRepository,
        IRecoveryJobRepository recoveryJobRepository,
        IStorageProviderRepository storageProviderRepository,
        IAuditService auditService,
        ILogger<DashboardApiController> logger)
    {
        _archivePlanRepository = archivePlanRepository;
        _archiveJobRepository = archiveJobRepository;
        _recoveryJobRepository = recoveryJobRepository;
        _storageProviderRepository = storageProviderRepository;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Gets overview statistics for the dashboard.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetStats(
        CancellationToken cancellationToken = default)
    {
        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var recoveryJobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);
        var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var stats = new DashboardStatsDto
        {
            TotalArchivePlans = plans.Count,
            ActiveArchivePlans = plans.Count(p => p.IsActive),
            TotalArchiveJobs = jobs.Count,
            RunningArchiveJobs = jobs.Count(j => j.Status == ArchiveStatus.Running),
            CompletedJobsLast24Hours = jobs.Count(j => j.Status == ArchiveStatus.Completed && j.CompletedAt >= last24Hours),
            FailedJobsLast24Hours = jobs.Count(j => j.Status == ArchiveStatus.Failed && j.CompletedAt >= last24Hours),
            TotalRecoveryJobs = recoveryJobs.Count,
            RunningRecoveryJobs = recoveryJobs.Count(j => j.Status == ArchiveStatus.Running),
            TotalStorageProviders = storageProviders.Count,
            EnabledStorageProviders = storageProviders.Count(s => s.IsEnabled),
            TotalDataArchivedBytes = jobs.Where(j => j.Status == ArchiveStatus.Completed).Sum(j => j.ProcessedBytes),
            JobsLast7Days = jobs.Count(j => j.CreatedAt >= last7Days),
            JobsLast30Days = jobs.Count(j => j.CreatedAt >= last30Days),
            SuccessRate = jobs.Count > 0
                ? (double)jobs.Count(j => j.Status == ArchiveStatus.Completed) / jobs.Count * 100
                : 100
        };

        return Success(stats);
    }

    /// <summary>
    /// Gets recent activity for the dashboard.
    /// </summary>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ActivityItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ActivityItemDto>>>> GetRecentActivity(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var recoveryJobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);

        var activities = new List<ActivityItemDto>();

        // Add archive job activities
        foreach (var job in jobs.OrderByDescending(j => j.UpdatedAt ?? j.CreatedAt).Take(limit))
        {
            activities.Add(new ActivityItemDto
            {
                Id = job.Id,
                Type = "ArchiveJob",
                Action = GetJobAction(job.Status),
                Description = $"Archive job {job.Id.ToString()[..8]}",
                Status = job.Status.ToString(),
                Timestamp = job.UpdatedAt ?? job.CreatedAt,
                Actor = job.CreatedBy ?? "System"
            });
        }

        // Add recovery job activities
        foreach (var job in recoveryJobs.OrderByDescending(j => j.UpdatedAt ?? j.CreatedAt).Take(limit))
        {
            activities.Add(new ActivityItemDto
            {
                Id = job.Id,
                Type = "RecoveryJob",
                Action = GetJobAction(job.Status),
                Description = $"Recovery job {job.Id.ToString()[..8]}",
                Status = job.Status.ToString(),
                Timestamp = job.UpdatedAt ?? job.CreatedAt,
                Actor = job.CreatedBy ?? "System"
            });
        }

        var result = activities
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToList();

        return Success<IReadOnlyList<ActivityItemDto>>(result);
    }

    /// <summary>
    /// Gets job statistics over time.
    /// </summary>
    [HttpGet("job-stats")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<JobStatDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JobStatDto>>>> GetJobStats(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var startDate = DateTimeOffset.UtcNow.AddDays(-days).Date;

        var stats = Enumerable.Range(0, days)
            .Select(i => startDate.AddDays(i))
            .Select(date => new JobStatDto
            {
                Date = new DateTimeOffset(date, TimeSpan.Zero),
                TotalJobs = jobs.Count(j => j.CreatedAt.Date == date),
                CompletedJobs = jobs.Count(j => j.Status == ArchiveStatus.Completed && j.CompletedAt?.Date == date),
                FailedJobs = jobs.Count(j => j.Status == ArchiveStatus.Failed && j.CompletedAt?.Date == date),
                DataProcessedBytes = jobs
                    .Where(j => j.Status == ArchiveStatus.Completed && j.CompletedAt?.Date == date)
                    .Sum(j => j.ProcessedBytes)
            })
            .ToList();

        return Success<IReadOnlyList<JobStatDto>>(stats);
    }

    /// <summary>
    /// Gets storage usage by provider.
    /// </summary>
    [HttpGet("storage-usage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StorageUsageSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StorageUsageSummaryDto>>>> GetStorageUsage(
        CancellationToken cancellationToken = default)
    {
        var providers = await _storageProviderRepository.GetAllAsync(cancellationToken);
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);

        var result = providers.Select(p =>
        {
            var providerPlans = plans.Where(pl => pl.StorageProviderId == p.Id).ToList();
            var providerJobs = jobs.Where(j => providerPlans.Any(pl => pl.Id == j.ArchivePlanId)).ToList();

            return new StorageUsageSummaryDto
            {
                ProviderId = p.Id,
                ProviderName = p.Name,
                ProviderType = p.Type.ToString(),
                IsEnabled = p.IsEnabled,
                UsedBytes = providerJobs.Where(j => j.Status == ArchiveStatus.Completed).Sum(j => j.ProcessedBytes),
                QuotaBytes = p.QuotaBytes,
                ArchivePlanCount = providerPlans.Count,
                JobCount = providerJobs.Count
            };
        }).ToList();

        return Success<IReadOnlyList<StorageUsageSummaryDto>>(result);
    }

    /// <summary>
    /// Gets system health summary.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<SystemHealthDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SystemHealthDto>>> GetSystemHealth(
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);

        var runningJobs = jobs.Count(j => j.Status == ArchiveStatus.Running);
        var failedJobsLast24h = jobs.Count(j => j.Status == ArchiveStatus.Failed && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24));
        var enabledProviders = storageProviders.Count(s => s.IsEnabled);

        var health = new SystemHealthDto
        {
            OverallStatus = failedJobsLast24h > 5 ? "Warning" : "Healthy",
            RunningJobs = runningJobs,
            FailedJobsLast24Hours = failedJobsLast24h,
            EnabledStorageProviders = enabledProviders,
            TotalStorageProviders = storageProviders.Count,
            LastChecked = DateTimeOffset.UtcNow,
            Components = new List<ComponentHealthDto>
            {
                new() { Name = "Archive Service", Status = "Healthy", Message = $"{runningJobs} jobs running" },
                new() { Name = "Storage Providers", Status = enabledProviders > 0 ? "Healthy" : "Warning", Message = $"{enabledProviders} of {storageProviders.Count} enabled" },
                new() { Name = "Database", Status = "Healthy", Message = "Connected" }
            }
        };

        return Success(health);
    }

    private static string GetJobAction(ArchiveStatus status)
    {
        return status switch
        {
            ArchiveStatus.Draft => "Created",
            ArchiveStatus.Scheduled => "Scheduled",
            ArchiveStatus.Running => "Started",
            ArchiveStatus.Completed => "Completed",
            ArchiveStatus.Failed => "Failed",
            ArchiveStatus.Canceled => "Canceled",
            _ => "Updated"
        };
    }
}

#region Dashboard DTOs

public record DashboardStatsDto
{
    public int TotalArchivePlans { get; init; }
    public int ActiveArchivePlans { get; init; }
    public int TotalArchiveJobs { get; init; }
    public int RunningArchiveJobs { get; init; }
    public int CompletedJobsLast24Hours { get; init; }
    public int FailedJobsLast24Hours { get; init; }
    public int TotalRecoveryJobs { get; init; }
    public int RunningRecoveryJobs { get; init; }
    public int TotalStorageProviders { get; init; }
    public int EnabledStorageProviders { get; init; }
    public long TotalDataArchivedBytes { get; init; }
    public int JobsLast7Days { get; init; }
    public int JobsLast30Days { get; init; }
    public double SuccessRate { get; init; }
}

public record ActivityItemDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";
    public string Action { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string Actor { get; init; } = "";
}

public record JobStatDto
{
    public DateTimeOffset Date { get; init; }
    public int TotalJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public long DataProcessedBytes { get; init; }
}

public record StorageUsageSummaryDto
{
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = "";
    public string ProviderType { get; init; } = "";
    public bool IsEnabled { get; init; }
    public long UsedBytes { get; init; }
    public long? QuotaBytes { get; init; }
    public int ArchivePlanCount { get; init; }
    public int JobCount { get; init; }
    public double? UsagePercentage => QuotaBytes.HasValue && QuotaBytes > 0
        ? (double)UsedBytes / QuotaBytes.Value * 100
        : null;
}

public record SystemHealthDto
{
    public string OverallStatus { get; init; } = "";
    public int RunningJobs { get; init; }
    public int FailedJobsLast24Hours { get; init; }
    public int EnabledStorageProviders { get; init; }
    public int TotalStorageProviders { get; init; }
    public DateTimeOffset LastChecked { get; init; }
    public List<ComponentHealthDto> Components { get; init; } = new();
}

public record ComponentHealthDto
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Message { get; init; }
}

#endregion

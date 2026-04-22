namespace EnterpriseDataManager.Infrastructure.BackgroundJobs;

using Cronos;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Data;
using AlertSeverity = EnterpriseDataManager.Infrastructure.ExternalServices.AlertSeverity;
using INotificationService = EnterpriseDataManager.Infrastructure.ExternalServices.INotificationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

public class ArchivalJobSchedulerOptions
{
    public const string SectionName = "ArchivalJobScheduler";

    public bool Enabled { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxConcurrentJobs { get; set; } = 4;
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromHours(4);
    public int MaxJobRetries { get; set; } = 3;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public List<ScheduledJobConfiguration> Jobs { get; set; } = new();
}

public class ScheduledJobConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public JobType Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class ArchivalJobScheduler : BackgroundService, IArchivalJobScheduler
{
    private readonly ArchivalJobSchedulerOptions _options;
    private readonly ILogger<ArchivalJobScheduler>? _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService? _notificationService;
    private readonly ConcurrentDictionary<string, ScheduledJob> _scheduledJobs = new();
    private readonly ConcurrentDictionary<string, JobExecution> _runningJobs = new();
    private readonly ConcurrentQueue<JobExecution> _jobHistory = new();
    private readonly SemaphoreSlim _jobSemaphore;

    public ArchivalJobScheduler(
        IOptions<ArchivalJobSchedulerOptions> options,
        IServiceScopeFactory scopeFactory,
        INotificationService? notificationService = null,
        ILogger<ArchivalJobScheduler>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _notificationService = notificationService;
        _logger = logger;
        _jobSemaphore = new SemaphoreSlim(_options.MaxConcurrentJobs, _options.MaxConcurrentJobs);

        InitializeScheduledJobs();
    }

    public async Task<string> ScheduleJobAsync(
        string name,
        string cronExpression,
        JobType jobType,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();

        var job = new ScheduledJob(
            JobId: jobId,
            Name: name,
            CronExpression: cronExpression,
            ParsedCron: CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds),
            JobType: jobType,
            Parameters: parameters ?? new Dictionary<string, string>(),
            IsEnabled: true,
            CreatedAt: DateTimeOffset.UtcNow,
            LastRunAt: null,
            NextRunAt: null);

        job = job with { NextRunAt = CalculateNextRunTime(job.ParsedCron) };

        _scheduledJobs[jobId] = job;
        _logger?.LogInformation("Scheduled job {JobName} ({JobId}) with cron {Cron}, next run at {NextRun}",
            name, jobId, cronExpression, job.NextRunAt);

        await PersistJobAsync(job, cancellationToken);

        return jobId;
    }

    public async Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var removed = _scheduledJobs.TryRemove(jobId, out var job);

        if (removed)
        {
            _logger?.LogInformation("Cancelled scheduled job {JobName} ({JobId})", job!.Name, jobId);
            await RemovePersistedJobAsync(jobId, CancellationToken.None);
        }

        return removed;
    }

    public async Task<bool> DisableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_scheduledJobs.TryGetValue(jobId, out var job))
        {
            _scheduledJobs[jobId] = job with { IsEnabled = false };
            _logger?.LogInformation("Disabled job {JobName} ({JobId})", job.Name, jobId);
            await PersistJobAsync(_scheduledJobs[jobId], CancellationToken.None);
            return true;
        }

        return false;
    }

    public async Task<bool> EnableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_scheduledJobs.TryGetValue(jobId, out var job))
        {
            var nextRun = CalculateNextRunTime(job.ParsedCron);
            _scheduledJobs[jobId] = job with { IsEnabled = true, NextRunAt = nextRun };
            _logger?.LogInformation("Enabled job {JobName} ({JobId}), next run at {NextRun}", job.Name, jobId, nextRun);
            await PersistJobAsync(_scheduledJobs[jobId], CancellationToken.None);
            return true;
        }

        return false;
    }

    public async Task<JobExecution> TriggerJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (!_scheduledJobs.TryGetValue(jobId, out var job))
        {
            throw new KeyNotFoundException($"Job {jobId} not found");
        }

        return await ExecuteJobAsync(job, cancellationToken);
    }

    public Task<ScheduledJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _scheduledJobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<ScheduledJob>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = _scheduledJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<ScheduledJob>>(jobs);
    }

    public Task<IReadOnlyList<JobExecution>> GetRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        var running = _runningJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<JobExecution>>(running);
    }

    public Task<IReadOnlyList<JobExecution>> GetJobHistoryAsync(
        string? jobId = null,
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var history = _jobHistory
            .Where(e => jobId == null || e.JobId == jobId)
            .OrderByDescending(e => e.StartedAt)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<JobExecution>>(history);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger?.LogInformation("Archival job scheduler is disabled");
            return;
        }

        _logger?.LogInformation("Archival job scheduler started");

        await LoadJobsFromDatabaseAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing scheduled jobs");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }

        _logger?.LogInformation("Archival job scheduler stopped");
    }

    private void InitializeScheduledJobs()
    {
        foreach (var config in _options.Jobs.Where(j => j.IsEnabled))
        {
            try
            {
                var jobId = Guid.NewGuid().ToString();
                var parsedCron = CronExpression.Parse(config.CronExpression, CronFormat.IncludeSeconds);
                var nextRun = CalculateNextRunTime(parsedCron);

                var job = new ScheduledJob(
                    JobId: jobId,
                    Name: config.Name,
                    CronExpression: config.CronExpression,
                    ParsedCron: parsedCron,
                    JobType: config.Type,
                    Parameters: config.Parameters,
                    IsEnabled: true,
                    CreatedAt: DateTimeOffset.UtcNow,
                    LastRunAt: null,
                    NextRunAt: nextRun);

                _scheduledJobs[jobId] = job;

                _logger?.LogInformation("Initialized scheduled job {JobName} with cron {Cron}, next run at {NextRun}",
                    config.Name, config.CronExpression, nextRun);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize scheduled job {JobName}", config.Name);
            }
        }
    }

    private async Task ProcessDueJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dueJobs = _scheduledJobs.Values
            .Where(j => j.IsEnabled && j.NextRunAt.HasValue && j.NextRunAt.Value <= now)
            .ToList();

        foreach (var job in dueJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Update next run time immediately to prevent re-execution
            var nextRun = CalculateNextRunTime(job.ParsedCron);
            _scheduledJobs[job.JobId] = job with { NextRunAt = nextRun };

            // Execute job in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteJobAsync(job, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing job {JobName} ({JobId})", job.Name, job.JobId);
                }
            }, cancellationToken);
        }
    }

    private async Task<JobExecution> ExecuteJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        var executionId = Guid.NewGuid().ToString();

        var execution = new JobExecution(
            ExecutionId: executionId,
            JobId: job.JobId,
            JobName: job.Name,
            JobType: job.JobType,
            Status: JobExecutionStatus.Running,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: null,
            ErrorMessage: null,
            ItemsProcessed: 0,
            BytesProcessed: 0);

        _runningJobs[executionId] = execution;
        _logger?.LogInformation("Starting job execution {ExecutionId} for {JobName}", executionId, job.Name);

        try
        {
            await _jobSemaphore.WaitAsync(cancellationToken);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.JobTimeout);

                var result = await ExecuteWithRetryAsync(job, timeoutCts.Token);

                execution = execution with
                {
                    Status = JobExecutionStatus.Completed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ItemsProcessed = result.ItemsProcessed,
                    BytesProcessed = result.BytesProcessed
                };

                // Update last run time and persist
                if (_scheduledJobs.TryGetValue(job.JobId, out var currentJob))
                {
                    _scheduledJobs[job.JobId] = currentJob with { LastRunAt = DateTimeOffset.UtcNow };
                }

                if (_scheduledJobs.TryGetValue(job.JobId, out var updatedJob))
                {
                    await PersistJobAsync(updatedJob, CancellationToken.None);
                }

                _logger?.LogInformation("Job execution {ExecutionId} completed successfully. Items: {Items}, Bytes: {Bytes}",
                    executionId, result.ItemsProcessed, result.BytesProcessed);
            }
            finally
            {
                _jobSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            execution = execution with
            {
                Status = JobExecutionStatus.Cancelled,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = "Job was cancelled or timed out"
            };

            _logger?.LogWarning("Job execution {ExecutionId} was cancelled", executionId);
        }
        catch (Exception ex)
        {
            execution = execution with
            {
                Status = JobExecutionStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };

            _logger?.LogError(ex, "Job execution {ExecutionId} failed", executionId);
        }
        finally
        {
            _runningJobs.TryRemove(executionId, out _);
            _jobHistory.Enqueue(execution);

            // Keep history limited
            while (_jobHistory.Count > 1000)
            {
                _jobHistory.TryDequeue(out _);
            }
        }

        return execution;
    }

    private async Task<JobResult> ExecuteWithRetryAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _options.MaxJobRetries)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = _options.InitialRetryDelay * Math.Pow(2, attempt - 1);
                    _logger?.LogWarning("Retrying job {JobName} (attempt {Attempt}/{Max}) after {Delay}",
                        job.Name, attempt + 1, _options.MaxJobRetries + 1, delay);
                    await Task.Delay(delay, cancellationToken);
                }

                return await RunJobAsync(job, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                _logger?.LogWarning(ex, "Job {JobName} attempt {Attempt} failed", job.Name, attempt);
            }
        }

        // All retries exhausted — send alert
        if (_notificationService is not null)
        {
            try
            {
                await _notificationService.SendAlertAsync(
                    AlertSeverity.Critical,
                    $"Archive Job Failed: {job.Name}",
                    $"Job '{job.Name}' (type: {job.JobType}) failed after {_options.MaxJobRetries + 1} attempts. Last error: {lastException?.Message}",
                    cancellationToken);
            }
            catch (Exception notifEx)
            {
                _logger?.LogError(notifEx, "Failed to send failure notification for job {JobName}", job.Name);
            }
        }

        throw lastException!;
    }

    private async Task<JobResult> RunJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        return job.JobType switch
        {
            JobType.ArchiveData => await RunArchiveJobAsync(job, cancellationToken),
            JobType.RestoreData => await RunRestoreJobAsync(job, cancellationToken),
            JobType.VerifyIntegrity => await RunVerifyIntegrityJobAsync(job, cancellationToken),
            JobType.CleanupExpired => await RunCleanupJobAsync(job, cancellationToken),
            JobType.ReplicateData => await RunReplicationJobAsync(job, cancellationToken),
            JobType.GenerateReport => await RunReportJobAsync(job, cancellationToken),
            _ => new JobResult(0, 0)
        };
    }

    private async Task<JobResult> RunArchiveJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        if (!job.Parameters.TryGetValue("PlanId", out var planIdStr) || !Guid.TryParse(planIdStr, out var planId))
        {
            _logger?.LogWarning("Archive job {JobName} is missing a valid PlanId parameter — skipping", job.Name);
            return new JobResult(0, 0);
        }

        _logger?.LogInformation("Running archive job {JobName} for plan {PlanId}", job.Name, planId);

        using var scope = _scopeFactory.CreateScope();
        var archivalService = scope.ServiceProvider.GetRequiredService<IArchivalService>();

        var archiveJob = await archivalService.CreateJobAsync(planId, cancellationToken: cancellationToken);
        archiveJob = await archivalService.StartJobAsync(archiveJob.Id, cancellationToken);
        archiveJob = await archivalService.CompleteJobAsync(archiveJob.Id, cancellationToken);

        _logger?.LogInformation("Archive job {JobName} completed: {Items} items, {Bytes} bytes",
            job.Name, archiveJob.ProcessedItemCount, archiveJob.ProcessedBytes);

        return new JobResult(archiveJob.ProcessedItemCount, archiveJob.ProcessedBytes);
    }

    private async Task<JobResult> RunRestoreJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        if (!job.Parameters.TryGetValue("ArchiveJobId", out var jobIdStr) || !Guid.TryParse(jobIdStr, out var archiveJobId))
        {
            _logger?.LogWarning("Restore job {JobName} is missing a valid ArchiveJobId parameter — skipping", job.Name);
            return new JobResult(0, 0);
        }

        var destinationPath = job.Parameters.TryGetValue("DestinationPath", out var dest) ? dest : Path.GetTempPath();

        _logger?.LogInformation("Running restore job {JobName} for archive job {ArchiveJobId}", job.Name, archiveJobId);

        using var scope = _scopeFactory.CreateScope();
        var recoveryService = scope.ServiceProvider.GetRequiredService<IRecoveryService>();

        var recoveryJob = await recoveryService.CreateRecoveryJobAsync(archiveJobId, destinationPath, cancellationToken);
        recoveryJob = await recoveryService.StartRecoveryAsync(recoveryJob.Id, cancellationToken);
        recoveryJob = await recoveryService.CompleteRecoveryAsync(recoveryJob.Id, cancellationToken);

        _logger?.LogInformation("Restore job {JobName} completed for recovery job {RecoveryJobId}", job.Name, recoveryJob.Id);
        return new JobResult(1, 0);
    }

    private async Task<JobResult> RunVerifyIntegrityJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        if (!job.Parameters.TryGetValue("ArchiveJobId", out var jobIdStr) || !Guid.TryParse(jobIdStr, out var archiveJobId))
        {
            _logger?.LogWarning("Integrity job {JobName} is missing a valid ArchiveJobId parameter — skipping", job.Name);
            return new JobResult(0, 0);
        }

        _logger?.LogInformation("Running integrity verification for archive job {ArchiveJobId}", archiveJobId);

        using var scope = _scopeFactory.CreateScope();
        var recoveryService = scope.ServiceProvider.GetRequiredService<IRecoveryService>();

        var isValid = await recoveryService.ValidateArchiveIntegrityAsync(archiveJobId, cancellationToken);

        _logger?.LogInformation("Integrity check for {ArchiveJobId}: {Result}", archiveJobId, isValid ? "PASSED" : "FAILED");
        return new JobResult(1, 0);
    }

    private async Task<JobResult> RunCleanupJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running cleanup job {JobName}", job.Name);

        using var scope = _scopeFactory.CreateScope();
        var archivalService = scope.ServiceProvider.GetRequiredService<IArchivalService>();
        await archivalService.ProcessScheduledJobsAsync(cancellationToken);

        return new JobResult(0, 0);
    }

    private Task<JobResult> RunReplicationJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogWarning("Replication job {JobName} is not yet implemented — skipping", job.Name);
        return Task.FromResult(new JobResult(0, 0));
    }

    private Task<JobResult> RunReportJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogWarning("Report job {JobName} is not yet implemented — skipping", job.Name);
        return Task.FromResult(new JobResult(0, 0));
    }

    private static DateTimeOffset? CalculateNextRunTime(CronExpression cron)
    {
        var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
        return next;
    }

    private async Task LoadJobsFromDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

            var records = await db.ScheduledJobRecords.ToListAsync(cancellationToken);
            var loaded = 0;

            foreach (var rec in records)
            {
                try
                {
                    var parsedCron = CronExpression.Parse(rec.CronExpression, CronFormat.IncludeSeconds);
                    var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(rec.ParametersJson)
                        ?? new Dictionary<string, string>();

                    var job = new ScheduledJob(
                        JobId: rec.JobId,
                        Name: rec.Name,
                        CronExpression: rec.CronExpression,
                        ParsedCron: parsedCron,
                        JobType: (JobType)rec.JobType,
                        Parameters: parameters,
                        IsEnabled: rec.IsEnabled,
                        CreatedAt: rec.CreatedAt,
                        LastRunAt: rec.LastRunAt,
                        NextRunAt: rec.NextRunAt ?? CalculateNextRunTime(parsedCron));

                    _scheduledJobs[rec.JobId] = job;
                    loaded++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to restore scheduled job {JobId} from database", rec.JobId);
                }
            }

            _logger?.LogInformation("Restored {Count} scheduled jobs from database", loaded);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load scheduled jobs from database");
        }
    }

    private async Task PersistJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

            var parametersJson = System.Text.Json.JsonSerializer.Serialize(job.Parameters);
            var existing = await db.ScheduledJobRecords.FindAsync(new object[] { job.JobId }, cancellationToken);

            if (existing is null)
            {
                db.ScheduledJobRecords.Add(new ScheduledJobRecord
                {
                    JobId = job.JobId,
                    Name = job.Name,
                    CronExpression = job.CronExpression,
                    JobType = (int)job.JobType,
                    ParametersJson = parametersJson,
                    IsEnabled = job.IsEnabled,
                    CreatedAt = job.CreatedAt,
                    LastRunAt = job.LastRunAt,
                    NextRunAt = job.NextRunAt
                });
            }
            else
            {
                existing.Name = job.Name;
                existing.IsEnabled = job.IsEnabled;
                existing.LastRunAt = job.LastRunAt;
                existing.NextRunAt = job.NextRunAt;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist scheduled job {JobId}", job.JobId);
        }
    }

    private async Task RemovePersistedJobAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

            var record = await db.ScheduledJobRecords.FindAsync(new object[] { jobId }, cancellationToken);
            if (record is not null)
            {
                db.ScheduledJobRecords.Remove(record);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove persisted job {JobId}", jobId);
        }
    }
}

public interface IArchivalJobScheduler
{
    Task<string> ScheduleJobAsync(string name, string cronExpression, JobType jobType, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default);
    Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<bool> DisableJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<bool> EnableJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<JobExecution> TriggerJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<ScheduledJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJob>> GetAllJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecution>> GetRunningJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecution>> GetJobHistoryAsync(string? jobId = null, int count = 100, CancellationToken cancellationToken = default);
}

public record ScheduledJob(
    string JobId,
    string Name,
    string CronExpression,
    CronExpression ParsedCron,
    JobType JobType,
    Dictionary<string, string> Parameters,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

public record JobExecution(
    string ExecutionId,
    string JobId,
    string JobName,
    JobType JobType,
    JobExecutionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    long ItemsProcessed,
    long BytesProcessed);

public record JobResult(long ItemsProcessed, long BytesProcessed);

public enum JobType
{
    ArchiveData,
    RestoreData,
    VerifyIntegrity,
    CleanupExpired,
    ReplicateData,
    GenerateReport,
    Custom
}

public enum JobExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

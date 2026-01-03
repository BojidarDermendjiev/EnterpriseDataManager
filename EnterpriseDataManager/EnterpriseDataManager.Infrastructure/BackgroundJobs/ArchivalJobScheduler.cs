namespace EnterpriseDataManager.Infrastructure.BackgroundJobs;

using Cronos;
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
    private readonly ConcurrentDictionary<string, ScheduledJob> _scheduledJobs = new();
    private readonly ConcurrentDictionary<string, JobExecution> _runningJobs = new();
    private readonly ConcurrentQueue<JobExecution> _jobHistory = new();
    private readonly SemaphoreSlim _jobSemaphore;

    public ArchivalJobScheduler(
        IOptions<ArchivalJobSchedulerOptions> options,
        ILogger<ArchivalJobScheduler>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _jobSemaphore = new SemaphoreSlim(_options.MaxConcurrentJobs, _options.MaxConcurrentJobs);

        InitializeScheduledJobs();
    }

    public Task<string> ScheduleJobAsync(
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

        return Task.FromResult(jobId);
    }

    public Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var removed = _scheduledJobs.TryRemove(jobId, out var job);

        if (removed)
        {
            _logger?.LogInformation("Cancelled scheduled job {JobName} ({JobId})", job!.Name, jobId);
        }

        return Task.FromResult(removed);
    }

    public Task<bool> DisableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_scheduledJobs.TryGetValue(jobId, out var job))
        {
            _scheduledJobs[jobId] = job with { IsEnabled = false };
            _logger?.LogInformation("Disabled job {JobName} ({JobId})", job.Name, jobId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> EnableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_scheduledJobs.TryGetValue(jobId, out var job))
        {
            var nextRun = CalculateNextRunTime(job.ParsedCron);
            _scheduledJobs[jobId] = job with { IsEnabled = true, NextRunAt = nextRun };
            _logger?.LogInformation("Enabled job {JobName} ({JobId}), next run at {NextRun}", job.Name, jobId, nextRun);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
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

                var result = await RunJobAsync(job, timeoutCts.Token);

                execution = execution with
                {
                    Status = JobExecutionStatus.Completed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ItemsProcessed = result.ItemsProcessed,
                    BytesProcessed = result.BytesProcessed
                };

                // Update last run time
                if (_scheduledJobs.TryGetValue(job.JobId, out var currentJob))
                {
                    _scheduledJobs[job.JobId] = currentJob with { LastRunAt = DateTimeOffset.UtcNow };
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

    private async Task<JobResult> RunJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        // Simulate job execution based on type
        // In production, this would delegate to actual service implementations
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
        _logger?.LogInformation("Running archive job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken); // Simulated work
        return new JobResult(ItemsProcessed: 100, BytesProcessed: 1024 * 1024 * 100);
    }

    private async Task<JobResult> RunRestoreJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running restore job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return new JobResult(ItemsProcessed: 50, BytesProcessed: 1024 * 1024 * 50);
    }

    private async Task<JobResult> RunVerifyIntegrityJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running integrity verification job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return new JobResult(ItemsProcessed: 1000, BytesProcessed: 0);
    }

    private async Task<JobResult> RunCleanupJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running cleanup job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return new JobResult(ItemsProcessed: 200, BytesProcessed: 1024 * 1024 * 500);
    }

    private async Task<JobResult> RunReplicationJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running replication job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return new JobResult(ItemsProcessed: 150, BytesProcessed: 1024 * 1024 * 200);
    }

    private async Task<JobResult> RunReportJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Running report generation job {JobName}", job.Name);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return new JobResult(ItemsProcessed: 1, BytesProcessed: 1024 * 10);
    }

    private static DateTimeOffset? CalculateNextRunTime(CronExpression cron)
    {
        var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
        return next;
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

namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

public sealed class ArchivalService : IArchivalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchivalService>? _logger;
    private readonly IStorageProviderFactory _storageFactory;

    public ArchivalService(
        IUnitOfWork unitOfWork,
        IStorageProviderFactory storageFactory,
        ILogger<ArchivalService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _storageFactory = storageFactory;
        _logger = logger;
    }

    public async Task<ArchiveJob> CreateJobAsync(Guid archivePlanId, JobPriority priority = JobPriority.Normal, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(archivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(archivePlanId);

        var job = plan.CreateJob(priority);
        await _unitOfWork.ArchiveJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created archive job {JobId} for plan {PlanId}", job.Id, archivePlanId);
        return job;
    }

    public async Task<ArchiveJob> StartJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(job.ArchivePlanId);

        var sourceFiles = EnumerateSourceFiles(plan.SourcePath.Path);
        var totalBytes = sourceFiles
            .Where(File.Exists)
            .Sum(f => new FileInfo(f).Length);

        job.SetItemCounts(sourceFiles.Count, totalBytes);
        job.Start();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Started archive job {JobId} with {Count} files ({Bytes} bytes)",
            jobId, sourceFiles.Count, totalBytes);
        return job;
    }

    public async Task ExecuteArchiveJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(job.ArchivePlanId);

        if (plan.StorageProvider is null)
        {
            job.Fail("Archive plan has no storage provider configured");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var adapter = _storageFactory.CreateForProvider(plan.StorageProvider);
        var sourceFiles = EnumerateSourceFiles(plan.SourcePath.Path);

        foreach (var sourcePath in sourceFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var fileName = Path.GetFileName(sourcePath);
            var targetPath = $"{job.Id}/{fileName}";

            try
            {
                if (!File.Exists(sourcePath))
                {
                    var missingItem = job.AddItem(sourcePath, targetPath, 0);
                    job.RecordItemFailure(missingItem, $"Source file not found: {sourcePath}");
                    _logger?.LogWarning("Source file not found during archive job {JobId}: {SourcePath}", jobId, sourcePath);
                    continue;
                }

                var sizeBytes = new FileInfo(sourcePath).Length;
                string hash;

                await using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    hash = await ComputeHashAsync(sourceStream, cancellationToken);
                    sourceStream.Position = 0;
                    await adapter.WriteAsync(targetPath, sourceStream, cancellationToken);
                }

                var item = job.AddItem(sourcePath, targetPath, sizeBytes);
                job.RecordItemSuccess(item, hash);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to archive item {SourcePath} in job {JobId}", sourcePath, jobId);
                var failedItem = job.AddItem(sourcePath, targetPath, 0);
                job.RecordItemFailure(failedItem, ex.Message);
            }
        }

        if (job.ProcessedItemCount > 0 || job.TotalItemCount == 0)
        {
            job.Complete();
        }
        else
        {
            job.Fail($"All {job.TotalItemCount} items failed to archive");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Archive job {JobId} finished: {Processed} processed, {Failed} failed",
            jobId, job.ProcessedItemCount, job.FailedItemCount);
    }

    public async Task<ArchiveJob> CompleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        job.Complete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Completed archive job {JobId}", jobId);
        return job;
    }

    public async Task<ArchiveJob> FailJobAsync(Guid jobId, string reason, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        job.Fail(reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogWarning("Archive job {JobId} failed: {Reason}", jobId, reason);
        return job;
    }

    public async Task<ArchiveJob> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        job.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Cancelled archive job {JobId}", jobId);
        return job;
    }

    public async Task<ArchiveJob?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.ArchiveJobs.GetByIdAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveJob>> GetRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.ArchiveJobs.GetRunningJobsAsync(cancellationToken);
    }

    public async Task ProcessScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        var duePlans = await _unitOfWork.ArchivePlans.GetPlansDueForExecutionAsync(DateTimeOffset.UtcNow, cancellationToken);

        foreach (var plan in duePlans)
        {
            try
            {
                _logger?.LogInformation("Creating scheduled job for plan {PlanId}", plan.Id);
                var job = plan.CreateJob(JobPriority.Normal);
                await _unitOfWork.ArchiveJobs.AddAsync(job, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create scheduled job for plan {PlanId}", plan.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArchiveItemResult> ArchiveItemAsync(Guid jobId, string sourcePath, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(jobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(jobId);

        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(job.ArchivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(job.ArchivePlanId);

        try
        {
            var fileName = Path.GetFileName(sourcePath);
            var targetPath = $"{job.Id}/{fileName}";
            long sizeBytes = 0;
            string? hash = null;

            if (File.Exists(sourcePath) && plan.StorageProvider is not null)
            {
                sizeBytes = new FileInfo(sourcePath).Length;
                var adapter = _storageFactory.CreateForProvider(plan.StorageProvider);

                await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                hash = await ComputeHashAsync(sourceStream, cancellationToken);
                sourceStream.Position = 0;
                await adapter.WriteAsync(targetPath, sourceStream, cancellationToken);
            }

            var item = job.AddItem(sourcePath, targetPath, sizeBytes);
            job.RecordItemSuccess(item, hash);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ArchiveItemResult(true, sourcePath, targetPath, sizeBytes, hash, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to archive item {SourcePath}", sourcePath);

            var failedItem = job.AddItem(sourcePath, $"{job.Id}/{Path.GetFileName(sourcePath)}", 0);
            job.RecordItemFailure(failedItem, ex.Message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ArchiveItemResult(false, sourcePath, null, null, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ArchiveItemResult>> ArchiveItemsAsync(Guid jobId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        var results = new List<ArchiveItemResult>();

        foreach (var sourcePath in sourcePaths)
        {
            var result = await ArchiveItemAsync(jobId, sourcePath, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private IReadOnlyList<string> EnumerateSourceFiles(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            _logger?.LogWarning("Source path does not exist or is empty: {SourcePath}", sourcePath);
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate source files at {SourcePath}", sourcePath);
            return Array.Empty<string>();
        }
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

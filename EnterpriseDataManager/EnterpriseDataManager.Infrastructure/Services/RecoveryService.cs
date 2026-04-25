namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Exceptions;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

public sealed class RecoveryService : IRecoveryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecoveryService>? _logger;
    private readonly IStorageProviderFactory _storageFactory;

    public RecoveryService(
        IUnitOfWork unitOfWork,
        IStorageProviderFactory storageFactory,
        ILogger<RecoveryService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _storageFactory = storageFactory;
        _logger = logger;
    }

    public async Task<RecoveryJob> CreateRecoveryJobAsync(Guid archiveJobId, string destinationPath, CancellationToken cancellationToken = default)
    {
        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        if (archiveJob.Status != ArchiveStatus.Completed)
        {
            throw ArchivalException.InvalidStatus(archiveJobId, archiveJob.Status.ToString(), ArchiveStatus.Completed.ToString());
        }

        var recoveryJob = RecoveryJob.Create(archiveJobId, destinationPath);
        await _unitOfWork.RecoveryJobs.AddAsync(recoveryJob, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created recovery job {RecoveryJobId} from archive {ArchiveJobId}", recoveryJob.Id, archiveJobId);
        return recoveryJob;
    }

    public async Task<RecoveryJob> StartRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(job.ArchiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(job.ArchiveJobId);

        job.Start(archiveJob.TotalItemCount, archiveJob.TotalBytes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Started recovery job {RecoveryJobId}", recoveryJobId);
        return job;
    }

    public async Task<RecoveryJob> CompleteRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        job.Complete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Completed recovery job {RecoveryJobId}", recoveryJobId);
        return job;
    }

    public async Task<RecoveryJob> FailRecoveryAsync(Guid recoveryJobId, string reason, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        job.Fail(reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogWarning("Recovery job {RecoveryJobId} failed: {Reason}", recoveryJobId, reason);
        return job;
    }

    public async Task<RecoveryJob> CancelRecoveryAsync(Guid recoveryJobId, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        job.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Cancelled recovery job {RecoveryJobId}", recoveryJobId);
        return job;
    }

    public async Task<RecoveryJob?> GetRecoveryStatusAsync(Guid recoveryJobId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryJob>> GetRunningRecoveriesAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.RecoveryJobs.GetRunningJobsAsync(cancellationToken);
    }

    public async Task<RecoveryItemResult> RecoverItemAsync(Guid recoveryJobId, string archiveItemPath, CancellationToken cancellationToken = default)
    {
        var job = await _unitOfWork.RecoveryJobs.GetByIdAsync(recoveryJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForRecoveryJob(recoveryJobId);

        try
        {
            var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(job.ArchiveJobId, cancellationToken)
                ?? throw EntityNotFoundException.ForArchiveJob(job.ArchiveJobId);

            var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(archiveJob.ArchivePlanId, cancellationToken)
                ?? throw EntityNotFoundException.ForArchivePlan(archiveJob.ArchivePlanId);

            if (plan.StorageProvider is null)
                throw new InvalidOperationException("Archive plan has no storage provider configured");

            var adapter = _storageFactory.CreateForProvider(plan.StorageProvider);

            var fileName = Path.GetFileName(archiveItemPath);
            var destinationFilePath = Path.Combine(job.DestinationPath, fileName);

            var directory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            long sizeBytes;
            using (var sourceStream = await adapter.ReadAsync(archiveItemPath, cancellationToken))
            {
                sizeBytes = sourceStream.Length;
                await using var destStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await sourceStream.CopyToAsync(destStream, cancellationToken);
            }

            var archiveItem = archiveJob.Items.FirstOrDefault(i => i.TargetPath == archiveItemPath);
            bool integrityVerified = false;

            if (archiveItem?.Hash is not null)
            {
                string writtenHash;
                await using (var verifyStream = new FileStream(destinationFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    var hashBytes = await SHA256.HashDataAsync(verifyStream, cancellationToken);
                    writtenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }

                integrityVerified = string.Equals(writtenHash, archiveItem.Hash, StringComparison.OrdinalIgnoreCase);

                if (!integrityVerified)
                {
                    _logger?.LogWarning("Hash mismatch for recovered item {ArchiveItemPath} in job {RecoveryJobId}",
                        archiveItemPath, recoveryJobId);
                }
            }

            job.RecordProgress(1, sizeBytes);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RecoveryItemResult(true, archiveItemPath, destinationFilePath, sizeBytes, integrityVerified, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to recover item {ArchiveItemPath} in job {RecoveryJobId}", archiveItemPath, recoveryJobId);
            return new RecoveryItemResult(false, archiveItemPath, null, null, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<RecoveryItemResult>> RecoverItemsAsync(Guid recoveryJobId, IEnumerable<string> archiveItemPaths, CancellationToken cancellationToken = default)
    {
        var results = new List<RecoveryItemResult>();

        foreach (var path in archiveItemPaths)
        {
            var result = await RecoverItemAsync(recoveryJobId, path, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<bool> ValidateArchiveIntegrityAsync(Guid archiveJobId, CancellationToken cancellationToken = default)
    {
        var archiveJob = await _unitOfWork.ArchiveJobs.GetByIdWithItemsAsync(archiveJobId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchiveJob(archiveJobId);

        var plan = await _unitOfWork.ArchivePlans.GetByIdAsync(archiveJob.ArchivePlanId, cancellationToken)
            ?? throw EntityNotFoundException.ForArchivePlan(archiveJob.ArchivePlanId);

        if (plan.StorageProvider is null)
        {
            _logger?.LogWarning("Cannot validate integrity — archive plan has no storage provider");
            return false;
        }

        var adapter = _storageFactory.CreateForProvider(plan.StorageProvider);

        foreach (var item in archiveJob.Items)
        {
            if (string.IsNullOrEmpty(item.TargetPath) || string.IsNullOrEmpty(item.Hash))
                continue;

            try
            {
                var storedHash = await adapter.CalculateHashAsync(item.TargetPath, cancellationToken);
                if (!string.Equals(storedHash, item.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("Integrity check failed for item {ItemId}: hash mismatch", item.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Integrity check failed for item {ItemId}", item.Id);
                return false;
            }
        }

        _logger?.LogInformation("Integrity validation passed for archive {ArchiveJobId}", archiveJobId);
        return true;
    }
}

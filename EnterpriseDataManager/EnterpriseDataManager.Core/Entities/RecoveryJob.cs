namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Enums;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public class RecoveryJob : BaseEntity
{
    public Guid ArchiveJobId { get; private set; }
    public ArchiveJob? ArchiveJob { get; private set; }
    public string DestinationPath { get; private set; } = default!;
    public ArchiveStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int TotalItems { get; private set; }
    public int RecoveredItems { get; private set; }
    public long TotalBytes { get; private set; }
    public long RecoveredBytes { get; private set; }

    protected RecoveryJob() { }

    private RecoveryJob(Guid archiveJobId, string destinationPath)
    {
        ArchiveJobId = archiveJobId;
        DestinationPath = destinationPath;
        Status = ArchiveStatus.Draft;
    }

    public static RecoveryJob Create(Guid archiveJobId, string destinationPath)
    {
        Guard.AgainstDefault(archiveJobId, ArchiveJobIdCannotBeEmpty);
        Guard.AgainstNullOrWhiteSpace(destinationPath, DestinationPathCannotBeEmpty);

        return new RecoveryJob(archiveJobId, destinationPath.Trim());
    }

    public void Start(int totalItems, long totalBytes)
    {
        Guard.AgainstInvalidOperation(
            Status != ArchiveStatus.Draft && Status != ArchiveStatus.Scheduled,
            string.Format(CannotStartJobInCurrentStatus, Status));

        Status = ArchiveStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        TotalItems = totalItems;
        TotalBytes = totalBytes;
    }

    public void RecordProgress(int items, long bytes)
    {
        RecoveredItems += items;
        RecoveredBytes += bytes;
    }

    public void Complete()
    {
        Guard.AgainstInvalidOperation(
            Status != ArchiveStatus.Running,
            string.Format(CannotCompleteJobInCurrentStatus, Status));

        Status = ArchiveStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string reason)
    {
        Guard.AgainstNullOrWhiteSpace(reason, FailureReasonCannotBeEmpty);

        Status = ArchiveStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Guard.AgainstInvalidOperation(
            Status == ArchiveStatus.Completed || Status == ArchiveStatus.Failed,
            string.Format(CannotCancelJobInCurrentStatus, Status));

        Status = ArchiveStatus.Canceled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public double GetProgressPercentage()
    {
        if (TotalItems == 0) return 0;
        return (double)RecoveredItems / TotalItems * 100;
    }

    public TimeSpan? GetDuration()
    {
        if (StartedAt is null) return null;
        var endTime = CompletedAt ?? DateTimeOffset.UtcNow;
        return endTime - StartedAt.Value;
    }
}

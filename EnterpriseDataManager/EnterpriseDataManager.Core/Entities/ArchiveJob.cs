namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Events;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public class ArchiveJob : BaseEntity
{
    private readonly List<ArchiveItem> _items = new();

    public Guid ArchivePlanId { get; private set; }
    public ArchivePlan? ArchivePlan { get; private set; }
    public ArchiveStatus Status { get; private set; }
    public JobPriority Priority { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int TotalItemCount { get; private set; }
    public int ProcessedItemCount { get; private set; }
    public int FailedItemCount { get; private set; }
    public long TotalBytes { get; private set; }
    public long ProcessedBytes { get; private set; }
    public string? TargetPath { get; private set; }
    public IReadOnlyCollection<ArchiveItem> Items => _items.AsReadOnly();

    protected ArchiveJob() { }

    private ArchiveJob(ArchivePlan plan, JobPriority priority)
    {
        ArchivePlanId = plan.Id;
        ArchivePlan = plan;
        Priority = priority;
        Status = ArchiveStatus.Draft;
    }

    internal static ArchiveJob Create(ArchivePlan plan, JobPriority priority = JobPriority.Normal)
    {
        var job = new ArchiveJob(plan, priority);
        job.AddDomainEvent(new ArchiveJobCreatedEvent(job.Id, plan.Id));
        return job;
    }

    public void Schedule(DateTimeOffset? scheduledTime = null)
    {
        EnsureStatus(ArchiveStatus.Draft);

        var oldStatus = Status;
        ScheduledAt = scheduledTime ?? DateTimeOffset.UtcNow;
        Status = ArchiveStatus.Scheduled;

        AddDomainEvent(new ArchiveJobStatusChangedEvent(Id, oldStatus, Status));
    }

    public void Start()
    {
        Guard.AgainstInvalidOperation(
            Status != ArchiveStatus.Scheduled && Status != ArchiveStatus.Draft,
            string.Format(CannotStartJobInCurrentStatus, Status));

        var oldStatus = Status;
        Status = ArchiveStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ArchiveJobStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new ArchiveJobStartedEvent(Id));
    }

    public void Complete()
    {
        EnsureStatus(ArchiveStatus.Running);

        var oldStatus = Status;
        Status = ArchiveStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;

        var duration = CompletedAt.Value - (StartedAt ?? CompletedAt.Value);
        AddDomainEvent(new ArchiveJobStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new ArchiveJobCompletedEvent(Id, ProcessedItemCount, ProcessedBytes, duration));
    }

    public void Fail(string reason)
    {
        Guard.AgainstNullOrWhiteSpace(reason, FailureReasonCannotBeEmpty);
        Guard.AgainstInvalidOperation(
            Status == ArchiveStatus.Completed || Status == ArchiveStatus.Canceled,
            string.Format(CannotFailJobInCurrentStatus, Status));

        var oldStatus = Status;
        Status = ArchiveStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ArchiveJobStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new ArchiveJobFailedEvent(Id, reason));
    }

    public void Cancel()
    {
        Guard.AgainstInvalidOperation(
            Status == ArchiveStatus.Completed || Status == ArchiveStatus.Failed,
            string.Format(CannotCancelJobInCurrentStatus, Status));

        var oldStatus = Status;
        Status = ArchiveStatus.Canceled;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ArchiveJobStatusChangedEvent(Id, oldStatus, Status));
    }

    public void SetTargetPath(string targetPath)
    {
        Guard.AgainstInvalidOperation(
            Status != ArchiveStatus.Draft && Status != ArchiveStatus.Scheduled,
            CannotModifyAfterJobStarted);

        TargetPath = targetPath;
    }

    public void SetItemCounts(int totalItems, long totalBytes)
    {
        TotalItemCount = totalItems;
        TotalBytes = totalBytes;
    }

    public ArchiveItem AddItem(string sourcePath, string targetPath, long sizeBytes)
    {
        var item = ArchiveItem.Create(this, sourcePath, targetPath, sizeBytes);
        _items.Add(item);
        return item;
    }

    public void RecordItemSuccess(ArchiveItem item, string? hash = null)
    {
        item.MarkSuccess(hash);
        ProcessedItemCount++;
        ProcessedBytes += item.SizeBytes;
    }

    public void RecordItemFailure(ArchiveItem item, string error)
    {
        item.MarkFailed(error);
        FailedItemCount++;
    }

    public double GetProgressPercentage()
    {
        if (TotalItemCount == 0) return 0;
        return (double)(ProcessedItemCount + FailedItemCount) / TotalItemCount * 100;
    }

    public TimeSpan? GetDuration()
    {
        if (StartedAt is null) return null;
        var endTime = CompletedAt ?? DateTimeOffset.UtcNow;
        return endTime - StartedAt.Value;
    }

    public bool IsTerminal => Status is ArchiveStatus.Completed or ArchiveStatus.Failed or ArchiveStatus.Canceled;

    private void EnsureStatus(ArchiveStatus expected)
    {
        Guard.AgainstInvalidOperation(
            Status != expected,
            string.Format(ExpectedStatusButWas, expected, Status));
    }
}

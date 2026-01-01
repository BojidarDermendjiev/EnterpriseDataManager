namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Events;
using EnterpriseDataManager.Core.ValueObjects;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public class ArchivePlan : BaseEntity
{
    private readonly List<ArchiveJob> _archiveJobs = new();

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public CronSchedule? Schedule { get; private set; }
    public StoragePath SourcePath { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public SecurityLevel SecurityLevel { get; private set; }
    public Guid? StorageProviderId { get; private set; }
    public StorageProvider? StorageProvider { get; private set; }
    public Guid? RetentionPolicyId { get; private set; }
    public RetentionPolicy? RetentionPolicy { get; private set; }
    public IReadOnlyCollection<ArchiveJob> ArchiveJobs => _archiveJobs.AsReadOnly();
    public DateTimeOffset? LastRunAt { get; private set; }
    public DateTimeOffset? NextRunAt { get; private set; }

    protected ArchivePlan() { }

    private ArchivePlan(string name, StoragePath sourcePath, SecurityLevel securityLevel)
    {
        Name = name;
        SourcePath = sourcePath;
        SecurityLevel = securityLevel;
        IsActive = false;
    }

    public static ArchivePlan Create(
        string name,
        string sourcePath,
        SecurityLevel securityLevel = SecurityLevel.Internal)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);

        var plan = new ArchivePlan(
            name.Trim(),
            StoragePath.Create(sourcePath),
            securityLevel);

        plan.AddDomainEvent(new ArchivePlanCreatedEvent(plan.Id, plan.Name));
        return plan;
    }

    public void UpdateDetails(string name, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, NameCannotBeEmpty);

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void SetSchedule(CronSchedule schedule)
    {
        Guard.AgainstNull(schedule, ValueCannotBeNull);
        Schedule = schedule;
    }

    public void ClearSchedule()
    {
        Schedule = null;
        NextRunAt = null;
    }

    public void SetStorageProvider(StorageProvider provider)
    {
        Guard.AgainstNull(provider, ValueCannotBeNull);
        StorageProvider = provider;
        StorageProviderId = provider.Id;
    }

    public void SetRetentionPolicy(RetentionPolicy policy)
    {
        Guard.AgainstNull(policy, ValueCannotBeNull);
        RetentionPolicy = policy;
        RetentionPolicyId = policy.Id;
    }

    public void SetSecurityLevel(SecurityLevel level) => SecurityLevel = level;

    public void Activate()
    {
        if (IsActive) return;

        Guard.AgainstInvalidOperation(
            StorageProviderId is null,
            CannotActivatePlanWithoutStorageProvider);

        IsActive = true;
        AddDomainEvent(new ArchivePlanActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        AddDomainEvent(new ArchivePlanDeactivatedEvent(Id));
    }

    public ArchiveJob CreateJob(JobPriority priority = JobPriority.Normal)
    {
        Guard.AgainstInvalidOperation(!IsActive, CannotCreateJobForInactivePlan);

        var job = ArchiveJob.Create(this, priority);
        _archiveJobs.Add(job);
        LastRunAt = DateTimeOffset.UtcNow;

        return job;
    }

    public void UpdateNextRunTime(DateTimeOffset nextRun) => NextRunAt = nextRun;
}

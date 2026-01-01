namespace EnterpriseDataManager.Core.Events;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Entities.Common;

public sealed record ArchiveJobCreatedEvent(Guid ArchiveJobId, Guid ArchivePlanId) : DomainEvent;

public sealed record ArchiveJobStartedEvent(Guid ArchiveJobId) : DomainEvent;

public sealed record ArchiveJobCompletedEvent(
    Guid ArchiveJobId,
    int TotalItems,
    long TotalBytes,
    TimeSpan Duration) : DomainEvent;

public sealed record ArchiveJobFailedEvent(
    Guid ArchiveJobId,
    string Reason) : DomainEvent;

public sealed record ArchiveJobStatusChangedEvent(
    Guid ArchiveJobId,
    ArchiveStatus OldStatus,
    ArchiveStatus NewStatus) : DomainEvent;

public sealed record ArchivePlanCreatedEvent(Guid ArchivePlanId, string Name) : DomainEvent;

public sealed record ArchivePlanActivatedEvent(Guid ArchivePlanId) : DomainEvent;

public sealed record ArchivePlanDeactivatedEvent(Guid ArchivePlanId) : DomainEvent;

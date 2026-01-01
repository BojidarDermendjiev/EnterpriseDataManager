namespace EnterpriseDataManager.Core.Events;

using EnterpriseDataManager.Core.Entities.Common;
public sealed record RetentionPolicyCreatedEvent(
    Guid RetentionPolicyId,
    string Name,
    TimeSpan RetentionPeriod) : DomainEvent;

public sealed record RetentionPolicyUpdatedEvent(
    Guid RetentionPolicyId,
    string Name) : DomainEvent;

public sealed record RetentionPolicyMadeImmutableEvent(Guid RetentionPolicyId) : DomainEvent;

public sealed record LegalHoldAppliedEvent(
    Guid RetentionPolicyId,
    string Reason) : DomainEvent;

public sealed record LegalHoldReleasedEvent(
    Guid RetentionPolicyId,
    string Reason) : DomainEvent;

public sealed record RetentionPeriodExpiredEvent(
    Guid ArchiveJobId,
    Guid RetentionPolicyId,
    DateTimeOffset ArchivedAt,
    DateTimeOffset ExpiredAt) : DomainEvent;

public sealed record DataDeletionScheduledEvent(
    Guid ArchiveJobId,
    DateTimeOffset ScheduledDeletionDate) : DomainEvent;

public sealed record DataDeletedEvent(
    Guid ArchiveJobId,
    int ItemsDeleted,
    long BytesReclaimed) : DomainEvent;
